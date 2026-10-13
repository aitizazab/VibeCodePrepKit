# The shared one-Unity-job lock (docs/UNITY-LOCK.md), for the Skyline Repo runner.
#   . tools/skyline/unity-lock.ps1; Enter-UnityLock -Who 'skyline-repo'; try { ... } finally { Exit-UnityLock }
#   powershell -File tools/skyline/unity-lock.ps1 -LockSelfTest   (a distinct name: pass.ps1 dot-sources this file and has its own -SelfTest)
param([switch]$LockSelfTest)

$script:LockDir = 'C:\Users\User\Desktop\yougotgamesonyophone\.unity-lock'
$script:LockScriptPath = $PSCommandPath

# The lock's owner as written in owner.txt: @{ Pid = <int, 0 when unknown>; Text = <trimmed text or ''> }.
# A missing, empty, half-written or unparseable owner.txt is "unknown" (Pid 0), never an exception.
function Get-UnityLockOwner {
    param([string]$Dir)
    $text = ''
    try {
        $owner = Join-Path $Dir 'owner.txt'
        if (Test-Path -LiteralPath $owner) { $text = ([string](Get-Content -LiteralPath $owner -Raw -ErrorAction Stop)).Trim() }
    } catch { $text = '' }
    $ownerPid = 0
    if ($text) {
        $parsed = 0
        if ([int]::TryParse(($text -split ' ')[0], [ref]$parsed) -and $parsed -gt 0) { $ownerPid = $parsed }
    }
    return @{ Pid = $ownerPid; Text = $text }
}

# True when the lock folder can be reclaimed: its owner process is gone, or the owner is unknown and the folder is more than 60 s old
# (a folder with no owner.txt yet is normal for the instant between mkdir and the owner write, so it gets a grace period).
function Test-UnityLockStale {
    param([string]$Dir, [int]$OwnerPid)
    if ($OwnerPid -gt 0) { return -not (Get-Process -Id $OwnerPid -ErrorAction SilentlyContinue) }
    try { return ((Get-Date) - (Get-Item -LiteralPath $Dir -ErrorAction Stop).LastWriteTime).TotalSeconds -gt 60 } catch { return $false }
}

