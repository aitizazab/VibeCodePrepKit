# Stills of Visiting Hours' opening and title lighting (docs/OPENING-AND-TITLE.md "Lighting"):
# the key moments on the PC quality level and on the one WebGL uses, written to out/vh-lighting/<time>/.
# Renders with the GPU, so Unity runs in batch mode WITH graphics (not Invoke-UnityBatch, which adds -nographics).
# Run it through the session memory cap:
#   uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -NoProfile -ExecutionPolicy Bypass -File tools/vh-lighting-capture.ps1
. "$PSScriptRoot\unity-env.ps1"
Assert-ProjectUnlocked

$unity = Get-UnityEditorPath
$log = Join-Path $OutDir 'vh-lighting-capture.log'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$unityArgs = @('-batchmode', '-projectPath', "`"$RepoRoot`"", '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.VisitingHours.EditorTools.VhLightingCapture.CaptureFromCommandLine')
Write-Host "Unity: $unity"
$p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
$null = $p.Handle
$p.WaitForExit()
Select-String -Path $log -Pattern 'JAMKIT_VH_CAPTURE|error CS|Exception' | Select-Object -Last 30 | ForEach-Object { Write-Host "  $($_.Line)" }
if ($p.ExitCode -ne 0) { Write-Host "Unity exited with $($p.ExitCode); see $log"; exit $p.ExitCode }
exit 0
