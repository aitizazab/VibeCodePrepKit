using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Combat;

namespace JamKit.Skyline
{
    /// <summary>One Skyline gun as data (docs/SKYLINE-DESIGN.md 2.2-2.3): its numbers, its feel and its handling set. The
    /// reload times ARE the reload clips' lengths, so balance, animation and sound share one number.</summary>
    public sealed class GunDef
    {
        public const int ClipKinds = 6;

        public string id = "";
        public string displayName = "";
        /// <summary>Magazine, fire mode and fire interval. <see cref="SkylineGun"/> copies it and sets reloadTime (from the
        /// clips), autoReloadOnEmpty off (a dry fire comes first) and infiniteReserve on, except for a <see cref="special"/> gun.</summary>
        public WeaponDef weapon = new WeaponDef();
        /// <summary>A special weapon (the Breacher and later ones; docs/SKYLINE-HUD.md "Ammo") has a finite reserve fed by
        /// <see cref="SpecialAmmoLedger"/>; a primary weapon (the Writ) has an infinite one.</summary>
        public bool special;
        /// <summary>Damage per bullet (per pellet for a shotgun) before falloff and multipliers.</summary>
        public float damage = 10f;
        public int pellets = 1;
        /// <summary>Half-angle (degrees) of the cone the pellets spread over; 0 is dead accurate.</summary>
        public float spreadDeg;
        public float range = 80f;
        /// <summary>Full damage up to falloffStart (m), falling linearly to falloffMinScale at falloffEnd and beyond.</summary>
        public float falloffStart = 20f, falloffEnd = 50f, falloffMinScale = 0.5f;
        /// <summary>A hit on a bot's weak point (its lens or display: a robot's head) multiplies the damage by this.</summary>
        public float weakPointMultiplier = 1.5f;
        /// <summary>m/s a hit pushes a bot (the Breacher's identity); 0 for none.</summary>
        public float enemyKnockback;
        /// <summary>A dry fire starts the from-empty reload when its clip ends.</summary>
        public bool reloadAfterDryFire = true;
        public float hitStopSeconds = 0.03f, killHitStopSeconds = 0.08f;
        /// <summary>Screen-shake trauma added per shot fired (0..1; Combat/ScreenShake).</summary>
        public float shakePerShot = 0.05f;
        public CameraKickDef kick = new CameraKickDef();
        public AimAssistDef aimAssist = new AimAssistDef();
        /// <summary>Aim down sights (hold Aim; the pause-menu plan, section 3), as scales at full aim: the field of view of the
        /// main and viewmodel cameras, look sensitivity, move speed, and the pellet spread. 1 is no change.</summary>
        public float aimFovScale = 1f, aimLookScale = 1f, aimMoveScale = 1f, aimSpreadScale = 1f;
        /// <summary>Indexed by (int)HandlingClipKind; every kind is required.</summary>
        public HandlingClip[] clips = new HandlingClip[ClipKinds];

        public HandlingClip Clip(HandlingClipKind kind) => clips != null && (int)kind < clips.Length ? clips[(int)kind] : null;

        public float FalloffScale(float distance)
        {
            if (!(distance > falloffStart)) return 1f;
            if (distance >= falloffEnd || falloffEnd <= falloffStart) return falloffMinScale;
            float t = (distance - falloffStart) / (falloffEnd - falloffStart);
            return 1f + (falloffMinScale - 1f) * t;
        }

