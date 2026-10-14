# Skyline Repo's pure C# tests, plus the Movement suite as the engine gate (docs/SKYLINE-DESIGN.md section 8), then the
# Unity-side compile check (core-tests/SkylineUnity: the runtime, its tests and Assets/JamKit/Templates/SkylineRepo against
# the Unity DLLs; compile only, nothing runs and Unity is not started).
#   powershell -File tools/skyline/test.ps1
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$bad = @()
foreach ($p in 'Skyline', 'Movement') {
    & dotnet test (Join-Path $root "core-tests\$p\$p.Tests.csproj") --nologo -v q
    if ($LASTEXITCODE -ne 0) { $bad += $p }
}
& dotnet build (Join-Path $root 'core-tests\SkylineUnity\SkylineUnity.Check.csproj') --nologo -v q -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { $bad += 'SkylineUnity (Unity-side compile check)' }
& powershell -NoProfile -File (Join-Path $root 'core-tests\SkylineUnity\check-asmdefs.ps1')
if ($LASTEXITCODE -ne 0) { $bad += 'SkylineUnity per-assembly check (a missing asmdef reference)' }
if ($bad.Count -gt 0) { Write-Host "Failed: $($bad -join ', ')"; exit 1 }
Write-Host 'Skyline and Movement tests passed; the Unity-side compile check is green.'
