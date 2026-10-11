using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Stealth
{
    /// <summary>
    /// What being caught costs, chosen per encounter (docs/STEALTH.md "Being caught"). The game has no combat, so a guard
    /// who finds her ends the attempt one of two ways: <see cref="RestartCheckpoint"/> (fade out, back to the last
    /// checkpoint, the guards back on their rounds, anything picked up since the checkpoint put back) or
    /// <see cref="FailSubquest"/> (the subquest this encounter belongs to fails; the main story goes on).
    /// </summary>
    public enum CaughtOutcome { RestartCheckpoint, FailSubquest }

    /// <summary>One checkpoint: a trigger box (feet inside it reach it) and the spawn point a restart puts her at.</summary>
    public sealed class StealthCheckpoint
    {
        public string name = "";
        public Vector3 center, size = new Vector3(4f, 3f, 4f);
        public Vector3 spawn;
        public float yaw;

        public bool Contains(Vector3 feet)
        {
            var d = feet - center;
            return System.Math.Abs(d.X) <= size.X * 0.5f && System.Math.Abs(d.Y) <= size.Y * 0.5f && System.Math.Abs(d.Z) <= size.Z * 0.5f;
        }
    }

    /// <summary>
    /// The checkpoints of an encounter: which one she reached last (the spawn point a restart uses) and what she had
    /// collected when she reached it. Entering a different checkpoint's box while no guard is chasing her makes it the
    /// current one; a checkpoint is never taken mid-chase, so a restart never drops her back into the chase. Index 0 is the
    /// start. No allocation after the first <see cref="Reset"/>.
    /// </summary>
    public sealed class CheckpointLog
    {
        public readonly List<StealthCheckpoint> points = new List<StealthCheckpoint>();
        readonly List<bool> _banked = new List<bool>();
        int _current;

        public int Current => _current;
        public StealthCheckpoint CurrentPoint => points.Count > 0 ? points[_current] : null;

        /// <summary>Back to the start (checkpoint 0), with what <paramref name="items"/> holds now banked.</summary>
        public void Reset(CollectibleSet items)
        {
            _current = 0;
            Bank(items);
        }

        /// <summary>One frame: the index of a checkpoint reached this frame, or -1.</summary>
        public int Tick(Vector3 feet, bool chased, CollectibleSet items)
        {
            if (chased) return -1;
            for (int i = 0; i < points.Count; i++)
            {
                if (i == _current || !points[i].Contains(feet)) continue;
                _current = i;
                Bank(items);
                return i;
            }
            return -1;
        }

        /// <summary>She had collectible <paramref name="index"/> when she reached the current checkpoint.</summary>
        public bool Banked(int index) => index >= 0 && index < _banked.Count && _banked[index];

        /// <summary>Puts back everything collected since the current checkpoint; returns how many.</summary>
        public int Restore(CollectibleSet items)
        {
            if (items == null) return 0;
            int n = 0;
            for (int i = 0; i < items.Total; i++)
                if (items.IsCollected(i) && !Banked(i) && items.Uncollect(i)) n++;
            return n;
        }

        void Bank(CollectibleSet items)
        {
            _banked.Clear();
            if (items == null) return;
            for (int i = 0; i < items.Total; i++) _banked.Add(items.IsCollected(i));
        }
    }

    public enum CaughtStep { None, Black, Failed, Done }

    /// <summary>
    /// The caught moment, as a clock: a short beat (the sting plays, everyone freezes), then either the fade out, the
    /// restart at full black (<see cref="CaughtStep.Black"/>) and the fade back in (<see cref="CaughtStep.Done"/>), or, for
    /// <see cref="CaughtOutcome.FailSubquest"/>, the fail message after the beat (<see cref="CaughtStep.Failed"/>). The
    /// game plays the sound, shows the words and does the restart; this only says when. Seconds of game time.
    /// </summary>
    public sealed class CaughtBeat
    {
        public float beat = 0.8f, fadeOut = 0.35f, fadeIn = 0.45f;

        CaughtOutcome _outcome;
        float _t, _blackAt;
        bool _active, _blacked;

        public bool Active => _active;
        public CaughtOutcome Outcome => _outcome;

        /// <summary>Screen fade, 0 clear to 1 black (a restart only).</summary>
        public float Fade
        {
            get
            {
                if (!_active || _outcome != CaughtOutcome.RestartCheckpoint || _t <= beat) return 0f;
                // Full black from the restart's frame (so the teleport is never seen), then back in.
                if (!_blacked) return System.Math.Min(1f, (_t - beat) / System.Math.Max(0.001f, fadeOut));
                return System.Math.Max(0f, 1f - (_t - _blackAt) / System.Math.Max(0.001f, fadeIn));
            }
        }

        public void Start(CaughtOutcome outcome)
        {
            _outcome = outcome;
            _t = 0f;
            _active = true;
            _blacked = false;
        }

        public void Cancel() => _active = false;

        public CaughtStep Tick(float dt)
        {
            if (!_active) return CaughtStep.None;
            _t += dt;
            if (_outcome == CaughtOutcome.FailSubquest)
            {
                if (_t < beat) return CaughtStep.None;
                _active = false;
                return CaughtStep.Failed;
            }
            if (!_blacked)
            {
                if (_t < beat + fadeOut) return CaughtStep.None;
                _blacked = true;
                _blackAt = _t;
                return CaughtStep.Black;
            }
            if (_t < _blackAt + fadeIn) return CaughtStep.None;
            _active = false;
            return CaughtStep.Done;
        }
    }
}