        /// <summary>Null when the gun is usable; otherwise what's wrong, naming the gun and the clip.</summary>
        public string Validate()
        {
            string name = string.IsNullOrEmpty(id) ? "gun" : id;
            if (weapon == null) return name + ": no weapon numbers";
            if (!(weapon.fireInterval > 0f)) return name + ": fireInterval must be > 0";
            if (weapon.magazineSize < 1) return name + ": magazineSize must be at least 1";
            if (pellets < 1) return name + ": pellets must be at least 1";
            if (!(damage > 0f)) return name + ": damage must be > 0";
            if (!(aimFovScale >= 0.5f && aimFovScale <= 1f)) return name + ": aimFovScale must be 0.5 to 1";
            if (!(aimLookScale >= 0.3f && aimLookScale <= 1f)) return name + ": aimLookScale must be 0.3 to 1";
            if (!(aimMoveScale >= 0.5f && aimMoveScale <= 1f)) return name + ": aimMoveScale must be 0.5 to 1";
            if (!(aimSpreadScale >= 0.5f && aimSpreadScale <= 1f)) return name + ": aimSpreadScale must be 0.5 to 1";
            if (clips == null || clips.Length != ClipKinds) return $"{name}: clips must have one entry per HandlingClipKind ({ClipKinds})";
            for (int k = 0; k < ClipKinds; k++)
            {
                var c = clips[k];
                if (c == null) return $"{name}: missing the {(HandlingClipKind)k} clip";
                if (c.kind != (HandlingClipKind)k) return $"{name}: clip {k} is a {c.kind}, expected {(HandlingClipKind)k}";
                string bad = c.Validate();
                if (bad != null) return name + " " + bad;
            }
            return null;
        }
    }

    public enum GunCueKind { ClipStarted, Event, Shot }

    /// <summary>What the gun did this tick, for the viewmodel, the sound and the shooting code.</summary>
    public struct GunCue
    {
        public GunCueKind kind;
        /// <summary>The clip started (ClipStarted), or the clip the event belongs to (Event).</summary>
        public HandlingClipKind clip;
        /// <summary>ClipStarted: its length after the speed scale (s).</summary>
        public float seconds;
        /// <summary>Event: the event (its sound plays now).</summary>
        public HandlingEvent ev;
        /// <summary>Shot: how long before the end of this tick it was fired (s), as WeaponState reports.</summary>
        public float age;
        /// <summary>ClipStarted: the equip a <see cref="Loadout"/> made when it was built (not a swap).</summary>
        public bool initial;
    }

    /// <summary>
    /// A Skyline gun in hand: WeaponState's ammo, fire timing and reloads, plus the handling set (2.3). Equip blocks firing
    /// until its clip ends; each shot plays the fire clip (a new shot restarts it); an empty magazine dry fires and, when
    /// the click's clip ends, reloads from empty; a manual reload with rounds left is the partial reload; inspect plays only
    /// when idle and firing or reloading cancels it. Reload times come from the reload clips, divided by
    /// <see cref="ReloadSpeed"/>, and the clips' events rescale with them. Call <see cref="Tick"/> once per frame and read
    /// the cues it appends.
    /// </summary>
    public sealed class SkylineGun
    {
        readonly WeaponState _state;
        readonly WeaponDef _weapon;
        readonly HandlingPlayer _handling = new HandlingPlayer();
        readonly List<HandlingEvent> _fired = new List<HandlingEvent>();
        readonly List<float> _ages = new List<float>();
        List<GunCue> _cues;
        bool _equipping, _inspecting, _reloadAfterDry;
        float _reloadSpeed = 1f;

        public GunDef Def { get; }
        public WeaponState State => _state;
        public HandlingPlayer Handling => _handling;
        public bool Equipped { get; private set; }
        public bool Equipping => _equipping;
        public bool Inspecting => _inspecting;
        public int Ammo => _state.AmmoInMagazine;

        /// <summary>1 = the clips' own lengths; 1.25 reloads 25% faster (its events move with it).</summary>
        public float ReloadSpeed
        {
            get => _reloadSpeed;
            set => _reloadSpeed = value > 0.01f ? value : 0.01f;
        }

        public SkylineGun(GunDef def)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            string bad = def.Validate();
            if (bad != null) throw new ArgumentException(bad, nameof(def));
            _weapon = Copy(def.weapon);
            _weapon.autoReloadOnEmpty = false;
            _weapon.infiniteReserve = !def.special;
            if (def.special) _weapon.startingReserve = 0;   // the SpecialAmmoLedger feeds a special gun; WeaponDef defaults to 48
            _state = new WeaponState(_weapon);
            _state.Fired += OnFired;
            _state.DryFired += OnDryFired;
            _state.ReloadStarted += OnReloadStarted;
            _state.ReloadCanceled += OnReloadCanceled;
        }

