# Night 2's house for the judge (docs/redesign/house.md "Captures"):
#   1. the stills (JamKit.DreamEscape.EditorTools.DreamHouseCapture.StillsFromCommandLine): every room at her eye height,
#      the hide-and-seek's moments, each escape beat, Chapel Row with Lena at the window; 1920x1080 PNGs and shots.txt
#      into out/house-captures/<time>/;
#   2. the recorded runs (DreamHouseCapture.RecordFromCommandLine: the scene plays itself on the autopilot, the
#      hide-and-seek and then the escape) with a 1920x1080 JPG every 0.5 s of game time and run.txt (what was
#      happening, by time) into out/house-runs/<time>/ (hide-and-seek/, escape/).
# The DreamHouse scene must be built first (JamKit > Dream House > Rebuild Scene, or
# -executeMethod JamKit.DreamEscape.EditorTools.DreamHouseBuilder.RebuildFromCommandLine; -Rebuild does it here).
# Renders with the GPU: the stills in batch mode WITH graphics; the runs play in the editor's window (play mode, like
# tools/record-attract.ps1: don't click into it; it quits by itself). Run it through the session memory cap:
#   uv run python ~/.local/bin/capped-run.py --session VibeJamKit -- powershell -NoProfile -ExecutionPolicy Bypass -File tools/house-capture.ps1
# -Only stills|runs picks one half; -Run all|hideandseek|escape picks the recorded run.
param([string]$Only = '', [string]$Run = 'all', [switch]$Rebuild)
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
    if ($code -ne 0) { exit $code }
}
if ($Only -ne 'runs') {
    $code = Invoke-HouseUnity 'stills' @('-executeMethod', 'JamKit.DreamEscape.EditorTools.DreamHouseCapture.StillsFromCommandLine')
    if ($code -ne 0) { exit $code }
}
if ($Only -ne 'stills') {
    $runs = Join-Path $RepoRoot "out\house-runs\$stamp"
    New-Item -ItemType Directory -Force $runs | Out-Null
    $code = Invoke-HouseUnity 'runs' @('-executeMethod', 'JamKit.DreamEscape.EditorTools.DreamHouseCapture.RecordFromCommandLine',
        '-houseRecord', "`"$runs`"", '-houseRecordRun', $Run) $false
    Write-Host "Recorded runs: $runs"
}
exit $code
