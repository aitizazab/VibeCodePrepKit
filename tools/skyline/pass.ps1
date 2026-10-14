# SkylinePass (docs/SKYLINE-DESIGN.md 9.4): four short Unity jobs, each holding the shared lock for one job, then an
# audio check without Unity. A failed stage doesn't stop the others, and a manifest.json is always written. Main session
# only. Run it UNCAPPED (GPU programs crash natively inside capped-run's Job Object: the graphics editor of 2026-10-13 died
# with "Failed to create the D3D11 device"); the runner caps only the batch stages itself:
#   powershell -NoProfile -File tools/skyline/pass.ps1
#   powershell -NoProfile -File tools/skyline/pass.ps1 -SelfTest     (no Unity: proves a hung stage is killed and unlocked)
# Stages A and C (batch editor jobs, no GPU) go through ~/.local/bin/capped-run.py (session skyline-repo, waits for 3 GB free).
# Stages B and D (the graphics editor, the built player) run directly, one at a time, once 3 GB of physical memory is free
# (waits up to 10 minutes, else the stage fails). The runner keeps the PC awake while it runs (this PC has only Modern Standby).
# If stage A shows the project doesn't compile (no editor-batch.json, an error in the job, or "error CS" in its log), B, C and
# D are skipped with a note. Exit code: 0 whenever manifest.json was written (its "ok" field carries pass or fail), non-zero
# only when the runner itself crashed before writing one.
# Stages B and D are muted on the speakers (Windows per-app session volume, tools/skyline/mute-app.ps1; the recording is made inside
# Unity, so it keeps its sound); the volume is put back when the stage's result file appears. If a pass dies mid-stage and Unity stays
# silent: powershell -NoProfile -File tools/skyline/mute-app.ps1 -RestoreName Unity
# -NoWindows runs only the batch stages (A, C, E): B and D open normal windows that take focus, so they wait until the
# creator is away from the PC (the creator's rule, 2026-10-12).
# -CapShare 0.45 raises the memory cap's share of RAM for the capped stages (the build's shader compiler ran out of memory at
# 0.30 on 2026-10-14 with the arena's new materials: "out of memory while parsing", 290 errors).
# -PerfArgs '-skyline-no-ink' adds arguments to the stage D player (an A/B run: the ink passes switched off, to price them; the
# probe writes "inkOff": true). Use it with -Only D -Out <an existing pass folder>, and copy perf.json away first (D sweeps it).
# -CaptureArgs '-skyline-ink-style halftone' adds arguments to the stage B graphics editor (the stills): the ink pass's optional
# stylization (docs/SKYLINE-STYLE.md, "Optional stylization"), e.g. one run per option into its own folder with -Only B -Out <folder>.
# -Only <letters> -Out <folder> runs only those stages into an existing pass folder (e.g. -Only A, then -Only B, -Only CD,
# -Only E), sweeping only their own old results; its manifest is manifest-<letters>.json. For a machine so short of memory
# that a whole pass can't finish in one sitting: each Unity stage then runs as its own short command.
param([string]$Out = '', [switch]$SelfTest, [switch]$NoWindows, [string]$Only = '', [double]$CapShare = 0, [string]$PerfArgs = '', [string]$CaptureArgs = '')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\unity-env.ps1"
. "$PSScriptRoot\unity-lock.ps1"
. "$PSScriptRoot\mute-app.ps1"

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
    $share = if ($CapShare -gt 0) { @('--share', [string]$CapShare) } else { @() }
    $launch = @('run', 'python', "`"$cappedRun`"", '--session', 'skyline-repo') + $share + @('--need', '3', '--retries', '1', '--no-keep-awake', '--', "`"$Exe`"") + @($StageArgs)
    return [pscustomobject]@{ File = $uvPath; Args = $launch }
}

# Stages B and D run with a normal window: a minimized player or editor can throttle or skip presenting, which would
# invalidate the recording and the frame-time measurement. A and C (batch work) stay minimized.
# -Capped: launched through capped-run (batch stages). -MinFreeGB: wait for that much free physical memory before starting
# (the uncapped GPU stages). The timeout and kill act on the whole process tree, so a killed capped-run takes Unity with it.
# -MuteAudio (stages B and D, which play sound through the speakers while the creator works): the process's Windows
# per-app audio session volume is held at zero (tools/skyline/mute-app.ps1) while it runs; the recording is made inside Unity,
# so it keeps its sound. -DoneFile: when that result file appears the stage's work is over, and the volume is put back at
# once (Windows may remember an app's volume by exe path); it is also put back before a timed-out stage is killed.
function Wait-StageProcess {
    param($Process, [int]$TimeoutSec, [switch]$MuteAudio, [string]$DoneFile = '')
    if (-not $MuteAudio) { return $Process.WaitForExit($TimeoutSec * 1000) }
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $ids = [uint32[]]@([uint32]$Process.Id); $nextTree = Get-Date; $restored = $false
    while (-not $Process.WaitForExit(200)) {
        if ((Get-Date) -ge $deadline) {
            if (-not $restored) { $null = Set-AppAudioMuted -ProcessIds $ids -Muted $false }
            return $false
        }
        if ($restored) { continue }
        if ($DoneFile -and (Test-Path -LiteralPath $DoneFile)) {
            $null = Set-AppAudioMuted -ProcessIds $ids -Muted $false
            $restored = $true
            continue
        }
        if ((Get-Date) -ge $nextTree) { $ids = Get-ProcessTreeIds -RootId $Process.Id; $nextTree = (Get-Date).AddSeconds(2) }
        $null = Set-AppAudioMuted -ProcessIds $ids -Muted $true
    }
    return $true
}