        static WeaponDef Copy(WeaponDef w) => new WeaponDef
        {
            magazineSize = w.magazineSize, startingReserve = w.startingReserve, maxReserve = w.maxReserve,
            infiniteReserve = w.infiniteReserve, ammoPerShot = w.ammoPerShot, fireMode = w.fireMode,
            fireInterval = w.fireInterval, burstCount = w.burstCount, burstInterval = w.burstInterval,
            reloadTime = w.reloadTime, autoReloadOnEmpty = w.autoReloadOnEmpty,
        };

        /// <summary>The reload's length now: the from-empty or partial clip, divided by <see cref="ReloadSpeed"/>.</summary>
        public float ReloadSeconds(bool fromEmpty) =>
            Def.Clip(fromEmpty ? HandlingClipKind.ReloadEmpty : HandlingClipKind.ReloadPartial).seconds / _reloadSpeed;

        /// <summary>Takes the gun out: plays the equip clip; it can't fire until the clip ends.</summary>
        public void Equip(List<GunCue> cues)
        {
            _cues = cues;
            if (_state.IsReloading) _state.CancelReload();
            Equipped = true;
            _inspecting = false;
            _reloadAfterDry = false;
            _equipping = true;
            Start(HandlingClipKind.Equip, 1f);
            Flush(0f);
            _cues = null;
        }

        /// <summary>Puts it away at once (the swap's other half): a reload in progress is cancelled and no further events of
        /// the current clip fire.</summary>
        public void Holster()
        {
            if (_state.IsReloading) _state.CancelReload();
            _handling.Stop();
            Equipped = false;
            _equipping = false;
            _inspecting = false;
            _reloadAfterDry = false;
        }

        /// <summary>Manual reload. False when the magazine is full, a reload is running, or the gun is out or equipping.</summary>
        public bool Reload(List<GunCue> cues)
        {
            if (!Equipped || _equipping) return false;
            _cues = cues;
            bool ok = BeginReload();
            _cues = null;
            return ok;
        }

        /// <summary>Plays the inspect clip when the gun is idle (equipped, not equipping or reloading, no clip playing).</summary>
        public bool Inspect(List<GunCue> cues)
        {
            if (!Equipped || _equipping || _state.IsReloading || _handling.Playing) return false;
            _cues = cues;
            _inspecting = true;
            Start(HandlingClipKind.Inspect, 1f);
            Flush(0f);
            _cues = null;
            return true;
        }

        public void Tick(float dt, bool triggerHeld, bool triggerPressed, List<GunCue> cues)
        {
            if (!Equipped) return;
            if (!(dt > 0f) || float.IsInfinity(dt)) dt = 0f;
            _cues = cues;
            Flush(dt);
            if (_equipping && !_handling.Playing) _equipping = false;
            bool canFire = !_equipping;
            if (_inspecting && canFire && (triggerHeld || triggerPressed))
            {
                _inspecting = false;
                _handling.Stop();
            }
            if (_inspecting && !_handling.Playing) _inspecting = false;
            _state.Tick(dt, canFire && triggerHeld, canFire && triggerPressed, _ages);
            for (int i = 0; i < _ages.Count; i++)
                Add(new GunCue { kind = GunCueKind.Shot, clip = HandlingClipKind.Fire, age = _ages[i] });
            // After WeaponState's tick, so its reload and the reload clip start on the same clock.
            if (_reloadAfterDry && !_handling.Playing && !_state.IsReloading)
            {
                _reloadAfterDry = false;
                BeginReload();
            }
            Flush(0f);   // the events at 0 of a clip started during this tick
            _cues = null;
        }

        bool BeginReload()
        {
            _weapon.reloadTime = ReloadSeconds(_state.AmmoInMagazine == 0);
            if (!_state.StartReload()) return false;
            _inspecting = false;
            Flush(0f);
            return true;
        }

