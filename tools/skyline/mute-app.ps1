# Per-application mute for the pass's automated runs (stage B: the graphics editor recording the scenes, stage D: the built
# player's frame-time run). Their sound played through the speakers while the creator worked. This sets the Windows per-app
# SESSION volume (Core Audio: IAudioSessionManager2 / ISimpleAudioVolume, the same thing the Volume Mixer moves) of one process
# and its child processes to zero. It only changes what reaches the speakers: Unity's Recorder captures the audio inside Unity,
# before the device, so the recordings keep their sound.
#
# Two ways to use it:
#   . tools\skyline\mute-app.ps1                      (dot-sourced by pass.ps1: defines Set-AppAudioMuted, Get-ProcessTreeIds)
#   powershell -NoProfile -File tools\skyline\mute-app.ps1 -ProcessId 1234 [-WaitSeconds 60]   mutes that process tree; keeps
#       retrying until its audio session exists (up to -WaitSeconds, default 0 = one try); exits 0 when it muted something
#   powershell -NoProfile -File tools\skyline\mute-app.ps1 -ProcessId 1234 -Restore           volume back to 100%, unmuted
#   powershell -NoProfile -File tools\skyline\mute-app.ps1 -List                              prints every audio session (read only)
#   powershell -NoProfile -File tools\skyline\mute-app.ps1 -Check                             compiles the Core Audio type and lists
#       the sessions; changes nothing
#
# Windows may REMEMBER an application's volume by its exe path (the Volume Mixer does, across restarts). So the pass always puts
# the volume back the moment a stage's work is done (the result file exists) and before it kills a stuck stage. If a pass dies
# in between, run:  powershell -NoProfile -File tools\skyline\mute-app.ps1 -RestoreName Unity   (or the exe's name, no .exe)
param([int]$ProcessId = 0, [int]$WaitSeconds = 0, [switch]$Restore, [switch]$List, [switch]$Check, [string]$RestoreName = '')

$script:MuteSource = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class SkylineAppMute
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr groupingParam, uint streamFlags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr groupingParam, uint streamFlags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
    }

    // IAudioSessionControl's nine methods come first in IAudioSessionControl2's table; they are never called here
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName(IntPtr name, ref Guid context);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath(IntPtr path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid param);
        [PreserveSig] int SetGroupingParam(ref Guid param, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int GetSessionIdentifier(out IntPtr id);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference(bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute(bool mute, ref Guid context);
        [PreserveSig] int GetMute(out bool mute);
    }

    static IAudioSessionEnumerator Sessions()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        IMMDevice device;
        if (enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out device) != 0 || device == null) return null;
        Guid iid = typeof(IAudioSessionManager2).GUID;
        object o;
        if (device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out o) != 0 || o == null) return null;
        IAudioSessionEnumerator sessions;
        if (((IAudioSessionManager2)o).GetSessionEnumerator(out sessions) != 0) return null;
        return sessions;
    }

    /// <summary>Sets the volume and mute of every audio session owned by one of <paramref name="pids"/>. Returns how many
    /// sessions it touched, or -1 when there is no audio device or Core Audio failed.</summary>
    public static int Apply(uint[] pids, float volume, bool mute)
    {
        try
        {
            var sessions = Sessions();
            if (sessions == null) return -1;
            int count;
            if (sessions.GetCount(out count) != 0) return -1;
            var wanted = new HashSet<uint>(pids);
            int touched = 0;
            Guid none = Guid.Empty;
            for (int i = 0; i < count; i++)
            {
                IAudioSessionControl2 s;
                if (sessions.GetSession(i, out s) != 0 || s == null) continue;
                uint pid;
                if (s.GetProcessId(out pid) != 0 || !wanted.Contains(pid)) continue;
                var v = (ISimpleAudioVolume)s;
                if (v.SetMasterVolume(volume, ref none) == 0 && v.SetMute(mute, ref none) == 0) touched++;
            }
            return touched;
        }
        catch (Exception) { return -1; }
    }

    /// <summary>One line a session: pid, volume, mute (read only).</summary>
    public static string Describe()
    {
        var sb = new StringBuilder();
        try
        {
            var sessions = Sessions();
            if (sessions == null) return "no audio device or Core Audio failed";
            int count;
            sessions.GetCount(out count);
            for (int i = 0; i < count; i++)
            {
                IAudioSessionControl2 s;
                if (sessions.GetSession(i, out s) != 0 || s == null) continue;
                uint pid = 0; float vol = -1f; bool muted = false;
                s.GetProcessId(out pid);
                var v = (ISimpleAudioVolume)s;
                v.GetMasterVolume(out vol);
                v.GetMute(out muted);
                sb.Append("pid ").Append(pid).Append(" volume ").Append(vol.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(muted ? " muted" : " audible").Append('\n');
            }
        }
        catch (Exception e) { sb.Append("failed: ").Append(e.Message); }
        return sb.Length == 0 ? "no audio sessions" : sb.ToString().TrimEnd();
    }
}
'@

