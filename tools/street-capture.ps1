# Stills of Night 1's street on the hill (docs/LARCH-HILL.md): the bottom (the whole climb, the shop), the middle (up to the
# tower, down the road), the top (the vista from the lookout, the house), the rooftops and the ravine, written to
# out/dream-captures/<time>-street/ (JamKit.DreamEscape.EditorTools.DreamCapture.CaptureStreet). The scene must be built
# first (Rebuild Shell Scenes, e.g. tools/verify.ps1 -From Scenes -To Scenes).
# Renders with the GPU, so Unity runs in batch mode WITH graphics (not Invoke-UnityBatch, which adds -nographics).
# Run it through the session memory cap:
#   uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -NoProfile -ExecutionPolicy Bypass -File tools/street-capture.ps1
. "$PSScriptRoot\unity-env.ps1"
Assert-ProjectUnlocked

$unity = Get-UnityEditorPath
$log = Join-Path $OutDir 'street-capture.log'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$unityArgs = @('-batchmode', '-projectPath', "`"$RepoRoot`"", '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.DreamEscape.EditorTools.DreamCapture.CaptureStreetFromCommandLine')
Write-Host "Unity: $unity"
$p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
$null = $p.Handle
$p.WaitForExit()
Select-String -Path $log -Pattern 'JAMKIT_DREAM_CAPTURE|JAMKIT_STREET art|error CS|Exception' | Select-Object -Last 40 | ForEach-Object { Write-Host "  $($_.Line)" }
if ($p.ExitCode -ne 0) { Write-Host "Unity exited with $($p.ExitCode); see $log"; exit $p.ExitCode }
exit 0