        void OnFired()
        {
            _inspecting = false;
            Start(HandlingClipKind.Fire, 1f);
        }

        void OnDryFired()
        {
            Start(HandlingClipKind.DryFire, 1f);
            if (Def.reloadAfterDryFire) _reloadAfterDry = true;
        }

        void OnReloadStarted()
        {
            Start(_state.AmmoInMagazine == 0 ? HandlingClipKind.ReloadEmpty : HandlingClipKind.ReloadPartial, _reloadSpeed);
        }

        void OnReloadCanceled()
        {
            var c = _handling.Clip;
            if (c != null && (c.kind == HandlingClipKind.ReloadEmpty || c.kind == HandlingClipKind.ReloadPartial)) _handling.Stop();
        }

        void Start(HandlingClipKind kind, float speed)
        {
            _handling.Play(Def.Clip(kind), speed);
            Add(new GunCue { kind = GunCueKind.ClipStarted, clip = kind, seconds = _handling.Duration });
            Flush(0f);   // the events at 0 fire now: several shots in one tick each restart the clip and must each be heard
        }

        void Flush(float dt)
        {
            _fired.Clear();
            _handling.Advance(dt, _fired);
            if (_fired.Count == 0) return;
            var kind = _handling.Clip.kind;
            for (int i = 0; i < _fired.Count; i++) Add(new GunCue { kind = GunCueKind.Event, clip = kind, ev = _fired[i] });
        }

        void Add(GunCue c) => _cues?.Add(c);
    }

    /// <summary>Two guns (2.1); swapping is a designed moment: the other gun's equip clip.</summary>
    public sealed class Loadout
    {
        readonly SkylineGun[] _guns;

        public int ActiveIndex { get; private set; }
        public SkylineGun Active => _guns[ActiveIndex];
        public SkylineGun this[int slot] => _guns[slot];
        public int Count => _guns.Length;

        /// <summary>Holds <paramref name="first"/> (equipped now) and <paramref name="second"/>.</summary>
        public Loadout(SkylineGun first, SkylineGun second, List<GunCue> cues)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            _guns = new[] { first, second };
            int from = cues == null ? 0 : cues.Count;
            first.Equip(cues);
            if (cues != null)
                for (int i = from; i < cues.Count; i++)
                {
                    var c = cues[i];
                    if (c.kind == GunCueKind.ClipStarted && c.clip == HandlingClipKind.Equip) { c.initial = true; cues[i] = c; }
                }
        }

        public void Swap(List<GunCue> cues)
        {
            Active.Holster();
            ActiveIndex = 1 - ActiveIndex;
            Active.Equip(cues);
        }
    }

    /// <summary>Gun cues as decision events (4.1, recorded from M1 on for Baja).</summary>
    public static class GunDecisions
    {
        /// <summary>A shot is Fire, a reload clip starting is Reload, an equip clip starting is Swap (except the loadout's first equip); other cues add nothing.
        /// <paramref name="slot"/> is the loadout slot (the decision's subject). Returns how many were added.</summary>
        public static int Append(DecisionLog log, in GunCue cue, int slot, float time, Vector3 position, DecisionContext context,
            float threatBearing, int ammo)
        {
            if (log == null) return 0;
            DecisionKind kind;
            if (cue.kind == GunCueKind.Shot) kind = DecisionKind.Fire;
            else if (cue.kind == GunCueKind.ClipStarted && (cue.clip == HandlingClipKind.ReloadPartial || cue.clip == HandlingClipKind.ReloadEmpty)) kind = DecisionKind.Reload;
            else if (cue.kind == GunCueKind.ClipStarted && cue.clip == HandlingClipKind.Equip && !cue.initial) kind = DecisionKind.Swap;
            else return 0;
            log.Add(new DecisionEvent
            {
                time = time, kind = kind, subject = slot, context = context, threatBearing = threatBearing, ammo = ammo, position = position,
            });
            return 1;
        }
    }
}
