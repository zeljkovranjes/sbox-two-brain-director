# Editor-process compile gate driver (adapted from humanoid-retargeter's verified rig).
#
# Creates a scratch s&box game project, installs this library into it via NTFS
# junction, launches sbox-dev.exe on it, and waits for the in-editor hook
# (Editor/CompileGate.cs, armed by TB_GATE_RESULT) to write a JSON result file.
# Reports pass/fail from that JSON.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File dev\editor-rig\run_editor_gate.ps1
#   ... -Clean          # wipe the scratch project first
#   ... -CopyLibrary    # robocopy the library instead of junction
#
# Exit codes: 0 = gate passed, 1 = gate ran but failed, 2 = no result produced.

[CmdletBinding()]
param(
    [string]$SboxRoot = "D:\SteamLibrary\steamapps\common\sbox",
    [int]$TimeoutSec = 480,
    [switch]$Clean,
    [switch]$CopyLibrary
)

$ErrorActionPreference = "Stop"

$repoRoot   = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$scratch    = Join-Path $env:TEMP "tb-editor-rig\scratch"  # OUTSIDE the repo: a scratch project inside the library tree creates a junction CYCLE (lib -> repo -> scratch -> lib) that intermittently crashes the s&box editor when this repo is junctioned into other projects
$sbproj     = Join-Path $scratch "tbscratch.sbproj"
$libDir     = Join-Path $scratch "Libraries\local.two_brain_director"
$resultPath = Join-Path $PSScriptRoot "gate_result.json"
$sboxExe    = Join-Path $SboxRoot "sbox-dev.exe"
$sboxLog    = Join-Path $SboxRoot "logs\sbox-dev.log"
$template   = Join-Path $SboxRoot "templates\game.minimal"

function Fail([int]$code, [string]$msg) { Write-Host "RESULT: $msg" -ForegroundColor Red; exit $code }

if (-not (Test-Path $sboxExe))  { Fail 2 "sbox-dev.exe not found at $sboxExe" }

# --- Steam check (editor generally wants Steam running) -------------------------
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) {
    Write-Warning "steam.exe is not running - sbox-dev.exe may fail to boot."
}

# --- scratch project -------------------------------------------------------------
if ($Clean -and (Test-Path $scratch)) {
    Write-Host "Cleaning scratch project..."
    # remove junction first so the repo is never touched by the recursive delete
    if (Test-Path $libDir) { cmd /c rmdir "$libDir" | Out-Null }
    Remove-Item -Recurse -Force $scratch
}

if (-not (Test-Path $sbproj)) {
    Write-Host "Creating scratch project at $scratch"
    New-Item -ItemType Directory -Force $scratch | Out-Null
    Copy-Item (Join-Path $template "Assets") (Join-Path $scratch "Assets") -Recurse -Force
    Copy-Item (Join-Path $template "Code")   (Join-Path $scratch "Code")   -Recurse -Force
    Copy-Item (Join-Path $template "Editor") (Join-Path $scratch "Editor") -Recurse -Force

    $proj = Get-Content (Join-Path $template "`$ident.sbproj") -Raw
    $proj = $proj -replace '"Title":\s*"[^"]*"', '"Title": "TB Scratch"'
    $proj = $proj -replace '"Ident":\s*"[^"]*"', '"Ident": "tbscratch"'
    [System.IO.File]::WriteAllText($sbproj, $proj)
}

# --- install library (junction by default) --------------------------------------
New-Item -ItemType Directory -Force (Join-Path $scratch "Libraries") | Out-Null

if ($CopyLibrary) {
    if (Test-Path $libDir) {
        $item = Get-Item $libDir -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { cmd /c rmdir "$libDir" | Out-Null }
    }
    Write-Host "Syncing library via robocopy..."
    robocopy "$repoRoot" "$libDir" /MIR /XD .git .claude dev docs research reference examples /XF *.user /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { Fail 2 "robocopy failed with code $LASTEXITCODE" }
} else {
    if (-not (Test-Path $libDir)) {
        Write-Host "Creating library junction $libDir -> $repoRoot"
        New-Item -ItemType Junction -Path $libDir -Value $repoRoot | Out-Null
    }
}

