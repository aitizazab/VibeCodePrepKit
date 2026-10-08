using System;
using System.Collections.Generic;

namespace JamKit.Combat
{
    /// <summary>
    /// One gun's live state (ammo, cooldown, burst, reload) driven by a <see cref="WeaponDef"/>.
    /// Call <see cref="Tick(float, bool, bool)"/> once per frame with the trigger input; it returns
    /// how many shots to spawn. Shots are timed inside the frame, so auto fire is exactly as fast at
    /// 30 fps as at 240 fps, and a long frame fires several shots instead of losing them.
    /// </summary>
    public sealed class WeaponState
    {
        /// <summary>Absorbs float error in summed frame times so shots land on their exact beat.</summary>
        const double Epsilon = 1e-6;
        /// <summary>Floor for intervals, so a zero in the data can't spin the fire loop forever.</summary>
        const float MinInterval = 0.001f;

        double _clock;
        double _nextShotAt;
        double _engagedAt;
        double _reloadStartAt;
        double _reloadEndAt;
        int _burstLeft;
        bool _heldLast;

        public WeaponDef Def { get; }
        public int AmmoInMagazine { get; private set; }
        public int Reserve { get; private set; }
        public bool IsReloading { get; private set; }
        public bool IsBursting => _burstLeft > 0;
        public bool HasMagazine => Def.magazineSize > 0;

        /// <summary>0 at the start of a reload, 1 when done; 0 when not reloading. For reload bars.</summary>
        public float ReloadProgress01
        {
            get
            {
                if (!IsReloading) return 0f;
                double length = _reloadEndAt - _reloadStartAt;
                if (length <= 0.0) return 1f;
                return (float)Math.Min(1.0, Math.Max(0.0, (_clock - _reloadStartAt) / length));
            }
        }

        /// <summary>Enough ammo (in the magazine, or the reserve for magazine-less guns) for one shot.</summary>
        public bool HasAmmoForShot
        {
            get
            {
                int cost = ShotCost;
                if (HasMagazine) return AmmoInMagazine >= cost;
                return Def.infiniteReserve || Reserve >= cost;
            }
        }

        public bool CanReload =>
            HasMagazine && !IsReloading && AmmoInMagazine < Def.magazineSize && (Def.infiniteReserve || Reserve > 0);

        /// <summary>Raised once per shot, in order, during Tick.</summary>
        public event Action Fired;
        /// <summary>Trigger pressed with no ammo and no reload possible: play the empty click.</summary>
        public event Action DryFired;
        public event Action ReloadStarted;
        public event Action ReloadCompleted;
        public event Action ReloadCanceled;

        public WeaponState(WeaponDef def)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            AmmoInMagazine = HasMagazine ? def.magazineSize : 0;
            Reserve = Math.Max(0, def.startingReserve);
        }

        int ShotCost => Math.Max(0, Def.ammoPerShot);
        float FireInterval => Math.Max(MinInterval, Def.fireInterval);
        float BurstInterval => Math.Max(MinInterval, Def.burstInterval);

        /// <summary>Advances the gun by <paramref name="dt"/> and fires as the input allows.
        /// <paramref name="triggerHeld"/>: the button is down. <paramref name="triggerPressed"/>: it went
        /// down this frame (derived from held when not passed). Returns shots fired this tick.</summary>
        public int Tick(float dt, bool triggerHeld, bool triggerPressed = false) =>
            Tick(dt, triggerHeld, triggerPressed, null);

