# Skyline Repo's pure C# tests, plus the Movement suite as the engine gate (docs/SKYLINE-DESIGN.md section 8).
#   powershell -File tools/skyline/test.ps1
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$bad = @()
foreach ($p in 'Skyline', 'Movement') {
    & dotnet test (Join-Path $root "core-tests\$p\$p.Tests.csproj") --nologo -v q
    if ($LASTEXITCODE -ne 0) { $bad += $p }
}
if ($bad.Count -gt 0) { Write-Host "Failed: $($bad -join ', ')"; exit 1 }
Write-Host 'Skyline and Movement tests passed.'
