# The Look agent's LookLab (Visiting Hours' golden-hour look): build it, bake its lighting, capture its stills, probe its
# frame time.
# Stages (-Stage): build     = import settings, the material library, the atmosphere renderer feature, the post profiles,
#                              and the LookLab scene (Assets/JamKit/Templates/VisitingHours/Look/Scenes/LookLab.unity)
#                  bake      = the scene's lighting: sky ambient + reflections, reflection probes, Adaptive Probe Volumes (GPU)
#                  capture   = twelve 1920x1080 stills to out/look-captures/<time>/ (GPU)
#                  all       = build, bake and capture in one Unity run (default)
#                  perfbuild = a Windows player of LookLab alone, to out/look-perf/ (no GPU work; ~5 min, ~300 MB on disk)
#                  perf      = runs that player with -perf-probe at 1920x1080 fullscreen (GPU, ~50 s): it flies the twelve shots
#                              and writes frame-time percentiles to out/look-perf/perf.txt (also grep JAMKIT_PERF in its log)
# Renders with the GPU, so Unity runs in batch mode WITH graphics (no -nographics). Run it one GPU job at a time.
param([ValidateSet('all', 'build', 'bake', 'capture', 'perfbuild', 'perf')][string]$Stage = 'all')
. "$PSScriptRoot\unity-env.ps1"

$perfDir = Join-Path $OutDir 'look-perf'
if ($Stage -eq 'perf') {
    $exe = Join-Path $perfDir 'LookLab.exe'
    if (-not (Test-Path $exe)) { Write-Host "No player at ${exe}: run -Stage perfbuild first"; exit 1 }
    $report = Join-Path $perfDir 'perf.txt'
    $log = Join-Path $perfDir 'player.log'
    $playerArgs = @('-perf-probe', '-perf-seconds', '42', '-perf-out', "`"$report`"", '-screen-width', '1920', '-screen-height', '1080',
        '-screen-fullscreen', '1', '-logFile', "`"$log`"")
    $p = Start-Process -FilePath $exe -ArgumentList $playerArgs -PassThru
    $null = $p.Handle
    $p.WaitForExit()
    if (Test-Path $report) { Get-Content $report | ForEach-Object { Write-Host "  $_" } } else { Write-Host "No report; see $log"; exit 1 }
    exit 0
}

Assert-ProjectUnlocked
$method = @{
    all       = 'JamKit.Look.EditorTools.VhLookLab.AllFromCommandLine'
    build     = 'JamKit.Look.EditorTools.VhLookLab.BuildFromCommandLine'
    bake      = 'JamKit.Look.EditorTools.VhLookLab.BakeFromCommandLine'
    capture   = 'JamKit.Look.EditorTools.VhLookLab.CaptureFromCommandLine'
    perfbuild = 'JamKit.Look.EditorTools.VhLookLab.BuildPerfPlayerFromCommandLine'
}[$Stage]
$unity = Get-UnityEditorPath
$log = Join-Path $OutDir "look-$Stage.log"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$unityArgs = @('-batchmode', '-projectPath', "`"$RepoRoot`"", '-logFile', "`"$log`"", '-executeMethod', $method)
if ($Stage -eq 'perfbuild') { $unityArgs += @('-vhOut', "`"$perfDir`"") }
Write-Host "Unity: $unity ($method)"
$p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
$null = $p.Handle
$p.WaitForExit()
Select-String -Path $log -Pattern 'JAMKIT_LOOK|error CS|Exception|Shader error' | Select-Object -Last 60 | ForEach-Object { Write-Host "  $($_.Line)" }
if ($p.ExitCode -ne 0) { Write-Host "Unity exited with $($p.ExitCode); see $log"; exit $p.ExitCode }
exit 0
