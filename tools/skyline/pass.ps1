# SkylinePass (docs/SKYLINE-DESIGN.md 9.4): four short Unity jobs, each holding the shared lock for one job, then an
# audio check without Unity. A failed stage doesn't stop the others, and a manifest.json is always written. Main session
# only. Run it UNCAPPED (GPU programs crash natively inside capped-run's Job Object: the graphics editor of 2026-10-13 died
# with "Failed to create the D3D11 device"); the runner caps only the batch stages itself:
#   powershell -NoProfile -File tools/skyline/pass.ps1
#   powershell -NoProfile -File tools/skyline/pass.ps1 -SelfTest     (no Unity: proves a hung stage is killed and unlocked)
# Stages A and C (batch editor jobs, no GPU) go through ~/.local/bin/capped-run.py (session skyline-repo, waits for 3 GB free).
# Stages B and D (the graphics editor, the built player) run directly, one at a time, once 3 GB of physical memory is free
# (waits up to 10 minutes, else the stage fails). The runner keeps the PC awake while it runs (this PC has only Modern Standby).
param([string]$Out = '', [switch]$SelfTest)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\unity-env.ps1"
. "$PSScriptRoot\unity-lock.ps1"

function Get-FreePhysicalGB {
    return [double](Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB      # the OS reports KB
}

# True once MinGB of physical memory is free; false after MaxWaitSec of polling every PollSec.
function Wait-FreePhysicalMemory {
    param([double]$MinGB, [int]$MaxWaitSec = 600, [int]$PollSec = 30)
    $deadline = (Get-Date).AddSeconds($MaxWaitSec)
    $first = $true
    while ($true) {
        $free = Get-FreePhysicalGB
        if ($free -ge $MinGB) { return $true }
        if ($first) { Write-Host ("SkylinePass: waiting for {0:N1} GB of free memory ({1:N1} GB free now)" -f $MinGB, $free); $first = $false }
        if ((Get-Date) -ge $deadline) { return $false }
        Start-Sleep -Seconds ([Math]::Max(1, [Math]::Min($PollSec, [int][Math]::Ceiling(($deadline - (Get-Date)).TotalSeconds))))
    }
}

# What to start for a stage. A capped stage goes through capped-run (the machine's per-session memory budget, enforced by a
# Windows Job Object; --no-keep-awake because this runner keeps the PC awake itself; one retry for a job killed by lack of
# memory), with the Unity command after "--". Arguments that need quotes are already quoted by the caller.
function Get-StageLaunch {
    param([string]$Exe, [string[]]$StageArgs, [switch]$Capped)
    if (-not $Capped) { return [pscustomobject]@{ File = $Exe; Args = @($StageArgs) } }
    $cappedRun = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.local\bin\capped-run.py'
    $uv = Get-Command uv -ErrorAction SilentlyContinue
    $uvPath = if ($uv) { $uv.Source } else { 'uv' }
    $launch = @('run', 'python', "`"$cappedRun`"", '--session', 'skyline-repo', '--need', '3', '--retries', '1', '--no-keep-awake', '--', "`"$Exe`"") + @($StageArgs)
    return [pscustomobject]@{ File = $uvPath; Args = $launch }
}

# Stages B and D run with a normal window: a minimized player or editor can throttle or skip presenting, which would
# invalidate the recording and the frame-time measurement. A and C (batch work) stay minimized.
# -Capped: launched through capped-run (batch stages). -MinFreeGB: wait for that much free physical memory before starting
# (the uncapped GPU stages). The timeout and kill act on the whole process tree, so a killed capped-run takes Unity with it.
function Invoke-Stage {
    param([string]$Name, [string]$Exe, [string[]]$StageArgs, [int]$TimeoutSec, [string]$Log, [switch]$Locked,
          [string]$WindowStyle = 'Minimized', [switch]$Capped, [double]$MinFreeGB = 0, [int]$MemWaitSec = 600, [int]$MemPollSec = 30)
    $started = Get-Date
    $ok = $false; $note = ''
    try {
        if ($Locked) { Enter-UnityLock -Who "skyline-repo:$Name" }
        if ($MinFreeGB -gt 0 -and -not (Wait-FreePhysicalMemory -MinGB $MinFreeGB -MaxWaitSec $MemWaitSec -PollSec $MemPollSec)) {
            throw ("not started: free physical memory stayed under {0:N1} GB for {1} s ({2:N1} GB free)" -f $MinFreeGB, $MemWaitSec, (Get-FreePhysicalGB))
        }
        $launch = Get-StageLaunch -Exe $Exe -StageArgs $StageArgs -Capped:$Capped
        $p = Start-Process -FilePath $launch.File -ArgumentList $launch.Args -PassThru -WindowStyle $WindowStyle
        $null = $p.Handle
        if (-not $p.WaitForExit($TimeoutSec * 1000)) {
            $note = "timed out after $TimeoutSec s; killed"
            & taskkill /PID $p.Id /T /F 2>$null | Out-Null
            # wait for the process to be really gone before the lock is released, or the next job meets a dying editor
            if (-not $p.WaitForExit(60000)) { $note += '; still running 60 s after the kill' }
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

# Asks Windows to keep the system and the display on while the runner works (Modern Standby freezes everything when the
# screen turns off). Thread state: the runner's main thread lives for the whole pass. Never throws.
function Set-KeepAwake {
    param([bool]$On)
    try {
        if (-not ('SkylinePassAwake' -as [type])) {
            Add-Type -Namespace Win32 -Name SkylinePassAwake -MemberDefinition '[System.Runtime.InteropServices.DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);'
        }
        $flags = if ($On) { [uint32]2147483651 } else { [uint32]2147483648 }     # ES_CONTINUOUS (| ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED)
        $null = [Win32.SkylinePassAwake]::SetThreadExecutionState($flags)
    } catch { Write-Host "SkylinePass: could not change keep-awake: $($_.Exception.Message)" }
}

function Test-IsNumber { param($Value) return ($Value -is [double]) -or ($Value -is [int]) -or ($Value -is [long]) -or ($Value -is [decimal]) }

# Folds the probe's perf.json into stage D's entry (mutates $Stage). Never throws: a missing, truncated or incomplete file
# fails the stage with a note, and the stage's own failure note ("timed out...", "exit code N") is kept in front of it.
function Update-PerfStage {
    param($Stage, [string]$PerfPath)
    $perfNote = ''; $perfOk = $false
    if (Test-Path -LiteralPath $PerfPath) {
        try {
            $p = Get-Content -LiteralPath $PerfPath -Raw | ConvertFrom-Json
            $problems = @()
            if (-not (Test-IsNumber $p.frames) -or $p.frames -le 0) { $problems += 'frames missing or not above 0' }
            if (-not (Test-IsNumber $p.avgMs)) { $problems += 'avgMs missing or not a number' }
            if (-not (Test-IsNumber $p.onePercentLowMs) -or $p.onePercentLowMs -le 0) { $problems += 'onePercentLowMs missing or not above 0' }
            elseif ($p.onePercentLowMs -gt 16.7) { $problems += '1% low over 16.7 ms' }
            if ($p.resolution -ne '1920x1080') { $problems += "resolution is '$($p.resolution)', not 1920x1080" }
            $perfNote = "avg $($p.avgMs) ms, 1% low $($p.onePercentLowMs) ms (blocker over 16.7), $($p.resolution)"
            if ($problems.Count -gt 0) { $perfNote += '; ' + ($problems -join '; ') } else { $perfOk = $true }
        } catch {
            $perfNote = "perf.json unreadable: $($_.Exception.Message)"
        }
    } else {
        $perfNote = 'no perf.json written'
    }
    if ($Stage.note) { $Stage.note = "$($Stage.note); $perfNote" } else { $Stage.note = $perfNote }
    $Stage.ok = [bool]($Stage.ok -and $perfOk)
}

if ($SelfTest) {
    $script:LockDir = Join-Path ([IO.Path]::GetTempPath()) "skyline-pass-selftest-$PID"
    $r = Invoke-Stage -Name 'hang' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'Start-Sleep 600') -TimeoutSec 5 -Log '' -Locked
    if ($r.ok -or $r.note -notlike 'timed out*') { throw "self-test: hung stage not reported ($($r | ConvertTo-Json -Compress))" }
    if (Test-Path $script:LockDir) { throw 'self-test: lock left behind after a timeout' }
    $r2 = Invoke-Stage -Name 'ok' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'exit 0') -TimeoutSec 30 -Log '' -Locked -WindowStyle Normal
    if (-not $r2.ok) { throw 'self-test: a passing stage was reported as failed' }
    $r3 = Invoke-Stage -Name 'fail' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'exit 3') -TimeoutSec 30 -Log '' -Locked
    if ($r3.ok -or $r3.note -ne 'exit code 3') { throw 'self-test: a failing stage was not reported with its exit code' }

    # the memory wait: an impossible requirement fails the stage within the wait, without starting anything, and releases the lock
    if (Wait-FreePhysicalMemory -MinGB 1000000 -MaxWaitSec 2 -PollSec 1) { throw 'self-test: an impossible memory requirement was reported as met' }
    if (-not (Wait-FreePhysicalMemory -MinGB 0.001 -MaxWaitSec 2 -PollSec 1)) { throw 'self-test: a trivial memory requirement was reported as unmet' }
    $r4 = Invoke-Stage -Name 'nomem' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'exit 0') -TimeoutSec 30 -Log '' -Locked -MinFreeGB 1000000 -MemWaitSec 2 -MemPollSec 1
    if ($r4.ok -or $r4.note -notlike 'not started: free physical memory stayed under*') { throw "self-test: a stage without memory was not failed ($($r4 | ConvertTo-Json -Compress))" }
    if (Test-Path $script:LockDir) { throw 'self-test: lock left behind after a memory failure' }
    # the capped launch: capped-run in front, the session, the memory need, the command after "--", quoting kept
    $l = Get-StageLaunch -Exe 'C:\Program Files\Unity\Unity.exe' -StageArgs @('-batchmode', '-projectPath', '"C:\a b\proj"') -Capped
    $joined = $l.Args -join ' '
    if ($joined -notlike 'run python "*capped-run.py" --session skyline-repo --need 3 --retries 1 --no-keep-awake -- "C:\Program Files\Unity\Unity.exe" -batchmode -projectPath "C:\a b\proj"') { throw "self-test: capped launch is wrong: $joined" }
    $l2 = Get-StageLaunch -Exe 'C:\x.exe' -StageArgs @('-a')
    if ($l2.File -ne 'C:\x.exe' -or ($l2.Args -join ' ') -ne '-a') { throw 'self-test: an uncapped launch must start the program itself' }

    # stage D's perf.json handling: every bad file fails the stage with a note and none of them throws
    $dir = Join-Path ([IO.Path]::GetTempPath()) "skyline-pass-perf-selftest-$PID"
    New-Item -ItemType Directory -Force $dir | Out-Null
    try {
        $good = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":11.2,"worstMs":15.0,"resolution":"1920x1080"}'
        $cases = @(
            @{ n = 'good'; json = $good; ok = $true; like = '*1% low 11.2*' },
            @{ n = 'truncated'; json = '{"frames":1200,"avgMs":8.1,"onePerc'; ok = $false; like = '*unreadable*' },
            @{ n = 'garbage'; json = 'not json at all'; ok = $false; like = '*unreadable*' },
            @{ n = 'missing low'; json = '{"frames":1200,"avgMs":8.1,"resolution":"1920x1080"}'; ok = $false; like = '*onePercentLowMs missing*' },
            @{ n = 'zero low'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":0,"resolution":"1920x1080"}'; ok = $false; like = '*onePercentLowMs missing or not above 0*' },
            @{ n = 'string low'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":"fast","resolution":"1920x1080"}'; ok = $false; like = '*onePercentLowMs missing*' },
            @{ n = 'no frames'; json = '{"frames":0,"avgMs":8.1,"onePercentLowMs":11.2,"resolution":"1920x1080"}'; ok = $false; like = '*frames missing*' },
            @{ n = 'over budget'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":20.5,"resolution":"1920x1080"}'; ok = $false; like = '*over 16.7*' },
            @{ n = 'wrong resolution'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":11.2,"resolution":"1280x720"}'; ok = $false; like = "*resolution is '1280x720'*" }
        )
        foreach ($c in $cases) {
            $f = Join-Path $dir 'perf.json'
            Set-Content -LiteralPath $f -Value $c.json -Encoding utf8
            $s = [pscustomobject]@{ name = 'D perf'; ok = $true; seconds = 1; log = ''; note = '' }
            Update-PerfStage -Stage $s -PerfPath $f
            if ($s.ok -ne $c.ok -or $s.note -notlike $c.like) { throw "self-test: perf case '$($c.n)' gave ok=$($s.ok) note='$($s.note)'" }
        }
        # no perf.json: fails an otherwise passing stage; and a failed stage keeps its own note in front
        Remove-Item -LiteralPath (Join-Path $dir 'perf.json')
        $s = [pscustomobject]@{ name = 'D perf'; ok = $true; seconds = 1; log = ''; note = '' }
        Update-PerfStage -Stage $s -PerfPath (Join-Path $dir 'perf.json')
        if ($s.ok -or $s.note -ne 'no perf.json written') { throw "self-test: missing perf.json not failed: '$($s.note)'" }
        Set-Content -LiteralPath (Join-Path $dir 'perf.json') -Value $good -Encoding utf8
        $s = [pscustomobject]@{ name = 'D perf'; ok = $false; seconds = 1; log = ''; note = 'timed out after 180 s; killed' }
        Update-PerfStage -Stage $s -PerfPath (Join-Path $dir 'perf.json')
        if ($s.ok -or $s.note -notlike 'timed out after 180 s; killed; avg 8.1*') { throw "self-test: a failed stage's own note was lost: '$($s.note)'" }
    } finally {
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host 'pass.ps1 self-test passed.'
    exit 0
}

# From here on a manifest.json is always written (the finally block), even when preflight or a stage's bookkeeping throws.
$stages = @()
$allOk = $false
if (-not $Out) { $Out = Join-Path $RepoRoot ("out\skyline\" + (Get-Date -Format 'yyyyMMdd-HHmm')) }
Set-KeepAwake $true
try {
    $preflightOk = $true
    try {
        New-Item -ItemType Directory -Force $Out | Out-Null
        Assert-ProjectUnlocked
        $unity = Get-UnityEditorPath
    } catch {
        $preflightOk = $false
        $stages += [pscustomobject]@{ name = 'preflight'; ok = $false; seconds = 0; log = ''; note = $_.Exception.Message }
    }
    $proj = "`"$RepoRoot`""

    if ($preflightOk) {
        # A: editor batch job (tests, scenes)
        $log = Join-Path $Out 'stage-a-editor-batch.log'
        # (batch mode, no GPU: through the memory cap)
        $stages += Invoke-Stage -Name 'A editor batch' -Exe $unity -Locked -Capped -TimeoutSec 1800 -Log $log -StageArgs @(
            '-batchmode', '-projectPath', $proj, '-logFile', "`"$log`"",
            '-executeMethod', 'JamKit.SkylineRepo.EditorTools.SkylinePassBatch.RunFromCommandLine', '-skylineOut', "`"$Out`"")

        # B: graphics editor recording (audio plays only outside batch mode; a normal window so it keeps presenting).
        # Uncapped (the D3D11 device needs the memory a Job Object cap refuses); -silent-crashes: a crash exits, no dialog.
        $log = Join-Path $Out 'stage-b-capture.log'
        $stages += Invoke-Stage -Name 'B recording' -Exe $unity -Locked -MinFreeGB 3 -TimeoutSec 900 -Log $log -WindowStyle Normal -StageArgs @(
            '-projectPath', $proj, '-logFile', "`"$log`"", '-silent-crashes',
            '-executeMethod', 'JamKit.SkylineRepo.EditorTools.Recording.SkylineCapture.RunFromCommandLine', '-skylineOut', "`"$Out`"")

        # C: Windows build of Skyline's scenes (batch mode: through the memory cap)
        $log = Join-Path $Out 'stage-c-build.log'
        $stages += Invoke-Stage -Name 'C build' -Exe $unity -Locked -Capped -TimeoutSec 1800 -Log $log -StageArgs @(
            '-batchmode', '-quit', '-projectPath', $proj, '-logFile', "`"$log`"",
            '-executeMethod', 'JamKit.SkylineRepo.EditorTools.SkylineBuild.BuildFromCommandLine', '-skylineOut', "`"$Out`"")

        # D: the built player's frame-time probe (windowed 1080p, normal window, not batch mode; uncapped like B)
        $exe = Join-Path $Out 'build\SkylineRepo.exe'
        if (Test-Path $exe) {
            $log = Join-Path $Out 'stage-d-perf.log'
            $d = Invoke-Stage -Name 'D perf' -Exe $exe -Locked -MinFreeGB 3 -TimeoutSec 180 -Log $log -WindowStyle Normal -StageArgs @(
                '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080', '-logFile', "`"$log`"", '-silent-crashes',
                '-skyline-perf', "`"$Out`"")
            Update-PerfStage -Stage $d -PerfPath (Join-Path $Out 'perf.json')
            $stages += $d
        } else {
            $stages += [pscustomobject]@{ name = 'D perf'; ok = $false; seconds = 0; log = ''; note = 'skipped: no build' }
        }
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
            $eok = (-not $m.error) -and (-not $m.silent) -and ($m.centroid_hz -gt 300) -and ($m.centroid_hz -lt 700)
            if ($m.error) { $enote = "the ears reported an error: $($m.error) $($m.detail)" }
            else { $enote = "silent=$($m.silent) centroid=$([int]$m.centroid_hz) Hz peak=$($m.sample_peak_dbfs) dBFS" }
        } catch { $ErrorActionPreference = 'Stop'; $enote = "could not read the ears' output: $($_.Exception.Message)" }
    } else { $enote = 'skipped: no firing range recording' }
    $stages += [pscustomobject]@{ name = 'E audio check'; ok = $eok; seconds = [int]((Get-Date) - $started).TotalSeconds; log = ''; note = $enote }
} catch {
    $stages += [pscustomobject]@{ name = 'runner'; ok = $false; seconds = 0; log = ''; note = "unexpected: $($_.Exception.Message)" }
} finally {
    Set-KeepAwake $false
    $allOk = (@($stages).Count -gt 0) -and -not (@($stages) | Where-Object { -not $_.ok })
    try {
        New-Item -ItemType Directory -Force $Out | Out-Null
        [pscustomobject]@{ ok = $allOk; out = $Out; stages = @($stages) } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Out 'manifest.json') -Encoding utf8
    } catch {
        $allOk = $false
        Write-Host "SkylinePass: could not write manifest.json in '$Out': $($_.Exception.Message)"
    }
    @($stages) | Format-Table name, ok, seconds, note -AutoSize | Out-String -Width 220 | Write-Host
    Write-Host "SkylinePass $(if ($allOk) { 'OK' } else { 'FAILED' }): $Out"
}
if ($allOk) { exit 0 } else { exit 1 }