function Enter-UnityLock {
    param([string]$Who = 'skyline-repo', [int]$TimeoutMinutes = 180, [string]$Dir = $script:LockDir, [int]$PollSeconds = 30)
    # Local, so a caller running under $ErrorActionPreference = 'Stop' (pass.ps1) can't turn mkdir's expected stderr on contention
    # into a terminating NativeCommandError (Windows PowerShell 5.1).
    $ErrorActionPreference = 'Continue'
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ($true) {
        # cmd's mkdir fails atomically when the folder exists (CreateDirectoryW), unlike New-Item -Force
        cmd /c mkdir "`"$Dir`"" 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            try {
                Set-Content -LiteralPath (Join-Path $Dir 'owner.txt') -Value "$PID $Who $(Get-Date -Format o)" -Encoding ascii -ErrorAction Stop
            } catch {
                $writeError = $_.Exception.Message
                Remove-Item -LiteralPath $Dir -Recurse -Force -ErrorAction SilentlyContinue
                throw "Unity lock: took '$Dir' but could not write owner.txt ($writeError); released it again"
            }
            $script:HeldLock = $Dir
            return
        }
        $o = Get-UnityLockOwner -Dir $Dir
        if (Test-UnityLockStale -Dir $Dir -OwnerPid $o.Pid) {
            # Claim the stale folder by renaming it to a name only this process uses: a rename is atomic, so of several waiters that
            # all saw the same dead owner exactly one wins; the rest fail the Move-Item and just loop. Nobody deletes $Dir itself.
            $claimed = "$Dir.stale-$PID-$([DateTime]::UtcNow.Ticks)"
            $moved = $false
            try { Move-Item -LiteralPath $Dir -Destination $claimed -ErrorAction Stop; $moved = $true } catch { }
            if ($moved) {
                # The rename isn't conditional: a fresh live lock may have replaced the stale one between the check and the rename.
                $now = Get-UnityLockOwner -Dir $claimed
                if ($now.Pid -ne $o.Pid -or $now.Text -ne $o.Text) {
                    try { Move-Item -LiteralPath $claimed -Destination $Dir -ErrorAction Stop; continue } catch { }
                }
                Write-Host "Unity lock: owner '$($o.Text)' is gone; took over the stale lock."
                Remove-Item -LiteralPath $claimed -Recurse -Force -ErrorAction SilentlyContinue
                continue
            }
        }
        if ((Get-Date) -gt $deadline) {
            $heldBy = (Get-UnityLockOwner -Dir $Dir).Text
            if (-not $heldBy) { $heldBy = '(no owner recorded)' }
            throw "Unity lock: still held by '$heldBy' after $TimeoutMinutes min"
        }
        Start-Sleep -Seconds $PollSeconds
    }
}

function Exit-UnityLock {
    if ($script:HeldLock -and (Test-Path -LiteralPath $script:HeldLock)) {
        if ((Get-UnityLockOwner -Dir $script:HeldLock).Pid -eq $PID) { Remove-Item -LiteralPath $script:HeldLock -Recurse -Force }
    }
    $script:HeldLock = $null
}

function Test-UnityLockSelf {
    $dir = Join-Path ([IO.Path]::GetTempPath()) "skyline-lock-selftest-$PID"
    $clean = { Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Filter "skyline-lock-selftest-$PID*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }
    & $clean
    try {
        # 1. take and release
        Enter-UnityLock -Who 'selftest' -Dir $dir
        if (-not (Test-Path (Join-Path $dir 'owner.txt'))) { throw 'selftest: owner.txt missing' }
        Exit-UnityLock
        if (Test-Path $dir) { throw 'selftest: lock not released' }

        # 2. a stale lock (a pid that doesn't exist) is taken over, and no renamed leftovers remain
        New-Item -ItemType Directory $dir | Out-Null
        Set-Content (Join-Path $dir 'owner.txt') '999999 ghost 2026-01-01' -Encoding ascii
        Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 1
        if ((Get-UnityLockOwner -Dir $dir).Pid -ne $PID) { throw 'selftest: stale lock not taken over' }
        Exit-UnityLock
        if (@(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Filter "skyline-lock-selftest-$PID*").Count -ne 0) { throw 'selftest: leftovers after stale takeover' }

        # 3. a live owner (our parent process) is not taken over: with a zero timeout it throws the lock's own message. Run under
        #    $ErrorActionPreference = 'Stop' (as pass.ps1 will) to prove contention doesn't surface as a NativeCommandError.
        New-Item -ItemType Directory $dir | Out-Null
        $parent = (Get-CimInstance Win32_Process -Filter "ProcessId=$PID").ParentProcessId
        Set-Content (Join-Path $dir 'owner.txt') "$parent livetest 2026-01-01" -Encoding ascii
        $msg = & {
            $ErrorActionPreference = 'Stop'
            try { Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 0; 'NO-THROW' } catch { $_.Exception.GetType().Name + ': ' + $_.Exception.Message }
        }
        Remove-Item -Recurse -Force $dir
        if ($msg -eq 'NO-THROW') { throw 'selftest: took a lock held by a live process' }
        if ($msg -notlike "RuntimeException: Unity lock: still held by '$parent livetest*") { throw "selftest: contention under Stop threw the wrong thing: $msg" }

        # 4. an unparseable owner.txt is "unknown": held while fresh, taken over once the folder is more than 60 s old, never an exception
        New-Item -ItemType Directory $dir | Out-Null
        Set-Content (Join-Path $dir 'owner.txt') 'not-a-pid garbage' -Encoding ascii
        $threw = $false
        try { Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 0 } catch { $threw = $_.Exception.Message -like "Unity lock: still held by 'not-a-pid garbage'*" }
        if (-not $threw) { throw 'selftest: a fresh unparseable owner was not treated as held' }
        (Get-Item -LiteralPath $dir).LastWriteTime = (Get-Date).AddMinutes(-2)
        Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 1
        if ((Get-UnityLockOwner -Dir $dir).Pid -ne $PID) { throw 'selftest: an old unparseable-owner lock was not taken over' }
        Exit-UnityLock

        # 5. a folder with no owner.txt: held while fresh (message guarded), taken over once more than 60 s old
        New-Item -ItemType Directory $dir | Out-Null
        $threw = $false
        try { Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 0 } catch { $threw = $_.Exception.Message -like "Unity lock: still held by '(no owner recorded)'*" }
        if (-not $threw) { throw 'selftest: a fresh ownerless lock was not treated as held (or its message failed)' }
        (Get-Item -LiteralPath $dir).LastWriteTime = (Get-Date).AddMinutes(-2)
        Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 1
        if ((Get-UnityLockOwner -Dir $dir).Pid -ne $PID) { throw 'selftest: an old ownerless lock was not taken over' }
        Exit-UnityLock

        # 6. an owner.txt that can't be written releases the folder again (a Set-Content that throws shadows the real one for this scope)
        $blocked = & {
            $ErrorActionPreference = 'Stop'
            function Set-Content { throw 'simulated write failure' }
            try { Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 0; 'NO-THROW' } catch { $_.Exception.Message }
        }
        if ($blocked -notlike '*could not write owner.txt*') { throw "selftest: owner write failure not reported: $blocked" }
        if (Test-Path $dir) { throw 'selftest: lock folder left behind after a failed owner write' }

        # 7. two waiters racing for one stale lock: they must hold it one after the other, never together
        New-Item -ItemType Directory $dir | Out-Null
        Set-Content (Join-Path $dir 'owner.txt') '999999 ghost 2026-01-01' -Encoding ascii
        $job = {
            param($path, $dir)
            . $path
            Enter-UnityLock -Who 'racer' -Dir $dir -TimeoutMinutes 1 -PollSeconds 1
            $start = [DateTime]::UtcNow.Ticks
            Start-Sleep -Seconds 2
            $end = [DateTime]::UtcNow.Ticks
            Exit-UnityLock
            [pscustomobject]@{ Start = $start; End = $end }
        }
        $jobs = 1..2 | ForEach-Object { Start-Job -ScriptBlock $job -ArgumentList $script:LockScriptPath, $dir }
        $spans = @($jobs | Wait-Job -Timeout 90 | Receive-Job)
        $jobs | Remove-Job -Force
        if ($spans.Count -ne 2) { throw "selftest: race: expected 2 racers to finish, got $($spans.Count)" }
        $a, $b = $spans | Sort-Object Start
        if ($a.End -gt $b.Start) { throw 'selftest: race: two waiters held the stale lock at the same time' }
        if (Test-Path $dir) { throw 'selftest: race: lock left behind' }
    } finally {
        $script:HeldLock = $null
        & $clean
    }
    Write-Host 'unity-lock self-test passed.'
}

if ($LockSelfTest) { Test-UnityLockSelf }