        /// <summary>As <see cref="Tick(float, bool, bool)"/>, and fills <paramref name="shotAges"/>
        /// (cleared first) with how long before the end of this tick each shot was fired. Advance each
        /// projectile by its age so streams stay evenly spaced at low frame rates.</summary>
        public int Tick(float dt, bool triggerHeld, bool triggerPressed, List<float> shotAges)
        {
            shotAges?.Clear();
            if (!(dt > 0f)) dt = 0f;
            double now = _clock;
            double end = _clock + dt;
            _clock = end;

            bool fresh = triggerPressed || (triggerHeld && !_heldLast);
            _heldLast = triggerHeld;
            if (fresh) _engagedAt = end;
            bool engaged = triggerHeld || triggerPressed;
            bool pressPending = fresh;
            int shots = 0;

            while (true)
            {
                if (IsReloading)
                {
                    if (_reloadEndAt > end + Epsilon) break;
                    if (_reloadEndAt > now) now = _reloadEndAt;
                    FinishReload();
                }
                if (Def.autoReloadOnEmpty && !HasAmmoForShot && BeginReload(now)) continue;

                bool burstShot = _burstLeft > 0;
                double t;
                if (burstShot) t = _nextShotAt;
                else if (Def.fireMode == FireMode.Auto && engaged) t = Math.Max(_nextShotAt, _engagedAt);
                else if (pressPending)
                {
                    if (_nextShotAt > end + Epsilon) break; // pressed before the gun was ready: dropped
                    t = end;
                }
                else break;
                if (t < now) t = now;
                if (t > end + Epsilon) break;

                if (!HasAmmoForShot)
                {
                    _burstLeft = 0;
                    if (pressPending) DryFired?.Invoke();
                    break;
                }

                pressPending = false;
                ConsumeShot();
                shots++;
                shotAges?.Add((float)Math.Max(0.0, end - t));
                now = t;
                if (burstShot) _burstLeft--;
                else if (Def.fireMode == FireMode.Burst) _burstLeft = Math.Max(1, Def.burstCount) - 1;
                if (_burstLeft > 0 && !HasAmmoForShot) _burstLeft = 0; // ran dry mid-burst
                _nextShotAt = t + (_burstLeft > 0 ? BurstInterval : FireInterval);
                Fired?.Invoke();
            }
            return shots;
        }

        /// <summary>Manual reload (the R key). Cancels a burst in progress. Returns false if a reload
        /// isn't possible (full, no reserve, already reloading, no magazine).</summary>
        public bool StartReload() => BeginReload(_clock);

        /// <summary>Abandons a reload without moving any ammo (weapon swap, sprint). With
        /// autoReloadOnEmpty an empty gun starts reloading again on its next Tick.</summary>
        public bool CancelReload()
        {
            if (!IsReloading) return false;
            IsReloading = false;
            ReloadCanceled?.Invoke();
            return true;
        }

        /// <summary>Ammo pickup: adds to the reserve up to maxReserve. Returns how much was taken,
        /// so a pickup can stay on the floor when the player is full.</summary>
        public int AddReserve(int amount)
        {
            if (amount <= 0 || Def.infiniteReserve) return 0;
            int room = Def.maxReserve > 0 ? Math.Max(0, Def.maxReserve - Reserve) : int.MaxValue - Reserve;
            int added = Math.Min(amount, room);
            Reserve += added;
            return added;
        }

        /// <summary>Overwrites the reserve (never below 0, ignoring maxReserve). For games that keep ammo
        /// outside the gun: shared pools by ammo type feed the reserve before Tick and read it back after.</summary>
        public void SetReserve(int amount)
        {
            Reserve = Math.Max(0, amount);
        }

        void ConsumeShot()
        {
            int cost = ShotCost;
            if (HasMagazine) AmmoInMagazine -= cost;
            else if (!Def.infiniteReserve) Reserve -= cost;
        }

        bool BeginReload(double at)
        {
            if (!CanReload) return false;
            _burstLeft = 0;
            IsReloading = true;
            _reloadStartAt = at;
            _reloadEndAt = at + Math.Max(0f, Def.reloadTime);
            ReloadStarted?.Invoke();
            return true;
        }

        void FinishReload()
        {
            IsReloading = false;
            int need = Math.Max(0, Def.magazineSize - AmmoInMagazine);
            int take = Def.infiniteReserve ? need : Math.Min(need, Reserve);
            AmmoInMagazine += take;
            if (!Def.infiniteReserve) Reserve -= take;
            if (_reloadEndAt > _nextShotAt) _nextShotAt = _reloadEndAt;
            ReloadCompleted?.Invoke();
        }
    }
}
