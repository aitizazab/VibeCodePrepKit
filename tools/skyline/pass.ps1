# SkylinePass (docs/SKYLINE-DESIGN.md 9.4): four short Unity jobs, each holding the shared lock for one job, then an
# audio check without Unity. A failed stage doesn't stop the others. Main session only; run it through the memory cap:
#   uv run python ~/.local/bin/capped-run.py --session skyline-repo --retries 1 -- powershell -NoProfile -File tools/skyline/pass.ps1
#   powershell -NoProfile -File tools/skyline/pass.ps1 -SelfTest     (no Unity: proves a hung stage is killed and unlocked)
param([string]$Out = '', [switch]$SelfTest)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\unity-env.ps1"
. "$PSScriptRoot\unity-lock.ps1"

function Invoke-Stage {
    param([string]$Name, [string]$Exe, [string[]]$StageArgs, [int]$TimeoutSec, [string]$Log, [switch]$Locked)
    $started = Get-Date
    $ok = $false; $note = ''
    try {
        if ($Locked) { Enter-UnityLock -Who "skyline-repo:$Name" }
        $p = Start-Process -FilePath $Exe -ArgumentList $StageArgs -PassThru -WindowStyle Minimized
        $null = $p.Handle
        if (-not $p.WaitForExit($TimeoutSec * 1000)) {
            $note = "timed out after $TimeoutSec s; killed"
            & taskkill /PID $p.Id /T /F 2>$null | Out-Null
        } else {
            $ok = ($p.ExitCode -eq 0)
            if (-not $ok) { $note = "exit code $($p.ExitCode)" }
        }
    } catch {
        $note = $_.Exception.Message
    } finally {
        if ($Locked) { Exit-UnityLock }
    }
    [pscustomobject]@{ name = $Name; ok = $ok; seconds = [int]((Get-Date) - $started).TotalSeconds; log = $Log; note = $note }
}

if ($SelfTest) {
    $script:LockDir = Join-Path ([IO.Path]::GetTempPath()) "skyline-pass-selftest-$PID"
    $r = Invoke-Stage -Name 'hang' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'Start-Sleep 600') -TimeoutSec 5 -Log '' -Locked
    if ($r.ok -or $r.note -notlike 'timed out*') { throw "self-test: hung stage not reported ($($r | ConvertTo-Json -Compress))" }
    if (Test-Path $script:LockDir) { throw 'self-test: lock left behind after a timeout' }
    $r2 = Invoke-Stage -Name 'ok' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'exit 0') -TimeoutSec 30 -Log '' -Locked
    if (-not $r2.ok) { throw 'self-test: a passing stage was reported as failed' }
    $r3 = Invoke-Stage -Name 'fail' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'exit 3') -TimeoutSec 30 -Log '' -Locked
    if ($r3.ok -or $r3.note -ne 'exit code 3') { throw 'self-test: a failing stage was not reported with its exit code' }
    Write-Host 'pass.ps1 self-test passed.'
    exit 0
}

