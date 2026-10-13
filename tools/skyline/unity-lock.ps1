# The shared one-Unity-job lock (docs/UNITY-LOCK.md), for the Skyline Repo runner.
#   . tools/skyline/unity-lock.ps1; Enter-UnityLock -Who 'skyline-repo'; try { ... } finally { Exit-UnityLock }
#   powershell -File tools/skyline/unity-lock.ps1 -LockSelfTest   (a distinct name: pass.ps1 dot-sources this file and has its own -SelfTest)
param([switch]$LockSelfTest)

$script:LockDir = 'C:\Users\User\Desktop\yougotgamesonyophone\.unity-lock'

function Enter-UnityLock {
    param([string]$Who = 'skyline-repo', [int]$TimeoutMinutes = 180, [string]$Dir = $script:LockDir)
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ($true) {
        # cmd's mkdir fails atomically when the folder exists (CreateDirectoryW), unlike New-Item -Force
        cmd /c mkdir "`"$Dir`"" 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Set-Content -Path (Join-Path $Dir 'owner.txt') -Value "$PID $Who $(Get-Date -Format o)" -Encoding ascii
            $script:HeldLock = $Dir
            return
        }
        $owner = Join-Path $Dir 'owner.txt'
        $ownerPid = 0
        if (Test-Path $owner) { $ownerPid = [int](((Get-Content $owner -Raw) -split ' ')[0]) }
        if ($ownerPid -gt 0 -and -not (Get-Process -Id $ownerPid -ErrorAction SilentlyContinue)) {
            Write-Host "Unity lock: owner $ownerPid is gone; taking over the stale lock."
            Remove-Item -Recurse -Force $Dir
            continue
        }
        if ((Get-Date) -gt $deadline) { throw "Unity lock: still held by '$((Get-Content $owner -Raw).Trim())' after $TimeoutMinutes min" }
        Start-Sleep -Seconds 30
    }
}

function Exit-UnityLock {
    if ($script:HeldLock -and (Test-Path $script:HeldLock)) {
        $owner = Join-Path $script:HeldLock 'owner.txt'
        if ((Test-Path $owner) -and (((Get-Content $owner -Raw) -split ' ')[0] -eq "$PID")) { Remove-Item -Recurse -Force $script:HeldLock }
    }
    $script:HeldLock = $null
}

function Test-UnityLockSelf {
    $dir = Join-Path ([IO.Path]::GetTempPath()) "skyline-lock-selftest-$PID"
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    # 1. take and release
    Enter-UnityLock -Who 'selftest' -Dir $dir
    if (-not (Test-Path (Join-Path $dir 'owner.txt'))) { throw 'selftest: owner.txt missing' }
    Exit-UnityLock
    if (Test-Path $dir) { throw 'selftest: lock not released' }
    # 2. a stale lock (a pid that doesn't exist) is taken over
    New-Item -ItemType Directory $dir | Out-Null
    Set-Content (Join-Path $dir 'owner.txt') '999999 ghost 2026-01-01' -Encoding ascii
    Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 1
    if (((Get-Content (Join-Path $dir 'owner.txt') -Raw) -split ' ')[0] -ne "$PID") { throw 'selftest: stale lock not taken over' }
    Exit-UnityLock
    # 3. a live owner (our parent process) is not taken over: with a zero timeout it throws instead
    New-Item -ItemType Directory $dir | Out-Null
    $parent = (Get-CimInstance Win32_Process -Filter "ProcessId=$PID").ParentProcessId
    Set-Content (Join-Path $dir 'owner.txt') "$parent livetest 2026-01-01" -Encoding ascii
    $threw = $false
    try { Enter-UnityLock -Who 'selftest' -Dir $dir -TimeoutMinutes 0 } catch { $threw = $true }
    Remove-Item -Recurse -Force $dir
    if (-not $threw) { throw 'selftest: took a lock held by a live process' }
    Write-Host 'unity-lock self-test passed.'
}

if ($LockSelfTest) { Test-UnityLockSelf }
