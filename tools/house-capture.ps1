# Night 2's house for the judge (docs/redesign/house.md "Captures"):
#   1. the stills (JamKit.DreamEscape.EditorTools.DreamHouseCapture.StillsFromCommandLine): every room at her eye height,
#      the hide-and-seek's moments, each escape beat, Chapel Row with Lena at the window; 1920x1080 PNGs and shots.txt
#      into out/house-captures/<time>/;
#   2. the recorded runs, in the BUILT PLAYER (no editor window opens: the editor taking focus interrupted the creator,
#      2026-10-12): VisitingHours.exe -house-record <folder> -house-record-run all|hideandseek|escape. The house plays
#      itself on the autopilot, a 1920x1080 JPG every 0.5 s of game time and run.txt (what happened, by time) go into
#      out/house-runs/<time>/ (hide-and-seek/, escape/), and the player quits by itself. Build the player first (the VH
#      play build, with DreamHouse in Build Settings); -Exe points at it.
# The DreamHouse scene must be built first (JamKit > Dream House > Rebuild Scene, or
# -executeMethod JamKit.DreamEscape.EditorTools.DreamHouseBuilder.RebuildFromCommandLine; -Rebuild does it here).
# Renders with the GPU: the stills in batch mode WITH graphics (no window); the runs in the player (a minimized window).
# The rebuild exits 3 when the mesh lint finds problems (the scene is still saved; JAMKIT_HOUSE_MESHLINT in its log). Run it through the session memory cap:
#   uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -NoProfile -ExecutionPolicy Bypass -File tools/house-capture.ps1
# -Only stills|runs picks one half; -Run all|hideandseek|escape picks the recorded run.
param([string]$Only = '', [string]$Run = 'all', [switch]$Rebuild, [string]$Exe = 'Builds/Play/VisitingHours/VisitingHours.exe')
. "$PSScriptRoot\unity-env.ps1"
Assert-ProjectUnlocked

$unity = Get-UnityEditorPath
New-Item -ItemType Directory -Force $OutDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

function Invoke-HouseUnity([string]$name, [string[]]$extra, [bool]$batch = $true) {
    $log = Join-Path $OutDir "house-$name.log"
    $unityArgs = @('-projectPath', "`"$RepoRoot`"", '-logFile', "`"$log`"") + $extra
    if ($batch) { $unityArgs = @('-batchmode') + $unityArgs }
    Write-Host "Unity ($name): $unity"
    $p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
    $null = $p.Handle
    $p.WaitForExit()
    Select-String -Path $log -Pattern 'JAMKIT_HOUSE|error CS|Exception' | Select-Object -Last 60 | ForEach-Object { Write-Host "  $($_.Line)" }
    if ($p.ExitCode -ne 0) { Write-Host "Unity exited with $($p.ExitCode); see $log" }
    return $p.ExitCode
}

$code = 0
if ($Rebuild) {
    $code = Invoke-HouseUnity 'rebuild' @('-executeMethod', 'JamKit.DreamEscape.EditorTools.DreamHouseBuilder.RebuildFromCommandLine')
    if ($code -eq 3) { Write-Host "The mesh lint found problems (JAMKIT_HOUSE_MESHLINT in out/house-rebuild.log); capturing anyway." }
    elseif ($code -ne 0) { exit $code }
}
if ($Only -ne 'runs') {
    $code = Invoke-HouseUnity 'stills' @('-executeMethod', 'JamKit.DreamEscape.EditorTools.DreamHouseCapture.StillsFromCommandLine')
    if ($code -ne 0) { exit $code }
}
if ($Only -ne 'stills') {
    $runs = Join-Path $RepoRoot "out\house-runs\$stamp"
    New-Item -ItemType Directory -Force $runs | Out-Null
    $player = if ([System.IO.Path]::IsPathRooted($Exe)) { $Exe } else { Join-Path $RepoRoot $Exe }
    if (-not (Test-Path $player)) { Write-Host "No player at $($player): build the VH play build first (or pass -Exe)."; exit 1 }
    $plog = Join-Path $runs 'player.log'
    $pargs = @('-house-record', "`"$runs`"", '-house-record-run', $Run, '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0', '-logFile', "`"$plog`"")
    Write-Host "Player (runs): $player"
    $p = Start-Process -FilePath $player -ArgumentList $pargs -PassThru -WindowStyle Minimized
    $null = $p.Handle
    $p.WaitForExit()
    Select-String -Path $plog -Pattern 'JAMKIT_HOUSE_RUN' | Select-Object -Last 40 | ForEach-Object { Write-Host "  $($_.Line)" }
    $code = $p.ExitCode
    Write-Host "Recorded runs: $runs (exit $code)"
}
exit $code