if (-not $Out) { $Out = Join-Path $RepoRoot ("out\skyline\" + (Get-Date -Format 'yyyyMMdd-HHmm')) }
New-Item -ItemType Directory -Force $Out | Out-Null
Assert-ProjectUnlocked
$unity = Get-UnityEditorPath
$proj = "`"$RepoRoot`""
$stages = @()

# A: editor batch job (tests, scenes)
$log = Join-Path $Out 'stage-a-editor-batch.log'
$stages += Invoke-Stage -Name 'A editor batch' -Exe $unity -Locked -TimeoutSec 1800 -Log $log -StageArgs @(
    '-batchmode', '-projectPath', $proj, '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.SkylineRepo.EditorTools.SkylinePassBatch.RunFromCommandLine', '-skylineOut', "`"$Out`"")

# B: graphics editor recording (audio plays only outside batch mode)
$log = Join-Path $Out 'stage-b-capture.log'
$stages += Invoke-Stage -Name 'B recording' -Exe $unity -Locked -TimeoutSec 900 -Log $log -StageArgs @(
    '-projectPath', $proj, '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.SkylineRepo.EditorTools.Recording.SkylineCapture.RunFromCommandLine', '-skylineOut', "`"$Out`"")

# C: Windows build of Skyline's scenes
$log = Join-Path $Out 'stage-c-build.log'
$stages += Invoke-Stage -Name 'C build' -Exe $unity -Locked -TimeoutSec 1800 -Log $log -StageArgs @(
    '-batchmode', '-quit', '-projectPath', $proj, '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.SkylineRepo.EditorTools.SkylineBuild.BuildFromCommandLine', '-skylineOut', "`"$Out`"")

# D: the built player's frame-time probe (windowed 1080p, not batch mode)
$exe = Join-Path $Out 'build\SkylineRepo.exe'
if (Test-Path $exe) {
    $log = Join-Path $Out 'stage-d-perf.log'
    $stages += Invoke-Stage -Name 'D perf' -Exe $exe -Locked -TimeoutSec 180 -Log $log -StageArgs @(
        '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080', '-logFile', "`"$log`"",
        '-skyline-perf', "`"$Out`"")
    $perf = Join-Path $Out 'perf.json'
    $last = $stages[-1]
    if (Test-Path $perf) {
        $p = Get-Content $perf -Raw | ConvertFrom-Json
        $last.note = "avg $($p.avgMs) ms, 1% low $($p.onePercentLowMs) ms (blocker over 16.7)"
        if ($p.onePercentLowMs -gt 16.7) { $last.ok = $false }
    } elseif ($last.ok) { $last.ok = $false; $last.note = 'no perf.json written' }
} else {
    $stages += [pscustomobject]@{ name = 'D perf'; ok = $false; seconds = 0; log = ''; note = 'skipped: no build' }
}

# E: audio check without Unity: the firing range recording must contain the 440 Hz test tone
$rec = Join-Path $Out 'rec-SkylineFiringRange.mp4'
$started = Get-Date
$eok = $false; $enote = ''
if (Test-Path $rec) {
    try {
        $ears = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.claude\skills\ears'
        $earsErr = Join-Path $Out 'audio-firing-range.stderr.txt'
        # stdout (the JSON) only; uv's progress goes to stderr, which under $ErrorActionPreference = 'Stop' would otherwise
        # surface as a terminating NativeCommandError in Windows PowerShell 5.1
        $ErrorActionPreference = 'Continue'
        $json = & uv run --project $ears python (Join-Path $ears 'listen.py') measure $rec --png (Join-Path $Out 'audio-firing-range.png') 2>$earsErr
        $ErrorActionPreference = 'Stop'
        $jsonText = ($json | Out-String)
        Set-Content -Path (Join-Path $Out 'audio-firing-range.json') -Value $jsonText -Encoding utf8
        $m = $jsonText.Substring($jsonText.IndexOf('{')) | ConvertFrom-Json
        $eok = (-not $m.silent) -and ($m.centroid_hz -gt 300) -and ($m.centroid_hz -lt 700)
        $enote = "silent=$($m.silent) centroid=$([int]$m.centroid_hz) Hz peak=$($m.sample_peak_dbfs) dBFS"
    } catch { $ErrorActionPreference = 'Stop'; $enote = "could not read the ears' output: $($_.Exception.Message)" }
} else { $enote = 'skipped: no firing range recording' }
$stages += [pscustomobject]@{ name = 'E audio check'; ok = $eok; seconds = [int]((Get-Date) - $started).TotalSeconds; log = ''; note = $enote }

$allOk = -not ($stages | Where-Object { -not $_.ok })
[pscustomobject]@{ ok = $allOk; out = $Out; stages = $stages } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Out 'manifest.json') -Encoding utf8
$stages | Format-Table name, ok, seconds, note -AutoSize | Out-String | Write-Host
Write-Host "SkylinePass $(if ($allOk) { 'OK' } else { 'FAILED' }): $Out"
if ($allOk) { exit 0 } else { exit 1 }