# --- fresh run state -------------------------------------------------------------
Remove-Item $resultPath -Force -ErrorAction SilentlyContinue
# One-shot arming marker: the in-editor gate only runs when this file exists (and
# consumes it). Protects real sessions from a leaked TB_GATE_RESULT env var - Steam
# booted as a gate child inherits the var and passes it to every editor launch.
Set-Content -Path "$resultPath.arm" -Value (Get-Date -Format o) -Encoding ascii

$preLogLen = 0
if (Test-Path $sboxLog) { $preLogLen = (Get-Item $sboxLog).Length }

# --- launch ----------------------------------------------------------------------
$env:TB_GATE_RESULT = $resultPath
try {
    Write-Host "Launching: `"$sboxExe`" -project `"$sbproj`""
    $proc = Start-Process -FilePath $sboxExe -ArgumentList @("-project", "`"$sbproj`"") `
        -WorkingDirectory $SboxRoot -PassThru
} finally {
    Remove-Item Env:TB_GATE_RESULT -ErrorAction SilentlyContinue
}

# --- wait for completed result / process exit / timeout --------------------------
$deadline = (Get-Date).AddSeconds($TimeoutSec)
$completed = $false
while ((Get-Date) -lt $deadline) {
    if (Test-Path $resultPath) {
        try {
            $j = Get-Content $resultPath -Raw | ConvertFrom-Json
            if ($j.completed) { $completed = $true; break }
        } catch { } # mid-write, retry
    }
    if ($proc.HasExited) { break }
    Start-Sleep -Seconds 2
}

if ($completed -and -not $proc.HasExited) {
    # give the hook's clean quit a chance before killing
    $proc.WaitForExit(20000) | Out-Null
}
if (-not $proc.HasExited) {
    Write-Warning "Editor still running - killing process tree (pid $($proc.Id))."
    taskkill /PID $proc.Id /T /F | Out-Null
}

# --- report -----------------------------------------------------------------------
Write-Host ""
Write-Host "===== sbox-dev.log (this run, filtered) =====" -ForegroundColor Cyan
if (Test-Path $sboxLog) {
    $fs = [System.IO.File]::Open($sboxLog, 'Open', 'Read', 'ReadWrite')
    try {
        # the editor may truncate/recreate the log on boot - restart from 0 then
        if ($fs.Length -lt $preLogLen) { $preLogLen = 0 }
        if ($fs.Length -gt $preLogLen) {
            $fs.Seek($preLogLen, 'Begin') | Out-Null
            $sr = New-Object System.IO.StreamReader($fs)
            $newLog = $sr.ReadToEnd() -split "`r?`n"
            $interesting = $newLog | Where-Object { $_ -match 'SB500|twobrains|two_brain|tb-gate|whitelist|\[tb' }
            $interesting | Select-Object -First 120 | ForEach-Object { Write-Host $_ }
            Write-Host "----- last 25 log lines -----" -ForegroundColor Cyan
            $newLog | Select-Object -Last 25 | ForEach-Object { Write-Host $_ }
        } else {
            Write-Host "(no new log output)"
        }
    } finally { $fs.Dispose() }
} else {
    Write-Host "(no sbox-dev.log found)"
}

Write-Host ""
Write-Host "===== gate_result.json =====" -ForegroundColor Cyan
if (-not (Test-Path $resultPath)) {
    Fail 2 "NO RESULT - the editor hook never wrote $resultPath (editor crash, compile failure of the library, or hook never armed). Check the log above."
}

$raw = Get-Content $resultPath -Raw
Write-Host $raw
$res = $raw | ConvertFrom-Json

if (-not $res.completed) {
    Fail 2 "PARTIAL RESULT - hook started but never completed (see json log above)."
}
if ($res.libraryAssemblyFound -and $res.coreTypeFound -and $res.compileErrors.Count -eq 0) {
    Write-Host "RESULT: PASS - library compiled in the real editor (assembly=$($res.libraryAssemblyName), core type=$($res.coreTypeName))." -ForegroundColor Green
    exit 0
}
Fail 1 "FAIL - libraryAssemblyFound=$($res.libraryAssemblyFound) coreTypeFound=$($res.coreTypeFound) compileErrors=$($res.compileErrors.Count)"
