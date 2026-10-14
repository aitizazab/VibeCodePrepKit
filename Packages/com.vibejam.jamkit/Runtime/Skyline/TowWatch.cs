using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The events of the repossession that the floor director turns into beats and spawns (task R2). It wraps a
    /// <see cref="Repossession"/> and adds the one event the objective itself does not raise: <see cref="LeftCircleMidTow"/>,
    /// the Unit leaving an asset's circle while that asset's tow is under way (the meter has started and is not done; the
    /// creator's line, 2026-10-14: Dot says "Don't go too far, you need to stay nearby to transmat!" once, the first time).
    /// <para>Mapping for the director: <see cref="MeterStarted"/> is <c>BeatTrigger.MeterStarted</c> and the alarm (the next
    /// wave is chosen round that asset: <see cref="AlarmAsset"/>); <see cref="Towed"/> is <c>BeatTrigger.AssetTowed</c>
    /// (<see cref="TowedCount"/> says whether it was the first tow: "Clamped! Get Moving!" ends the FIRST tow);
    /// <see cref="LeftCircleMidTow"/> is <c>BeatTrigger.LeftCircleMidTow</c> (it fires on every such leave; the teaching queue
    /// says a beat that does not repeat only once); <see cref="AllTowed"/> is <c>BeatTrigger.AllTowed</c> and the end of the
    /// waves. Events of one <see cref="Tick"/> fire in this order: MeterStarted, Towed, AllTowed, then LeftCircleMidTow (never for the asset that was just towed).</para>
    /// </summary>
    public sealed class TowWatch
    {
        readonly Repossession _r;
        int _prevActive = -1;
        int _leaveCount;
        Vector3? _alarm;

        public TowWatch(Repossession r)
        {
            _r = r ?? throw new ArgumentNullException(nameof(r));
            _r.MeterStarted += e => { _alarm = e.socket.position; MeterStarted?.Invoke(e); };
            _r.Towed += e => Towed?.Invoke(e);
            _r.AllTowedEvent += () => AllTowed?.Invoke();
        }

        public Repossession Objective => _r;

        /// <summary>The first tick an asset's meter moves (the alarm).</summary>
        public event Action<JobEntry> MeterStarted;
        /// <summary>The tick an asset's meter completes.</summary>
        public event Action<JobEntry> Towed;
        /// <summary>The Unit left the circle of an asset whose tow had started and not finished. Fires every time it happens.</summary>
        public event Action<JobEntry> LeftCircleMidTow;
        /// <summary>Once, when the third asset is towed.</summary>
        public event Action AllTowed;

        /// <summary>How many times <see cref="LeftCircleMidTow"/> has fired (a wreck does not clear it: the line is said once per floor).</summary>
        public int LeaveCount => _leaveCount;

        /// <summary>Assets towed so far (1 inside the first <see cref="Towed"/> event).</summary>
        public int TowedCount => _r.TowedCount;

        /// <summary>Where the last alarm was raised (the position of the asset whose meter started), until
        /// <see cref="ConsumeAlarm"/> takes it: the next wave's doors are chosen round it (<c>SpawnQuery.alarmAsset</c>).</summary>
        public Vector3? AlarmAsset => _alarm;

        /// <summary>Takes the pending alarm position (null when none): the wave that used it clears it.</summary>
        public Vector3? ConsumeAlarm()
        {
            Vector3? a = _alarm;
            _alarm = null;
            return a;
        }

        /// <summary>Advances the objective (<see cref="Repossession.Tick"/>) and raises <see cref="LeftCircleMidTow"/> when the
        /// circle that held the feet last tick no longer does, its meter had started and its asset is still there.</summary>
        public void Tick(float dt, Vector3 feet)
        {
            int before = _prevActive;
            _r.Tick(dt, feet);
            int now = _r.ActiveCircle;
            if (before >= 0 && now != before)
            {
                JobEntry e = _r.Sheet[before];
                if (!e.Towed && e.meter.Started)
                {
                    _leaveCount++;
                    LeftCircleMidTow?.Invoke(e);
                }
            }
            _prevActive = now;
        }

        /// <summary>A wreck: the objective resets (meters 0, same sockets), the pending alarm is dropped and no leave is
        /// reported for the respawn. <see cref="LeaveCount"/> stays.</summary>
        public void Reset()
        {
            _r.Reset();
            _prevActive = -1;
            _alarm = null;
        }
    }
}
