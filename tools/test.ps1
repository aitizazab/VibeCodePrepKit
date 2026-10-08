# Runs the tests: the pure C# core (dotnet, no Unity), then Unity EditMode and PlayMode.
#   powershell -File tools/test.ps1 [-Only Core|EditMode|PlayMode]
# Unity's exit codes from -runTests aren't reliable, so the results XML decides.
param([ValidateSet('All', 'Core', 'EditMode', 'PlayMode')][string]$Only = 'All')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-env.ps1"
$bad = @()

if ($Only -in 'All', 'Core') {
    & dotnet test (Join-Path $RepoRoot 'core-tests\All\All.Tests.csproj') --nologo -v q
    if ($LASTEXITCODE -ne 0) { $bad += 'Core' }
    # Same sources against Unity's own NUnit 3.5 (compile only; needs one Unity open first)
    if (Test-Path (Join-Path $RepoRoot 'Library\PackageCache')) {
        & dotnet build (Join-Path $RepoRoot 'core-tests\UnityApi\UnityApi.Check.csproj') --nologo -v q
        if ($LASTEXITCODE -ne 0) { $bad += 'Core-UnityNUnit' }
    }
}

foreach ($platform in 'EditMode', 'PlayMode') {
    if ($Only -notin 'All', $platform) { continue }
    if (-not (Test-Path (Join-Path $RepoRoot 'ProjectSettings'))) {
        Write-Host "${platform}: skipped (no Unity project yet)"
        continue
    }
    Assert-ProjectUnlocked
    $xml = Join-Path $OutDir "tests-$platform.xml"
    if (Test-Path $xml) { Remove-Item $xml }
    $null = Invoke-UnityBatch -UnityArgs @('-runTests', '-testPlatform', $platform, '-testResults', "`"$xml`"") `
        -LogFile (Join-Path $OutDir "tests-$platform.log")
    if (-not (Test-Path $xml)) {
        Write-Host "${platform}: no results file (compile error?). See out/tests-$platform.log"
        $bad += $platform
        continue
    }
    $run = ([xml](Get-Content $xml -Raw)).'test-run'
    Write-Host ("{0}: {1} - {2} passed, {3} failed, {4} skipped" -f $platform, $run.result, $run.passed, $run.failed, $run.skipped)
    if ($run.result -notlike 'Passed*') {
        $bad += $platform
        ([xml](Get-Content $xml -Raw)).SelectNodes("//test-case[@result='Failed']") | Select-Object -First 10 |
            ForEach-Object { Write-Host "  FAIL $($_.fullname): $($_.failure.message.'#cdata-section')" }
    }
}
if ($bad.Count -gt 0) { Write-Host "Failed: $($bad -join ', ')"; exit 1 }
Write-Host 'All tests passed.'
