# Build smoke test: runs the Windows build headless with -jamSmoke, which plays itself through
# boot, title, Play, the first gameplay scene, pause, a debug win, victory, credits and back to the
# title, then quits. Exit code 0 and a JAMKIT_SMOKE_PASS line = pass; any logged error = fail.
#   powershell -File tools/smoke.ps1 [-Seconds 20] [-Exe Builds/Windows/VibeJamKit.exe]
param(
    [int]$Seconds = 20,
    [string]$Exe = ''
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-env.ps1"

if (-not $Exe) {
    $dir = Join-Path $RepoRoot 'Builds\Windows'
    $found = Get-ChildItem $dir -Filter *.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike 'UnityCrashHandler*' } | Select-Object -First 1
    if (-not $found) { Write-Host "smoke: no build in $dir. Run tools/build.ps1 -Target Windows first."; exit 1 }
    $Exe = $found.FullName
}
New-Item -ItemType Directory -Force $OutDir | Out-Null
$log = Join-Path $OutDir 'smoke.log'
if (Test-Path $log) { Remove-Item $log }

Write-Host "smoke: $Exe -jamSmoke $Seconds"
$started = Get-Date
$p = Start-Process -FilePath $Exe -ArgumentList @('-batchmode', '-nographics', '-jamSmoke', "$Seconds", '-logFile', "`"$log`"") -PassThru -NoNewWindow
# Windows PowerShell only fills ExitCode later if the handle was read while the process ran.
$null = $p.Handle
# The player has its own watchdog; this one only catches a hang (it never quits).
if (-not $p.WaitForExit(($Seconds + 90) * 1000)) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Write-Host "smoke: FAIL (the player hung; killed after $($Seconds + 90) s). Log: $log"
    exit 1
}
$code = $p.ExitCode
$elapsed = ((Get-Date) - $started).TotalSeconds

if (Test-Path $log) {
    Select-String -Path $log -Pattern 'JAMKIT_SMOKE' | ForEach-Object { Write-Host "  $($_.Line)" }
    $errors = Select-String -Path $log -Pattern 'Exception|\(Filename: .* Line: \d+\)' -Context 1, 0 |
        Where-Object { $_.Context.PreContext -match 'Error|Exception' } | Select-Object -First 5
    foreach ($e in $errors) { Write-Host "  ERR $($e.Context.PreContext)" }
}
$pass = ($code -eq 0) -and (Test-Path $log) -and (Select-String -Path $log -Pattern 'JAMKIT_SMOKE_PASS' -Quiet)
if ($pass) { Write-Host ("smoke: PASS in {0:n0} s (exit {1})" -f $elapsed, $code); exit 0 }
Write-Host ("smoke: FAIL (exit {0}, {1:n0} s). Log: {2}" -f $code, $elapsed, $log)
exit 1
