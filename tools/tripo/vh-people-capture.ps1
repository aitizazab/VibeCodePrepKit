# The 3D cast (docs/redesign/people.md): builds the prefabs, then captures the lineup, turntables and face close-ups
# to out/vh-people/<time>/. Renders with the GPU, so Unity runs in batch mode WITH graphics (no -nographics).
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/tripo/vh-people-capture.ps1            # build + capture
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/tripo/vh-people-capture.ps1 -BuildOnly
# Run it through the session memory cap:
#   uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -NoProfile -ExecutionPolicy Bypass -File tools/tripo/vh-people-capture.ps1
param([switch]$BuildOnly)
. "$PSScriptRoot\..\unity-env.ps1"
Assert-ProjectUnlocked

$unity = Get-UnityEditorPath
New-Item -ItemType Directory -Force $OutDir | Out-Null
$steps = @(@{ name = 'build'; method = 'JamKit.VisitingHours.Characters.EditorTools.VhPeopleBuild.BuildFromCommandLine' })
if (-not $BuildOnly) { $steps += @{ name = 'capture'; method = 'JamKit.VisitingHours.Characters.EditorTools.VhPeopleCapture.CaptureFromCommandLine' } }
foreach ($s in $steps) {
    $log = Join-Path $OutDir "vh-people-$($s.name).log"
    $unityArgs = @('-batchmode', '-projectPath', "`"$RepoRoot`"", '-logFile', "`"$log`"", '-executeMethod', $s.method)
    Write-Host "Unity ($($s.name)): $unity"
    $p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
    $null = $p.Handle
    $p.WaitForExit()
    Select-String -Path $log -Pattern 'JAMKIT_VH_PEOPLE|^(Elias|EliasBed|Mags|Rafael|Representative|animations)|error CS|Exception|Shader error' | Select-Object -Last 40 | ForEach-Object { Write-Host "  $($_.Line)" }
    if ($p.ExitCode -ne 0) { Write-Host "Unity exited with $($p.ExitCode); see $log"; exit $p.ExitCode }
}
exit 0