function Invoke-Stage {
    param([string]$Name, [string]$Exe, [string[]]$StageArgs, [int]$TimeoutSec, [string]$Log, [switch]$Locked,
          [string]$WindowStyle = 'Minimized', [switch]$Capped, [double]$MinFreeGB = 0, [int]$MemWaitSec = 600, [int]$MemPollSec = 30,
          [switch]$CheckProject, [switch]$MuteAudio, [string]$DoneFile = '')
    $started = Get-Date
    $ok = $false; $note = ''
    try {
        if ($Locked) { Enter-UnityLock -Who "skyline-repo:$Name" }
        # -CheckProject (the editor stages): another editor may have opened the project while this stage waited for the lock;
        # that is this stage's failure, not a runner crash
        if ($CheckProject) { Assert-ProjectUnlocked }
        if ($MinFreeGB -gt 0 -and -not (Wait-FreePhysicalMemory -MinGB $MinFreeGB -MaxWaitSec $MemWaitSec -PollSec $MemPollSec)) {
            throw ("not started: free physical memory stayed under {0:N1} GB for {1} s ({2:N1} GB free)" -f $MinFreeGB, $MemWaitSec, (Get-FreePhysicalGB))
        }
        $launch = Get-StageLaunch -Exe $Exe -StageArgs $StageArgs -Capped:$Capped
        $p = Start-Process -FilePath $launch.File -ArgumentList $launch.Args -PassThru -WindowStyle $WindowStyle
        $null = $p.Handle
        if (-not (Wait-StageProcess -Process $p -TimeoutSec $TimeoutSec -MuteAudio:$MuteAudio -DoneFile $DoneFile)) {
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

function Add-StageNote {
    param($Stage, [string]$Text)
    if ($Text) { if ($Stage.note) { $Stage.note = "$($Stage.note); $Text" } else { $Stage.note = $Text } }
}

function Limit-Text {
    param([string]$Text, [int]$Max = 240)
    $t = ($Text -replace '\s+', ' ').Trim()
    if ($t.Length -gt $Max) { return $t.Substring(0, $Max) + '...' }
    return $t
}

# A stage's JSON result file: @{ Data = <parsed or $null>; Problem = <'' or why there is no data> }. Never throws.
function Read-StageJson {
    param([string]$Path)
    $leaf = Split-Path -Leaf $Path
    if (-not (Test-Path -LiteralPath $Path)) { return @{ Data = $null; Problem = "no $leaf written" } }
    try { return @{ Data = (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json); Problem = '' } }
    catch { return @{ Data = $null; Problem = "$leaf unreadable: $(Limit-Text $_.Exception.Message 120)" } }
}

# The 'error CS' lines of a Unity log: @{ Count = n; Files = up to 4 distinct file names }. The tree is shared with other
# sessions, so the files may not be Skyline's. Never throws.
function Get-CompileErrors {
    param([string]$Log)
    $count = 0; $files = @()
    try {
        if ($Log -and (Test-Path -LiteralPath $Log)) {
            foreach ($line in (Select-String -LiteralPath $Log -Pattern 'error CS\d+' -ErrorAction Stop)) {
                $count++
                $m = [regex]::Match($line.Line, '([^\s(]+\.cs)\(\d+,\d+\)')
                $f = if ($m.Success) { $m.Groups[1].Value } else { '(file unknown)' }
                if ($files -notcontains $f -and $files.Count -lt 4) { $files += $f }
            }
        }
    } catch { }
    return @{ Count = $count; Files = $files }
}

# Folds stage A's editor-batch.json and its log into the stage's entry (mutates $Stage): the test counts and failures, the
# job's own error, any compile errors. Returns @{ SkipReason = '' | why B, C and D must not run }: the graphics editor on a
# broken compile shows a blocking safe-mode dialog. Test failures alone don't skip (the project compiles; B-D still tell
# something); a missing editor-batch.json, an error thrown in the job, or 'error CS' in the log do.
function Update-BatchStage {
    param($Stage, [string]$JsonPath, [string]$Log)
    $j = Read-StageJson $JsonPath
    $jsonOk = $false; $jobError = $false
    if ($j.Data) {
        $t = $j.Data.tests
        if ($t) {
            Add-StageNote $Stage "tests: $($t.passed) passed, $($t.failed) failed, $($t.skipped) skipped"
            $fails = @($t.failures | Where-Object { $_ })
            if ($fails.Count -gt 0) { Add-StageNote $Stage ("failures: " + ((@($fails | Select-Object -First 3) | ForEach-Object { Limit-Text $_ 160 }) -join ' | ')) }
        }
        # The scene lint (Task 16) is reported, not part of stage A's ok: lint problems are the judges' blockers.
        if ($j.Data.lint) { Add-StageNote $Stage "lint: $($j.Data.lint.count) problem(s)" }
        if ($j.Data.error) { $jobError = $true; Add-StageNote $Stage ("editor job error: " + (Limit-Text ([string]$j.Data.error))) }
        $jsonOk = [bool]($j.Data.ok -eq $true)
        if (-not $jsonOk -and -not $jobError -and -not ($t -and $t.failed -gt 0)) { Add-StageNote $Stage 'editor-batch.json says ok:false' }
    } else {
        Add-StageNote $Stage $j.Problem
    }
    $cs = Get-CompileErrors -Log $Log
    if ($cs.Count -gt 0) { Add-StageNote $Stage ("$($cs.Count) 'error CS' line(s) in the log, in: " + ($cs.Files -join ', ')) }
    $Stage.ok = [bool]($Stage.ok -and $jsonOk -and $cs.Count -eq 0)
    $skip = ''
    if ($cs.Count -gt 0) { $skip = "skipped: the project doesn't compile (" + ($cs.Files -join ', ') + ')' }
    elseif (-not $j.Data) { $skip = "skipped: the project doesn't compile or stage A never finished its job ($($j.Problem))" }
    elseif ($jobError) { $skip = "skipped: stage A's editor job failed ($(Limit-Text ([string]$j.Data.error) 120))" }
    return @{ SkipReason = $skip }
}

# Stage B: capture.json's recorded scenes and error.
function Update-CaptureStage {
    param($Stage, [string]$JsonPath)
    $j = Read-StageJson $JsonPath
    if ($j.Data) {
        if ($j.Data.recorded) { Add-StageNote $Stage "recorded: $($j.Data.recorded)" }
        if ($j.Data.error) { Add-StageNote $Stage ("capture error: " + (Limit-Text ([string]$j.Data.error))) }
        $Stage.ok = [bool]($Stage.ok -and ($j.Data.ok -eq $true))
    } else {
        Add-StageNote $Stage $j.Problem
        $Stage.ok = $false
    }
}

# Stage C: build.json's error and error count.
function Update-BuildStage {
    param($Stage, [string]$JsonPath)
    $j = Read-StageJson $JsonPath
    if ($j.Data) {
        if ($j.Data.errors) { Add-StageNote $Stage "build errors: $($j.Data.errors)" }
        if ($j.Data.error) { Add-StageNote $Stage ("build error: " + (Limit-Text ([string]$j.Data.error))) }
        $Stage.ok = [bool]($Stage.ok -and ($j.Data.ok -eq $true))
    } else {
        Add-StageNote $Stage $j.Problem
        $Stage.ok = $false
    }
}

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
            # The probe always writes the scene (Task 13), so a perf.json without one is stale or foreign.
            if (-not $p.scene) { $problems += 'scene missing (the arena must be measured)' }
            elseif ($p.scene -ne 'SkylineArena') { $problems += "measured '$($p.scene)', not SkylineArena" }
            # The whole-demo measurement (judge E, round 2: the 45 s window never reached the heavier fights). Older
            # probes don't write these fields; a probe that does must have measured all three fights to the end.
            if ($null -ne $p.fightsMeasured) {
                if ($p.fightsMeasured -lt 3) { $problems += "only $($p.fightsMeasured) of 3 fights measured" }
                if ($p.demoComplete -ne $true) { $problems += 'the demo did not complete' }
                if ((Test-IsNumber $p.worstFightOnePercentLowMs) -and $p.worstFightOnePercentLowMs -gt 16.7) { $problems += "the worst fight's 1% low is over 16.7 ms" }
            }
            $perfNote = "avg $($p.avgMs) ms, 1% low $($p.onePercentLowMs) ms (blocker over 16.7)$(if (Test-IsNumber $p.worstFightOnePercentLowMs) { ", worst fight $($p.worstFightOnePercentLowMs) ms" }), $($p.resolution)$(if ($p.gpu) { ", gpu $($p.gpu)" })$(if ($p.scene) { ", scene $($p.scene)" })"
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

# Stage E's bookkeeping (docs/SKYLINE-DESIGN.md 2.3): runs $Runner (which prints synccheck.py's JSON) when both the
# firing range recording and the director's schedule exist, saves the output to $JsonOut and folds its ok and note into
# the stage. Never throws.
function Get-SyncStage {
    param([string]$Rec, [string]$Schedule, [string]$JsonOut, [scriptblock]$Runner)
    $started = Get-Date
    $eok = $false; $enote = ''
    if ((Test-Path -LiteralPath $Rec) -and (Test-Path -LiteralPath $Schedule)) {
        try {
            $text = [string](& $Runner | Out-String)
            Set-Content -Path $JsonOut -Value $text -Encoding utf8
            $i = $text.IndexOf('{')
            if ($i -lt 0) { throw "no JSON in its output" }
            $m = $text.Substring($i) | ConvertFrom-Json
            $eok = [bool]($m.ok -eq $true)
            $enote = [string]$m.note
        } catch { $enote = "could not run the sync check: $($_.Exception.Message)" }
    } elseif (Test-Path -LiteralPath $Rec) { $enote = 'no firing-range-schedule.json (the range director did not write one)' }
    else { $enote = 'skipped: no firing range recording' }
    return [pscustomobject]@{ name = 'E sync check'; ok = $eok; seconds = [int]((Get-Date) - $started).TotalSeconds; log = ''; note = $enote }
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

    # the muted wait loop (stages B and D): a quick exit is reported ok, a hang times out (volume restored first), and the
    # result file path is accepted; no real audio session is touched (the test processes play nothing)
    $r6 = Invoke-Stage -Name 'muted ok' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'exit 0') -TimeoutSec 30 -Log '' -Locked -MuteAudio -DoneFile (Join-Path ([IO.Path]::GetTempPath()) "skyline-no-such-file-$PID.json")
    if (-not $r6.ok) { throw "self-test: a muted passing stage was reported as failed ($($r6 | ConvertTo-Json -Compress))" }
    $r7 = Invoke-Stage -Name 'muted hang' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'Start-Sleep 600') -TimeoutSec 4 -Log '' -Locked -MuteAudio
    if ($r7.ok -or $r7.note -notlike 'timed out*') { throw "self-test: a muted hung stage was not reported ($($r7 | ConvertTo-Json -Compress))" }
    if (Test-Path $script:LockDir) { throw 'self-test: lock left behind after a muted timeout' }
    if ((Set-AppAudioMuted -ProcessIds @([uint32]4294967295) -Muted $true) -gt 0) { throw 'self-test: the mute touched a session of a process that does not exist' }

    # an editor opened on the project while the stage waited for the lock: that stage fails, the runner doesn't crash, the lock is released
    $origAssert = ${function:Assert-ProjectUnlocked}
    try {
        ${function:Assert-ProjectUnlocked} = { throw 'simulated: the Unity editor has this project open' }
        $r5 = Invoke-Stage -Name 'open' -Exe 'powershell' -StageArgs @('-NoProfile', '-Command', 'exit 0') -TimeoutSec 30 -Log '' -Locked -CheckProject
    } finally { ${function:Assert-ProjectUnlocked} = $origAssert }
    if ($r5.ok -or $r5.note -notlike 'simulated: the Unity editor*') { throw "self-test: an open editor was not reported as the stage's failure ($($r5 | ConvertTo-Json -Compress))" }
    if (Test-Path $script:LockDir) { throw 'self-test: lock left behind after an open-editor failure' }

    # stages A, B, C: the result files folded into the manifest notes, and A's compile-failure skip
    $dirJ = Join-Path ([IO.Path]::GetTempPath()) "skyline-pass-json-selftest-$PID"
    New-Item -ItemType Directory -Force $dirJ | Out-Null
    try {
        function New-TestStage {
            param($ok = $true, $note = '')
            [pscustomobject]@{ name = 'X'; ok = $ok; seconds = 1; log = ''; note = $note }
        }
        $logOk = Join-Path $dirJ 'a-ok.log'; Set-Content -LiteralPath $logOk -Value 'Refresh completed' -Encoding utf8
        $logCs = Join-Path $dirJ 'a-cs.log'
        Set-Content -LiteralPath $logCs -Encoding utf8 -Value @('Assets/Other/Thing.cs(5,14): error CS0234: namespace missing', 'Assets/Other/Thing.cs(9,1): error CS1002: ; expected', 'Packages/x/Y.cs(2,2): error CS0103: nope')
        $jf = Join-Path $dirJ 'editor-batch.json'
        $okJson = '{"ok":true,"tests":{"passed":40,"failed":0,"skipped":0,"failures":[]},"scenes":["a","b"],"error":null}'
        $failJson = '{"ok":false,"tests":{"passed":38,"failed":2,"skipped":0,"failures":["T.One: expected 1","T.Two: expected 2"]},"scenes":["a","b"],"error":null}'
        $errJson = '{"ok":false,"tests":{"passed":0,"failed":0,"skipped":0,"failures":[]},"scenes":[],"error":"System.Exception: scene build blew up"}'

        Set-Content -LiteralPath $jf -Value $okJson -Encoding utf8
        $s = New-TestStage; $i = Update-BatchStage -Stage $s -JsonPath $jf -Log $logOk
        if (-not $s.ok -or $i.SkipReason -or $s.note -ne 'tests: 40 passed, 0 failed, 0 skipped') { throw "self-test: batch good case: ok=$($s.ok) skip='$($i.SkipReason)' note='$($s.note)'" }
        Set-Content -LiteralPath $jf -Value $failJson -Encoding utf8
        $s = New-TestStage; $i = Update-BatchStage -Stage $s -JsonPath $jf -Log $logOk
        if ($s.ok -or $i.SkipReason -or $s.note -notlike '*2 failed*failures: T.One: expected 1 | T.Two: expected 2*') { throw "self-test: batch test-failure case: ok=$($s.ok) skip='$($i.SkipReason)' note='$($s.note)'" }
        Set-Content -LiteralPath $jf -Value $errJson -Encoding utf8
        $s = New-TestStage; $i = Update-BatchStage -Stage $s -JsonPath $jf -Log $logOk
        if ($s.ok -or $i.SkipReason -notlike 'skipped: stage A''s editor job failed*' -or $s.note -notlike '*editor job error: System.Exception: scene build blew up*') { throw "self-test: batch job-error case: ok=$($s.ok) skip='$($i.SkipReason)' note='$($s.note)'" }
        Remove-Item -LiteralPath $jf
        $s = New-TestStage $false 'exit code 1'; $i = Update-BatchStage -Stage $s -JsonPath $jf -Log $logCs     # a compile error: no json, error CS in the log
        if ($s.ok -or $i.SkipReason -ne "skipped: the project doesn't compile (Assets/Other/Thing.cs, Packages/x/Y.cs)" -or $s.note -notlike 'exit code 1; no editor-batch.json written; 3 ''error CS'' line(s)*Assets/Other/Thing.cs, Packages/x/Y.cs') { throw "self-test: batch compile-error case: ok=$($s.ok) skip='$($i.SkipReason)' note='$($s.note)'" }
        $s = New-TestStage; $i = Update-BatchStage -Stage $s -JsonPath $jf -Log $logOk                    # no json and no compile error: still not run on
        if ($s.ok -or $i.SkipReason -notlike 'skipped: the project doesn''t compile or stage A never finished*') { throw "self-test: batch no-json case: ok=$($s.ok) skip='$($i.SkipReason)'" }
        Set-Content -LiteralPath $jf -Value '{"ok":tru' -Encoding utf8                                  # a truncated file
        $s = New-TestStage; $i = Update-BatchStage -Stage $s -JsonPath $jf -Log $logOk
        if ($s.ok -or -not $i.SkipReason -or $s.note -notlike '*editor-batch.json unreadable*') { throw "self-test: batch truncated case: ok=$($s.ok) note='$($s.note)'" }

        $cf = Join-Path $dirJ 'capture.json'
        Set-Content -LiteralPath $cf -Value '{"ok":false,"recorded":"SkylineFiringRange;","error":"the Recorder did not start for SkylineGallery: boom"}' -Encoding utf8
        $s = New-TestStage $true 'x'; Update-CaptureStage -Stage $s -JsonPath $cf
        if ($s.ok -or $s.note -ne 'x; recorded: SkylineFiringRange;; capture error: the Recorder did not start for SkylineGallery: boom') { throw "self-test: capture failure case: ok=$($s.ok) note='$($s.note)'" }
        Set-Content -LiteralPath $cf -Value '{"ok":true,"recorded":"SkylineFiringRange;SkylineGallery;","error":null}' -Encoding utf8
        $s = New-TestStage; Update-CaptureStage -Stage $s -JsonPath $cf
        if (-not $s.ok -or $s.note -ne 'recorded: SkylineFiringRange;SkylineGallery;') { throw "self-test: capture good case: ok=$($s.ok) note='$($s.note)'" }
        Remove-Item -LiteralPath $cf
        $s = New-TestStage; Update-CaptureStage -Stage $s -JsonPath $cf
        if ($s.ok -or $s.note -ne 'no capture.json written') { throw "self-test: capture missing case: ok=$($s.ok) note='$($s.note)'" }

        $bf = Join-Path $dirJ 'build.json'
        Set-Content -LiteralPath $bf -Value '{"ok":false,"exe":"x","seconds":5,"errors":3,"error":"System.Exception: nope"}' -Encoding utf8
        $s = New-TestStage; Update-BuildStage -Stage $s -JsonPath $bf
        if ($s.ok -or $s.note -ne 'build errors: 3; build error: System.Exception: nope') { throw "self-test: build failure case: ok=$($s.ok) note='$($s.note)'" }
        Set-Content -LiteralPath $bf -Value '{"ok":true,"exe":"x","seconds":5,"errors":0,"error":null}' -Encoding utf8
        $s = New-TestStage; Update-BuildStage -Stage $s -JsonPath $bf
        if (-not $s.ok -or $s.note) { throw "self-test: build good case: ok=$($s.ok) note='$($s.note)'" }
        Remove-Item -LiteralPath $bf
        $s = New-TestStage; Update-BuildStage -Stage $s -JsonPath $bf
        if ($s.ok -or $s.note -ne 'no build.json written') { throw "self-test: build missing case: ok=$($s.ok) note='$($s.note)'" }
    } finally {
        Remove-Item -LiteralPath $dirJ -Recurse -Force -ErrorAction SilentlyContinue
    }

    # stage D's perf.json handling: every bad file fails the stage with a note and none of them throws
    $dir = Join-Path ([IO.Path]::GetTempPath()) "skyline-pass-perf-selftest-$PID"
    New-Item -ItemType Directory -Force $dir | Out-Null
    try {
        $good = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":11.2,"worstMs":15.0,"resolution":"1920x1080","scene":"SkylineArena"}'
        $cases = @(
            @{ n = 'good'; json = $good; ok = $true; like = '*1% low 11.2*' },
            @{ n = 'with gpu'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":11.2,"worstMs":15.0,"resolution":"1920x1080","gpu":"NVIDIA GeForce RTX 3050","scene":"SkylineArena"}'; ok = $true; like = '*1920x1080, gpu NVIDIA GeForce RTX 3050, scene SkylineArena' },
            @{ n = 'no scene'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":11.2,"worstMs":15.0,"resolution":"1920x1080"}'; ok = $false; like = '*scene missing*' },
            @{ n = 'truncated'; json = '{"frames":1200,"avgMs":8.1,"onePerc'; ok = $false; like = '*unreadable*' },
            @{ n = 'garbage'; json = 'not json at all'; ok = $false; like = '*unreadable*' },
            @{ n = 'missing low'; json = '{"frames":1200,"avgMs":8.1,"resolution":"1920x1080"}'; ok = $false; like = '*onePercentLowMs missing*' },
            @{ n = 'zero low'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":0,"resolution":"1920x1080"}'; ok = $false; like = '*onePercentLowMs missing or not above 0*' },
            @{ n = 'string low'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":"fast","resolution":"1920x1080"}'; ok = $false; like = '*onePercentLowMs missing*' },
            @{ n = 'no frames'; json = '{"frames":0,"avgMs":8.1,"onePercentLowMs":11.2,"resolution":"1920x1080"}'; ok = $false; like = '*frames missing*' },
            @{ n = 'over budget'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":20.5,"resolution":"1920x1080"}'; ok = $false; like = '*over 16.7*' },
            @{ n = 'wrong resolution'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":11.2,"resolution":"1280x720"}'; ok = $false; like = "*resolution is '1280x720'*" },
            @{ n = 'arena'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":11.2,"resolution":"1920x1080","gpu":"X","scene":"SkylineArena"}'; ok = $true; like = '*1920x1080, gpu X, scene SkylineArena' },
            @{ n = 'wrong scene'; json = '{"frames":1200,"avgMs":8.1,"onePercentLowMs":11.2,"resolution":"1920x1080","scene":"SkylineGallery"}'; ok = $false; like = "*measured 'SkylineGallery', not SkylineArena*" }
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

    # stage E's three branches and its JSON handling (the runner is faked: no uv, no ears)
    $dir = Join-Path ([IO.Path]::GetTempPath()) "skyline-pass-sync-selftest-$PID"
    New-Item -ItemType Directory -Force $dir | Out-Null
    try {
        $rec = Join-Path $dir 'rec-SkylineFiringRange.mp4'; $sched = Join-Path $dir 'firing-range-schedule.json'; $jo = Join-Path $dir 'sync-check.json'
        $s = Get-SyncStage -Rec $rec -Schedule $sched -JsonOut $jo -Runner { throw 'must not run' }
        if ($s.ok -or $s.note -ne 'skipped: no firing range recording') { throw "self-test: sync without a recording gave '$($s.note)'" }
        Set-Content -LiteralPath $rec -Value 'x'
        $s = Get-SyncStage -Rec $rec -Schedule $sched -JsonOut $jo -Runner { throw 'must not run' }
        if ($s.ok -or $s.note -notlike 'no firing-range-schedule.json*') { throw "self-test: sync without a schedule gave '$($s.note)'" }
        Set-Content -LiteralPath $sched -Value '{}'
        $syncCases = @(
            @{ n = 'ok'; run = { 'uv noise'; '{"ok": true, "note": "12/12 events within 35 ms"}' }; ok = $true; like = '12/12 events*' },
            @{ n = 'not ok'; run = { '{"ok": false, "note": "3/12 events within 35 ms"}' }; ok = $false; like = '3/12 events*' },
            @{ n = 'garbage'; run = { 'Traceback: boom' }; ok = $false; like = 'could not run the sync check*' },
            @{ n = 'truncated'; run = { '{"ok": tr' }; ok = $false; like = 'could not run the sync check*' },
            @{ n = 'throws'; run = { throw 'uv missing' }; ok = $false; like = 'could not run the sync check: uv missing' }
        )
        foreach ($c in $syncCases) {
            $s = Get-SyncStage -Rec $rec -Schedule $sched -JsonOut $jo -Runner $c.run
            if ($s.ok -ne $c.ok -or $s.note -notlike $c.like) { throw "self-test: sync case '$($c.n)' gave ok=$($s.ok) note='$($s.note)'" }
        }
    } finally {
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host 'pass.ps1 self-test passed.'
    exit 0
}

# From here on a manifest.json is always written (the finally block), even when preflight or a stage's bookkeeping throws.
$stages = @()
$allOk = $false
if ($Only -and ($Only.ToUpper() -notmatch '^[ABCDE]+$')) { Write-Host "SkylinePass: -Only takes stage letters from ABCDE, got '$Only'"; exit 1 }
if ($Only -and -not $Out) { Write-Host 'SkylinePass: -Only needs -Out <an existing pass folder>'; exit 1 }
function Test-RunStage([string]$Letter) { (-not $Only) -or $Only.ToUpper().Contains($Letter) }
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
        # Results of an earlier pass in a reused -Out folder must never stand in for this one (with -Only: the chosen
        # stages' own results only).
        $staleByStage = @{
            A = @('editor-batch.json'); B = @('capture.json', 'firing-range-schedule.json', 'rec-*.mp4', 'still-*.png')
            C = @('build.json', 'build\SkylineRepo.exe'); D = @('perf.json')
            E = @('sync-check.json', 'sync-check.stderr.txt', 'audio-firing-range.png')
        }
        foreach ($s in 'A', 'B', 'C', 'D', 'E') {
            if (-not (Test-RunStage $s)) { continue }
            foreach ($stale in $staleByStage[$s]) {
                if ($stale.Contains('*')) {
                    Get-ChildItem -LiteralPath $Out -Filter $stale -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
                } else {
                    Remove-Item -LiteralPath (Join-Path $Out $stale) -Force -ErrorAction SilentlyContinue
                }
            }
        }
        # A: editor batch job (tests, scenes)
        if (Test-RunStage 'A') {
        $log = Join-Path $Out 'stage-a-editor-batch.log'
        # (batch mode, no GPU: through the memory cap)
        $a = Invoke-Stage -Name 'A editor batch' -Exe $unity -Locked -Capped -CheckProject -TimeoutSec 1800 -Log $log -StageArgs @(
            '-batchmode', '-projectPath', $proj, '-logFile', "`"$log`"",
            '-executeMethod', 'JamKit.SkylineRepo.EditorTools.SkylinePassBatch.RunFromCommandLine', '-skylineOut', "`"$Out`"")
        $aInfo = Update-BatchStage -Stage $a -JsonPath (Join-Path $Out 'editor-batch.json') -Log $log
        $stages += $a
        } else {
            $aInfo = [pscustomobject]@{ SkipReason = $null }   # -Only without A: an earlier -Only A in this folder compiled it
        }

        if ($aInfo.SkipReason) {
            # A graphics editor on a project that doesn't compile shows a blocking safe-mode dialog: don't start B, C or D.
            foreach ($n in 'B recording', 'C build', 'D perf') {
                $stages += [pscustomobject]@{ name = $n; ok = $false; seconds = 0; log = ''; note = $aInfo.SkipReason }
            }
        } else {
            # B: graphics editor recording (audio plays only outside batch mode; a normal window so it keeps presenting).
            # Uncapped (the D3D11 device needs the memory a Job Object cap refuses); -silent-crashes: a crash exits, no dialog.
            $log = Join-Path $Out 'stage-b-capture.log'
            if (-not (Test-RunStage 'B')) {
            } elseif ($NoWindows) {
                $stages += [pscustomobject]@{ name = 'B recording'; ok = $false; seconds = 0; log = ''; note = 'skipped: -NoWindows (needs a normal window)' }
            } else {
            $b = Invoke-Stage -Name 'B recording' -Exe $unity -Locked -CheckProject -MinFreeGB 3 -TimeoutSec 900 -Log $log -WindowStyle Normal -MuteAudio -DoneFile (Join-Path $Out 'capture.json') -StageArgs (@(
                '-projectPath', $proj, '-logFile', "`"$log`"", '-silent-crashes',
                '-executeMethod', 'JamKit.SkylineRepo.EditorTools.Recording.SkylineCapture.RunFromCommandLine', '-skylineOut', "`"$Out`"") + @($CaptureArgs -split '\s+' | Where-Object { $_ }))
            Update-CaptureStage -Stage $b -JsonPath (Join-Path $Out 'capture.json')
            $stages += $b
            }

            # C: Windows build of Skyline's scenes (batch mode: through the memory cap). -buildTarget Win64 sets the target when
            # the editor opens (Unity's own switch-then-import path) instead of leaving BuildPipeline to switch it mid-method.
            # A project whose active target was WebGL may be left on Win64 afterwards: tell the Visiting Hours session.
            # The build's shader compilers need more than the default 30% cap: at 0.30 URP/Lit's variants failed "out of
            # memory" (2026-10-14: 289 errors, magenta materials in the build), and Unity CACHES failed compiles, so a
            # retry stays broken until Library/ShaderCache and Library/ShaderCache.db are deleted. 0.6 compiled clean.
            if (Test-RunStage 'C') {
            $log = Join-Path $Out 'stage-c-build.log'
            $shareBefore = $CapShare
            $script:CapShare = [math]::Max($CapShare, 0.6)
            $c = Invoke-Stage -Name 'C build' -Exe $unity -Locked -Capped -CheckProject -TimeoutSec 1800 -Log $log -StageArgs @(
                '-batchmode', '-quit', '-buildTarget', 'Win64', '-projectPath', $proj, '-logFile', "`"$log`"",
                '-executeMethod', 'JamKit.SkylineRepo.EditorTools.SkylineBuild.BuildFromCommandLine', '-skylineOut', "`"$Out`"")
            $script:CapShare = $shareBefore
            Update-BuildStage -Stage $c -JsonPath (Join-Path $Out 'build.json')
            if ((Test-Path $log) -and (Select-String -LiteralPath $log -Pattern 'out of memory (while parsing|during compilation)' -Quiet)) {
                $c.ok = $false
                $c.note = "shader compiles ran out of memory (magenta in the build): delete Library/ShaderCache* and rebuild. $($c.note)"
            }
            $stages += $c
            }

            # D: the built player's frame-time probe (windowed 1080p, normal window, not batch mode; uncapped like B)
            $exe = Join-Path $Out 'build\SkylineRepo.exe'
            if (-not (Test-RunStage 'D')) {
            } elseif ($NoWindows) {
                $stages += [pscustomobject]@{ name = 'D perf'; ok = $false; seconds = 0; log = ''; note = 'skipped: -NoWindows (needs a normal window)' }
            } elseif (Test-Path $exe) {
                $log = Join-Path $Out 'stage-d-perf.log'
                $d = Invoke-Stage -Name 'D perf' -Exe $exe -Locked -MinFreeGB 3 -TimeoutSec 240 -Log $log -WindowStyle Normal -MuteAudio -DoneFile (Join-Path $Out 'perf.json') -StageArgs (@(
                    '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080', '-logFile', "`"$log`"", '-silent-crashes',
                    '-skyline-perf', "`"$Out`"") + @($PerfArgs -split '\s+' | Where-Object { $_ }))
                Update-PerfStage -Stage $d -PerfPath (Join-Path $Out 'perf.json')
                $stages += $d
            } else {
                $stages += [pscustomobject]@{ name = 'D perf'; ok = $false; seconds = 0; log = ''; note = 'skipped: no build' }
            }
        }
    }

    # E: the sync check without Unity (docs/SKYLINE-DESIGN.md 2.3): the firing range recording's sound onsets against the
    # schedule its director wrote, aligned on the marker beep; plus the ears' picture of the recording's audio.
    # Only after a good preflight: otherwise the stale sweep never ran, and an old recording and schedule would be checked.
    if ($preflightOk -and (Test-RunStage 'E')) {
        $rec = Join-Path $Out 'rec-SkylineFiringRange.mp4'
        $schedule = Join-Path $Out 'firing-range-schedule.json'
        $stages += Get-SyncStage -Rec $rec -Schedule $schedule -JsonOut (Join-Path $Out 'sync-check.json') -Runner {
            $ears = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.claude\skills\ears'
            $sounds = Join-Path $RepoRoot 'Assets\JamKit\Templates\SkylineRepo\Resources\SkylineAudio'
            $err = Join-Path $Out 'sync-check.stderr.txt'
            # stdout (the JSON) only; uv's progress goes to stderr, which under Stop would surface as an error in PS 5.1
            # (this scriptblock runs in its own scope, so the preference goes back by itself)
            $ErrorActionPreference = 'Continue'
            & uv run --project $ears python (Join-Path $ears 'listen.py') measure $rec --png (Join-Path $Out 'audio-firing-range.png') 2>$err | Out-Null
            & uv run --project $ears python (Join-Path $RepoRoot 'tools\skyline\soundforge\synccheck.py') $rec $schedule --sounds $sounds 2>>$err
        }
    } elseif (Test-RunStage 'E') {
        $stages += [pscustomobject]@{ name = 'E sync check'; ok = $false; seconds = 0; log = ''; note = 'skipped: preflight failed' }
    }
} catch {
    $stages += [pscustomobject]@{ name = 'runner'; ok = $false; seconds = 0; log = ''; note = "unexpected: $($_.Exception.Message)" }
} finally {
    Set-KeepAwake $false
    $allOk = (@($stages).Count -gt 0) -and -not (@($stages) | Where-Object { -not $_.ok })
    $manifestWritten = $false
    try {
        New-Item -ItemType Directory -Force $Out | Out-Null
        $manifestName = if ($Only) { "manifest-$($Only.ToUpper()).json" } else { 'manifest.json' }
        [pscustomobject]@{ ok = $allOk; out = $Out; stages = @($stages) } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Out $manifestName) -Encoding utf8
        $manifestWritten = $true
    } catch {
        $allOk = $false
        Write-Host "SkylinePass: could not write manifest.json in '$Out': $($_.Exception.Message)"
    }
    @($stages) | Format-Table name, ok, seconds, note -AutoSize | Out-String -Width 220 | Write-Host
    Write-Host "SkylinePass $(if ($allOk) { 'OK' } else { 'FAILED' }): $Out"
}
# The exit code says whether the RUNNER worked: 0 whenever manifest.json was written (its "ok" field carries pass or fail, so a
# failed stage is not a failed runner); non-zero only when no manifest could be written.
if ($manifestWritten) { exit 0 } else { exit 1 }
