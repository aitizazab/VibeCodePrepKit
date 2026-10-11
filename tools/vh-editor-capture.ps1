# The opening editor's capture tour (docs/OPENING-EDITOR.md "Checking it"): runs a Windows build with
# -edit-opening -opening-editor-tour <folder>. The build opens the editor, works it with a virtual mouse and keyboard
# (the machine's own are switched off while it runs, about a minute), saves a screenshot after each step and a
# captions.txt (each step, the status line, PASS/FAIL checks), undoes everything and quits. Nothing is saved.
# The window shows on screen while it runs (full screen if the game's settings say so).
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/vh-editor-capture.ps1 [-Exe <path to VibeJamKit.exe>] [-Out <folder>]
# Defaults: the Visiting Hours play build (Builds/Play/VisitingHours) and out/opening-editor-ux/<time>/.
param(
    [string]$Exe = '',
    [string]$Out = '',
    [int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-env.ps1"

if (-not $Exe) {
    foreach ($dir in @((Join-Path $RepoRoot 'Builds\Play\VisitingHours'), (Join-Path $RepoRoot 'Builds\Windows'))) {
        $found = Get-ChildItem $dir -Filter *.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notlike 'UnityCrashHandler*' } | Select-Object -First 1
        if ($found) { $Exe = $found.FullName; break }
    }
}
if (-not $Exe -or -not (Test-Path $Exe)) { Write-Host "capture: no build found (pass -Exe)."; exit 1 }
if (-not $Out) { $Out = Join-Path $OutDir ("opening-editor-ux\" + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force $Out | Out-Null
$log = Join-Path $Out 'player.log'

Write-Host "capture: $Exe"
Write-Host "capture: into $Out"
$p = Start-Process -FilePath $Exe -ArgumentList @('-edit-opening', '-opening-editor-tour', "`"$Out`"", '-logFile', "`"$log`"") -PassThru
$null = $p.Handle
if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Write-Host "capture: the build didn't finish in $TimeoutSeconds s (killed). See $log"
}
$captions = Join-Path $Out 'captions.txt'
if (Test-Path $captions) { Get-Content $captions | ForEach-Object { Write-Host "  $_" } }
else { Write-Host "capture: no captions.txt. See $log" }
if (Test-Path $log) {
    Select-String -Path $log -Pattern 'Exception|NullReference|MissingReference' | Select-Object -First 8 | ForEach-Object { Write-Host "  LOG $($_.Line)" }
}
$pngs = @(Get-ChildItem $Out -Filter *.png -ErrorAction SilentlyContinue)
Write-Host "capture: $($pngs.Count) pictures in $Out"
if ($pngs.Count -eq 0) { exit 1 }
if (Test-Path $captions) { if (Select-String -Path $captions -Pattern 'CHECK FAIL' -Quiet) { exit 2 } }
exit 0
