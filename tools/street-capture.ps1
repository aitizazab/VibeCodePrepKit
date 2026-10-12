# Stills of Larch Hill (docs/redesign/world.md "Captures"): the 25 views of core LarchHill.views (a vista from each band,
# the house and its block, Walt's shop and yard, the hospital on the skyline, every challenge at player height, the middle
# band and the top), 1920 x 1080 through the Look agent's capture rig, written to out/hill-captures/<time>/
# (JamKit.DreamEscape.EditorTools.DreamCaptureStreet). -DreamChanges captures with the bedside's changes on (the plank,
# the lit hospital) to out/hill-captures-changed/<time>/. -Build rebuilds the scene first (DreamStreetBuilder).
# Renders with the GPU, so Unity runs in batch mode WITH graphics (not Invoke-UnityBatch, which adds -nographics).
# Run it through the session memory cap:
#   uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -NoProfile -ExecutionPolicy Bypass -File tools/street-capture.ps1 -Build
param([switch]$Build, [switch]$DreamChanges)
. "$PSScriptRoot\unity-env.ps1"
Assert-ProjectUnlocked

$unity = Get-UnityEditorPath
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Invoke-Hill([string]$method, [string]$logName, [string[]]$extra) {
    $log = Join-Path $OutDir $logName
    $unityArgs = @('-batchmode', '-projectPath', "`"$RepoRoot`"", '-logFile', "`"$log`"", '-executeMethod', $method) + $extra
    Write-Host "Unity: $method"
    $p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
    $null = $p.Handle
    $p.WaitForExit()
    Select-String -Path $log -Pattern 'JAMKIT_STREET|JAMKIT_LOOK_CAPTURE|JAMKIT_DREAM_CAPTURE|error CS|Exception' | Select-Object -Last 60 | ForEach-Object { Write-Host "  $($_.Line)" }
    if ($p.ExitCode -ne 0) { Write-Host "Unity exited with $($p.ExitCode); see $log"; exit $p.ExitCode }
}

if ($Build) { Invoke-Hill 'JamKit.DreamEscape.EditorTools.DreamStreetBuilder.BuildFromCommandLine' 'street-build.log' @() }
$extra = @()
if ($DreamChanges) { $extra = @('-hillDreamChanges', 'all') }
Invoke-Hill 'JamKit.DreamEscape.EditorTools.DreamCaptureStreet.CaptureStreetFromCommandLine' 'street-capture.log' $extra
exit 0
