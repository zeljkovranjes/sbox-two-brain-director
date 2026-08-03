# Restructure to the adaptive-director layout: flat Code/SboxTwoBrains (+ Sandbox/).
# RUN ONLY AFTER the macro/micro implementation agents have finished.
# Usage: powershell -ExecutionPolicy Bypass -File dev\restructure.ps1
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $root
try {
    $coreOld = "Code\TwoBrainsCore"
    $coreNew = "Code\SboxTwoBrains"
    $sandbox = Join-Path $coreNew "Sandbox"

    if (-not (Test-Path $coreOld)) { throw "$coreOld not found - already restructured?" }
    New-Item -ItemType Directory -Force $coreNew | Out-Null
    New-Item -ItemType Directory -Force $sandbox | Out-Null

    # 1. move all core .cs files flat into Code/SboxTwoBrains
    Get-ChildItem -Recurse -Filter *.cs $coreOld | ForEach-Object {
        $dest = Join-Path $coreNew $_.Name
        if (Test-Path $dest) { throw "name collision flattening: $($_.Name) exists twice" }
        Move-Item $_.FullName $dest
    }
    Remove-Item -Recurse -Force $coreOld

    # 2. rewrite namespaces + usings in library, tests
    $targets = @()
    $targets += Get-ChildItem -Recurse -Filter *.cs $coreNew
    $targets += Get-ChildItem -Recurse -Filter *.cs "dev\SboxTwoBrains.Tests" | Where-Object { $_.FullName -notmatch "obj|bin" }

    foreach ($file in $targets) {
        $text = [System.IO.File]::ReadAllText($file.FullName)
        $orig = $text
        # namespace declarations: TwoBrains.Core(.X)? -> SboxTwoBrains  (keep .Compat sub-namespace? no - flat)
        $text = [regex]::Replace($text, 'namespace\s+TwoBrains\.Core(\.[A-Za-z]+)?\s*;', 'namespace SboxTwoBrains;')
        $text = [regex]::Replace($text, 'namespace\s+TwoBrains\.Core(\.[A-Za-z]+)?\s*\{', 'namespace SboxTwoBrains {')
        # usings: collapse all TwoBrains.Core.* to SboxTwoBrains, then dedupe identical lines
        $text = [regex]::Replace($text, 'using\s+TwoBrains\.Core(\.[A-Za-z]+)?\s*;', 'using SboxTwoBrains;')
        $text = $text -replace 'TwoBrains\.Core\.Tests', 'SboxTwoBrains.Tests'
        $text = $text -replace 'TwoBrains\.Core\.', 'SboxTwoBrains.'
        $text = $text -replace 'TwoBrains\.Core', 'SboxTwoBrains'
        # dedupe identical using lines (keep first occurrence)
        $seen = @{}
        $lines = $text -split "`r?`n" | ForEach-Object {
            $l = $_
            if ($l -match '^\s*using\s+[A-Za-z0-9_.]+;\s*$') {
                if ($seen.ContainsKey($l.Trim())) { return $null }
                $seen[$l.Trim()] = $true
            }
            return $l
        } | Where-Object { $_ -ne $null }
        $text = $lines -join "`n"
        if ($text -ne $orig) { [System.IO.File]::WriteAllText($file.FullName, $text) }
    }

    # 3. update test csproj include path + exclude Sandbox (engine refs cannot compile under dotnet)
    $csproj = "dev\SboxTwoBrains.Tests\SboxTwoBrains.Tests.csproj"
    $xml = [System.IO.File]::ReadAllText($csproj)
    $xml = $xml -replace 'Code\\TwoBrainsCore\\\*\*\\\*\.cs', 'Code\SboxTwoBrains\**\*.cs'
    if ($xml -notmatch 'Sandbox') {
        $xml = $xml -replace '(<Compile Include="\.\.\\\.\.\\Code\\SboxTwoBrains\\\*\*\\\*\.cs") */>', '$1 Exclude="..\..\Code\SboxTwoBrains\Sandbox\**\*.cs" />'
    }
    [System.IO.File]::WriteAllText($csproj, $xml)

    Write-Host "Restructure complete. Next: dotnet test dev\SboxTwoBrains.Tests\SboxTwoBrains.Tests.csproj" -ForegroundColor Green
} finally { Pop-Location }
