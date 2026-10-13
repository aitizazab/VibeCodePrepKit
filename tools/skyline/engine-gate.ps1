# The engine gate (docs/SKYLINE-DESIGN.md 8): "the full Movement test suite, plus the Dream Escape and Deep Block
# route-time pins, unchanged". Record the route times BEFORE an engine change, check them after.
#   powershell -NoProfile -File tools/skyline/engine-gate.ps1 -Record   # before: writes out/skyline-gate/route-times.txt
#   powershell -NoProfile -File tools/skyline/engine-gate.ps1           # after: Movement green, route times identical
# It also checks SharedTuningTests ran (Passed 1, Skipped 0: the shared tuning asset was found and still holds MoveDef's
# defaults; plan amendment 3), and compile-checks the Unity layer (Visiting Hours' core-tests/UnityLayerPlayer), since
# MovementController.cs is shared. A failure there that isn't in MovementController.cs is another session's: report it,
# don't fix it.
# In a scratch worktree with no Library folder, point the Unity-layer check at a checkout Unity has built:
#   powershell -NoProfile -File tools/skyline/engine-gate.ps1 -UnityLibrary C:\path\to\VibeJamKit\Library
param([switch]$Record, [string]$UnityLibrary = '')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$gateDir = Join-Path $root 'out\skyline-gate'
$baseline = Join-Path $gateDir 'route-times.txt'
$movement = Join-Path $root 'core-tests\Movement\Movement.Tests.csproj'

function Get-RouteTimes {
    $lines = @()
    $runs = @(
        @{ Name = 'DreamEscape'; Filter = 'FullyQualifiedName~TheAutopilot' },
        @{ Name = 'DeepBlock'; Filter = 'FullyQualifiedName~TheAutopilotRunsTheRouteToTheHillGate' }
    )
    foreach ($r in $runs) {
        $proj = Join-Path $root "core-tests\$($r.Name)\$($r.Name).Tests.csproj"
        $ErrorActionPreference = 'Continue'
        $out = & dotnet test $proj --nologo --filter $r.Filter --logger 'console;verbosity=detailed' 2>&1 | ForEach-Object { "$_" }
        $code = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($code -ne 0) { throw "$($r.Name): the autopilot tests failed (exit $code)" }
        $lines += $out | Where-Object { $_ -match 'finished (True|False) in [0-9.]+ s' -or $_ -match "the doc's rough time" } |
            ForEach-Object { "$($r.Name): $($_.Trim())" }
    }
    if ($lines.Count -eq 0) { throw 'no route times in the test output: the tests changed what they print; update this gate' }
    return @($lines | Sort-Object)
}

& dotnet test $movement --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host 'ENGINE GATE FAILED: the Movement suite'; exit 1 }

# SharedTuningTests must really run (an Ignore means the asset wasn't found, so the check proved nothing).
$ErrorActionPreference = 'Continue'
$tuning = & dotnet test $movement --nologo -v q --filter 'FullyQualifiedName~SharedTuningTests' 2>&1 | ForEach-Object { "$_" }
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
$summary = $tuning | Where-Object { $_ -match 'Passed:\s*\d+' } | Select-Object -Last 1
if ($code -ne 0 -or -not $summary -or $summary -notmatch 'Passed:\s*1,' -or $summary -notmatch 'Skipped:\s*0,') {
    Write-Host "ENGINE GATE FAILED: SharedTuningTests must be Passed 1, Skipped 0 (got: $summary)"
    exit 1
}

$unityArgs = @('--nologo', '-v', 'q', '-p:NuGetAudit=false')
if ($UnityLibrary -ne '') { $unityArgs += "-p:ScriptAssemblies=$((Resolve-Path $UnityLibrary).Path)\ScriptAssemblies\" }
& dotnet build (Join-Path $root 'core-tests\UnityLayerPlayer\UnityLayerPlayer.Check.csproj') @unityArgs
if ($LASTEXITCODE -ne 0) { Write-Host 'ENGINE GATE FAILED: the Unity layer does not compile (is the error in MovementController.cs?)'; exit 1 }

$times = Get-RouteTimes
New-Item -ItemType Directory -Force $gateDir | Out-Null
if ($Record) {
    Set-Content -Path $baseline -Value $times -Encoding utf8
    Write-Host "Recorded $($times.Count) route times in $baseline"
    exit 0
}
if (-not (Test-Path $baseline)) { Write-Host 'ENGINE GATE FAILED: no baseline; run with -Record before the engine change'; exit 1 }
$before = @(Get-Content $baseline)
$diff = Compare-Object $before $times
if ($diff) {
    Write-Host 'ENGINE GATE FAILED: route times changed:'
    $diff | ForEach-Object { Write-Host "  $($_.SideIndicator) $($_.InputObject)" }
    exit 1
}
Write-Host "ENGINE GATE OK: Movement green, SharedTuningTests 1 passed 0 skipped, the Unity layer compiles, $($times.Count) route times unchanged"
exit 0
