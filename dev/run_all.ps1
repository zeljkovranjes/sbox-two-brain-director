[CmdletBinding()]
param([switch]$SkipEditor, [switch]$CleanEditor)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
function Run([string]$label, [scriptblock]$command) {
    Write-Host "===== $label =====" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$label failed with exit code $LASTEXITCODE" }
}
Push-Location $root
try {
    Run "Deterministic core suite" { dotnet test dev\SboxTwoBrains.Tests\SboxTwoBrains.Tests.csproj }
    Run "s&box library build" { dotnet build Code\two_brain_director.csproj --no-restore }
    Run "Examples build" { dotnet build examples\TwoBrains.Examples.csproj --no-restore }
    if (-not $SkipEditor) {
        $arguments = @("-ExecutionPolicy", "Bypass", "-File", "dev\editor-rig\run_editor_gate.ps1")
        if ($CleanEditor) { $arguments += "-Clean" }
        Run "Real s&box editor gate" { powershell @arguments }
    }
} finally { Pop-Location }
Write-Host "ALL TWO-BRAIN DIRECTOR CHECKS PASSED" -ForegroundColor Green
