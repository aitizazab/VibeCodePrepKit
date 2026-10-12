# Stills of the dream scenes' art (Night 2's house, its hide-and-seek and its escape, then the dream escape) at fixed
# spots, written to out/dream-captures/<time>/ (JamKit.DreamEscape.EditorTools.DreamCapture, which calls
# DreamHouseCapture.Stills for the house; shots.txt says what each one shows). The scenes must be built first (Rebuild
# Shell Scenes, e.g. tools/verify.ps1). Renders with the GPU, so Unity runs in batch mode WITH graphics (not
# Invoke-UnityBatch, which adds -nographics). Only the house (stills and recorded runs): tools/house-capture.ps1.
# Run it through the session memory cap:
#   uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -NoProfile -ExecutionPolicy Bypass -File tools/dream-capture.ps1
. "$PSScriptRoot\unity-env.ps1"
Assert-ProjectUnlocked

$unity = Get-UnityEditorPath
$log = Join-Path $OutDir 'dream-capture.log'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$unityArgs = @('-batchmode', '-projectPath', "`"$RepoRoot`"", '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.DreamEscape.EditorTools.DreamCapture.CaptureFromCommandLine')
Write-Host "Unity: $unity"
$p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
$null = $p.Handle
$p.WaitForExit()
Select-String -Path $log -Pattern 'JAMKIT_DREAM_CAPTURE|JAMKIT_HOUSE_CAPTURE|error CS|Exception' | Select-Object -Last 60 | ForEach-Object { Write-Host "  $($_.Line)" }
if ($p.ExitCode -ne 0) { Write-Host "Unity exited with $($p.ExitCode); see $log"; exit $p.ExitCode }
exit 0
