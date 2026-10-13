using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    public enum BotState { Idle, Alert, Telegraph, Recover, Wrecked }

    /// <summary>What the bot's face or display shows (2.5: "every enemy has a face or display").</summary>
    public enum BotFace { Happy, Alert, Angry, Hurt, Wrecked }

    public enum BotCueKind { State, Attack, Bark }

    public struct BotCue
    {
        public BotCueKind kind;
        /// <summary>State: the state entered.</summary>
        public BotState state;
        /// <summary>Bark: "spotted", "hurt", "lost" or "wrecked" (each bot's agent writes the lines and the babble voice).</summary>
        public string bark;
    }

    /// <summary>
    /// One bot's behaviour clock (docs/SKYLINE-DESIGN.md 2.5: idle, alert and attack states). Seeing the player (or being
    /// shot) alerts it; after <see cref="EnemyDef.alertSeconds"/> plus a seeded jitter of up to 0.4 s (so bots don't fire in
    /// lockstep) it cycles: a telegraph of <see cref="EnemyDef.telegraphSeconds"/>, the attack (a cue), then recovery, one
    /// cycle every <see cref="EnemyDef.fireInterval"/>. A started telegraph always finishes; out of sight for
    /// <see cref="EnemyDef.loseSightSeconds"/> it goes back to idle. Frame-rate independent: time past a deadline carries
    /// into the next state. A bot with <see cref="EnemyDef.rememberSeconds"/> (the drone) that sees the player again within
    /// that long of last seeing it skips the alert and goes straight to the telegraph.
    /// </summary>
    public sealed class BotBrain
    {
        public const float HurtSeconds = 0.25f;
        /// <summary>The most time a state change carries into the next state (s): enough that ordinary frame rates (20 fps and up)
        /// lose nothing, small enough that a hitch cannot burn a telegraph away or fire a burst of attacks.</summary>
        public const float MaxCarrySeconds = 0.05f;
        /// <summary>The most a remembering bot waits (s, seeded) between re-sighting the player and its telegraph.</summary>
        public const float RememberJitterSeconds = 0.15f;

        readonly EnemyDef _def;
        readonly Rng _rng;
        float _t, _unseen, _hurt, _alertFor, _sinceSeen = float.PositiveInfinity;

        public BotState State { get; private set; } = BotState.Idle;
        public float StateTime => _t;
        public BotFace Face =>
            State == BotState.Wrecked ? BotFace.Wrecked :
            _hurt > 0f ? BotFace.Hurt :
            State == BotState.Idle ? BotFace.Happy :
            State == BotState.Alert ? BotFace.Alert : BotFace.Angry;

        public BotBrain(EnemyDef def, ulong seed)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _rng = new Rng(seed);
        }

        public void Tick(float dt, bool seesPlayer, List<BotCue> cues)
        {
            if (State == BotState.Wrecked || !(dt > 0f) || float.IsInfinity(dt)) return;
            _t += dt;
            _hurt = Math.Max(0f, _hurt - dt);
            bool remembers = _def.rememberSeconds > 0f && _sinceSeen <= _def.rememberSeconds;
            _sinceSeen = seesPlayer ? 0f : _sinceSeen + dt;
            _unseen = seesPlayer ? 0f : _unseen + dt;
            bool lost = _unseen >= _def.loseSightSeconds;
            float recover = Math.Max(0f, _def.fireInterval - _def.telegraphSeconds);
            switch (State)
            {
                case BotState.Idle:
                    if (seesPlayer && remembers)
                    {
                        // it saw the player moments ago (rememberSeconds): no alert, only a seeded beat of up to RememberJitterSeconds
                        // before the telegraph, so a group regaining sight together doesn't fire in lockstep
                        _alertFor = _rng.Range(0f, RememberJitterSeconds);
                        Go(BotState.Alert, 0f, cues);
                        Bark("spotted", cues);
                    }
                    else if (seesPlayer) Alert(cues, "spotted");
                    break;
                case BotState.Alert:
                    if (lost) GiveUp(cues);
                    else if (_t >= _alertFor) Go(BotState.Telegraph, _t - _alertFor, cues);
                    break;
                case BotState.Telegraph:
                    if (_t >= _def.telegraphSeconds)
                    {
                        cues?.Add(new BotCue { kind = BotCueKind.Attack, state = BotState.Telegraph });
                        Go(BotState.Recover, _t - _def.telegraphSeconds, cues);
                    }
                    break;
                case BotState.Recover:
                    if (_t >= recover)
                    {
                        if (lost) GiveUp(cues);
                        else Go(BotState.Telegraph, _t - recover, cues);
                    }
                    break;
            }
        }

        /// <summary>A hit: the face flinches for <see cref="HurtSeconds"/>, and an idle bot wakes up.</summary>
        public void OnDamaged(List<BotCue> cues)
        {
            if (State == BotState.Wrecked) return;
            _hurt = HurtSeconds;
            _unseen = 0f;
            if (State == BotState.Idle) Alert(cues, "hurt");
        }

        public void OnWrecked(List<BotCue> cues)
        {
            if (State == BotState.Wrecked) return;
            Go(BotState.Wrecked, 0f, cues);
            Bark("wrecked", cues);
        }

        void Alert(List<BotCue> cues, string bark)
        {
            _alertFor = _def.alertSeconds + _rng.Range(0f, 0.4f);
            Go(BotState.Alert, 0f, cues);
            Bark(bark, cues);
        }

        void GiveUp(List<BotCue> cues)
        {
            Go(BotState.Idle, 0f, cues);
            Bark("lost", cues);
        }

        void Go(BotState s, float carry, List<BotCue> cues)
        {
            State = s;
            _t = carry > 0f ? Math.Min(MaxCarrySeconds, carry) : 0f;
            cues?.Add(new BotCue { kind = BotCueKind.State, state = s });
        }

        static void Bark(string id, List<BotCue> cues) => cues?.Add(new BotCue { kind = BotCueKind.Bark, bark = id });
    }

    /// <summary>The floor-care unit's attack (2.5: "ankle-height acid sweep"; CONCIERGE: "Guests are advised to jump").</summary>
    public static class AcidSweep
    {
        /// <summary>Whether the acid jet, <paramref name="t"/> seconds into a sweep, touches a player whose feet are at
        /// <paramref name="feet"/>. The jet sweeps <see cref="EnemyDef.sweepArcDeg"/> across the unit's facing, left to
        /// right, over <see cref="EnemyDef.sweepSeconds"/>, out to <see cref="EnemyDef.sweepRadius"/>, and is
        /// <see cref="EnemyDef.sweepWidthDeg"/> wide. Feet more than <see cref="EnemyDef.ankleHeight"/> above the floor clear it.</summary>
        public static bool Touches(Vector3 unit, float unitYawDeg, float t, Vector3 feet, float floorY, EnemyDef d)
        {
            if (d == null || !(t >= 0f) || t > d.sweepSeconds) return false;
            if (feet.Y - floorY > d.ankleHeight) return false;
            float dx = feet.X - unit.X, dz = feet.Z - unit.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            if (!(dist <= d.sweepRadius)) return false;
            if (dist < 0.3f) return true;   // standing on the nozzle
            float jet = unitYawDeg - d.sweepArcDeg * 0.5f + d.sweepArcDeg * (d.sweepSeconds > 0f ? t / d.sweepSeconds : 1f);
            float bearing = MathF.Atan2(dx, dz) * (180f / MathF.PI);
            return MathF.Abs(AimAssist.DeltaAngle(jet, bearing)) <= d.sweepWidthDeg * 0.5f;
        }
    }
}
