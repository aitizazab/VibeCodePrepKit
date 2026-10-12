# Stills of Larch Hill (docs/redesign/world.md "Captures"): the 25 views of core LarchHill.views (a vista from each band,
# the house and its block, Walt's shop and yard, the hospital on the skyline, every challenge at player height, the middle
# band and the top), 1920 x 1080 through the Look agent's capture rig, written to out/hill-captures/<time>/
# (JamKit.DreamEscape.EditorTools.DreamCaptureStreet). -DreamChanges captures with the bedside's changes on (the plank,
# the lit hospital) to out/hill-captures-changed/<time>/. -Build rebuilds the scene first (DreamStreetBuilder).
# The build (-Build) runs WITHOUT graphics (-nographics): it needs no GPU, and with one (DirectX 12) every mesh it made was
# most likely also uploaded through staging memory the batch editor never recycled (the 6.8 GB crash had 0.7 GB in
# Unity's own allocators). The capture renders with the GPU, so it runs in batch mode WITH graphics.
# Both log a memory and size report (grep JAMKIT_STREET_MEM / JAMKIT_STREET_SIZE; also out/street-report/<time>-*.txt).
# Run it through the session memory cap:
#   uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -NoProfile -ExecutionPolicy Bypass -File tools/street-capture.ps1 -Build
# -D3D11 renders the capture on DirectX 11 instead of the default 12 (try it if the capture runs out of memory).
param([switch]$Build, [switch]$DreamChanges, [switch]$D3D11)
. "$PSScriptRoot\unity-env.ps1"
Assert-ProjectUnlocked

$unity = Get-UnityEditorPath
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Invoke-Hill([string]$method, [string]$logName, [string[]]$extra, [switch]$NoGraphics) {
    $log = Join-Path $OutDir $logName
    $unityArgs = @('-batchmode')
    if ($NoGraphics) { $unityArgs += '-nographics' }
    $unityArgs += @('-projectPath', "`"$RepoRoot`"", '-logFile', "`"$log`"", '-executeMethod', $method) + $extra
    Write-Host "Unity: $method"
    $p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
    $null = $p.Handle
    $p.WaitForExit()
    Select-String -Path $log -Pattern 'JAMKIT_STREET|JAMKIT_LOOK_CAPTURE|JAMKIT_DREAM_CAPTURE|error CS|Exception' | Select-Object -Last 80 | ForEach-Object { Write-Host "  $($_.Line)" }
    if ($p.ExitCode -ne 0) { Write-Host "Unity exited with $($p.ExitCode); see $log"; exit $p.ExitCode }
}

if ($Build) { Invoke-Hill 'JamKit.DreamEscape.EditorTools.DreamStreetBuilder.BuildFromCommandLine' 'street-build.log' @() -NoGraphics }
$extra = @()
if ($DreamChanges) { $extra = @('-hillDreamChanges', 'all') }
if ($D3D11) { $extra += '-force-d3d11' }
Invoke-Hill 'JamKit.DreamEscape.EditorTools.DreamCaptureStreet.CaptureStreetFromCommandLine' 'street-capture.log' $extra
exit 0