function Initialize-AppMute {
    if (-not ('SkylineAppMute' -as [type])) { Add-Type -TypeDefinition $script:MuteSource -Language CSharp }
}

# The process and everything it started (Unity starts helper processes; an audio session belongs to whichever one plays).
function Get-ProcessTreeIds {
    param([int]$RootId)
    $ids = New-Object System.Collections.Generic.List[uint32]
    $ids.Add([uint32]$RootId)
    try {
        $all = @(Get-CimInstance -ClassName Win32_Process -Property ProcessId, ParentProcessId -ErrorAction Stop)
        $queue = New-Object System.Collections.Generic.Queue[uint32]
        $queue.Enqueue([uint32]$RootId)
        while ($queue.Count -gt 0) {
            $parent = $queue.Dequeue()
            foreach ($c in $all) {
                if ([uint32]$c.ParentProcessId -eq $parent -and -not $ids.Contains([uint32]$c.ProcessId)) {
                    $ids.Add([uint32]$c.ProcessId); $queue.Enqueue([uint32]$c.ProcessId)
                }
            }
        }
    } catch { }
    return ,$ids.ToArray()
}

# Mutes (volume 0 and muted) or restores (volume 1, unmuted) the audio sessions of the given process ids. Returns how many
# sessions it touched; 0 means none exists yet (the caller retries), -1 means no audio device. Never throws.
function Set-AppAudioMuted {
    param([uint32[]]$ProcessIds, [bool]$Muted = $true)
    try {
        Initialize-AppMute
        if ($Muted) { return [SkylineAppMute]::Apply($ProcessIds, 0.0, $true) }
        return [SkylineAppMute]::Apply($ProcessIds, 1.0, $false)
    } catch { return -1 }
}

# Dot-sourced: the functions are all the caller wants.
if ($MyInvocation.InvocationName -eq '.') { return }

$ErrorActionPreference = 'Stop'
if ($Check) {
    Initialize-AppMute
    if (-not ('SkylineAppMute' -as [type])) { throw 'self-test: the Core Audio type did not compile' }
    # a process id that cannot own a session: touches nothing, and must not throw
    $n = Set-AppAudioMuted -ProcessIds @([uint32]4294967295) -Muted $true
    if ($n -gt 0) { throw "self-test: muted $n sessions of a process that does not exist" }
    $tree = Get-ProcessTreeIds -RootId $PID
    if ($tree.Count -lt 1 -or $tree[0] -ne [uint32]$PID) { throw 'self-test: the process tree does not start with its root' }
    Write-Host ("self-test ok (audio sessions now: " + ([SkylineAppMute]::Describe() -replace "`n", '; ') + ')')
    exit 0
}
if ($List) { Initialize-AppMute; Write-Host ([SkylineAppMute]::Describe()); exit 0 }
if ($RestoreName) {
    $ids = @(Get-Process -Name $RestoreName -ErrorAction SilentlyContinue | ForEach-Object { [uint32]$_.Id })
    $n = if ($ids.Count -gt 0) { Set-AppAudioMuted -ProcessIds $ids -Muted $false } else { 0 }
    Write-Host "restored $n session(s) of '$RestoreName' ($($ids.Count) process(es))"
    exit 0
}
if ($ProcessId -le 0) { throw 'give -ProcessId <pid>, -List, -RestoreName <name> or -Check' }
$deadline = (Get-Date).AddSeconds($WaitSeconds)
$touched = 0
do {
    $ids = Get-ProcessTreeIds -RootId $ProcessId
    $touched = Set-AppAudioMuted -ProcessIds $ids -Muted (-not $Restore)
    if ($touched -gt 0) { break }
    if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) { break }
    Start-Sleep -Milliseconds 250
} while ((Get-Date) -lt $deadline)
Write-Host ("{0} {1} session(s) of process {2}" -f $(if ($Restore) { 'restored' } else { 'muted' }), $touched, $ProcessId)
exit $(if ($touched -gt 0) { 0 } else { 1 })
