using System;

namespace JamKit.Movement
{
    /// <summary>The peek's numbers (docs/MOVEMENT.md "Peeking"). Metres, seconds, degrees.</summary>
    [Serializable]
    public sealed class PeekLeanDef
    {
        /// <summary>How far the eyes slide sideways at a full lean (m).</summary>
        public float distance = 0.4f;
        /// <summary>The view's roll toward the lean at a full lean (degrees; times the player's camera-roll and motion settings).</summary>
        public float roll = 4f;
        /// <summary>Seconds from upright to a full lean, and back (eased in and out).</summary>
        public float easeTime = 0.17f;
        /// <summary>The eyes stay at least this far from any wall (the radius of the sphere cast toward the lean, m).</summary>
        public float clearance = 0.18f;
        /// <summary>While leaning you only walk slowly: the stick counts for at most this share of a full push.</summary>
        public float walkShare = 0.5f;
        /// <summary>The first-person body's spine bends this far toward a full lean (degrees); the hips take the rest.</summary>
        public float bodyBend = 26f;
    }

    /// <summary>
    /// The peek: hold left or right to lean the eyes round a corner. Pure state, no engine: the caller says which side is
    /// held, whether a lean is allowed now (on the ground, not sprinting), and how much room there is beside the eyes on the
    /// side it probed (a sphere cast toward the lean point), and reads back the sideways offset and the roll. The lean eases
    /// in and out over <see cref="PeekLeanDef.easeTime"/>; the room cuts it at once (it never waits for the easing), so the
    /// eyes never pass into a wall. No allocation.
    /// </summary>
    public sealed class PeekLean
    {
        float _p; // -1 (full left) .. 1 (full right), linear in time

        /// <summary>The linear progress, -1..1 (tests).</summary>
        public float Progress => _p;

        /// <summary>-1 left, 0 upright, +1 right.</summary>
        public int Side => _p > 0f ? 1 : _p < 0f ? -1 : 0;

        /// <summary>True while the eyes are off the body's centre line at all.</summary>
        public bool Leaning => _p != 0f;

        /// <summary>The eased amount, -1..1 (full left .. full right), already cut by the room.</summary>
        public float Amount => _p >= 0f ? Ease(_p) : -Ease(-_p);

        /// <summary>The sideways offset of the eyes (m, + right).</summary>
        public float Offset(PeekLeanDef def) => Amount * def.distance;

        /// <summary>The view's roll toward the lean (degrees, + right), before the comfort settings.</summary>
        public float Roll(PeekLeanDef def) => Amount * def.roll;

        /// <summary>The side to probe for room this frame: the side the eyes are on, else the side wanted (0: none).</summary>
        public int ProbeSide(int want) => _p != 0f ? Side : Math.Sign(want);

        /// <summary>Back upright at once (a teleport, a cutscene).</summary>
        public void Reset() => _p = 0f;

        /// <summary>
        /// One frame. <paramref name="want"/>: -1 left held, +1 right held, 0 neither (or both). <paramref name="allowed"/>
        /// false (in the air, sprinting, sliding) eases back upright. <paramref name="roomSide"/> and <paramref name="room"/>:
        /// the side that was probed (<see cref="ProbeSide"/>) and the free metres beside the eyes on it.
        /// </summary>
        public void Tick(float dt, int want, bool allowed, int roomSide, float room, PeekLeanDef def)
        {
            float goal = allowed ? Math.Sign(want) : 0f;
            float step = dt / Math.Max(0.01f, def.easeTime);
            if (_p < goal) _p = Math.Min(goal, _p + step);
            else if (_p > goal) _p = Math.Max(goal, _p - step);
            if (_p == 0f) return;
            // A side that wasn't probed this frame (it crossed over in one long frame): stay upright until it is.
            if (Side != roomSide) { _p = 0f; return; }
            float most = def.distance > 1e-4f ? Clamp01(room / def.distance) : 0f;
            float now = Ease(Math.Abs(_p));
            if (now > most) _p = Side * InverseEase(most);
        }

        /// <summary>True when a lean may start or hold: on the ground (standing, crouched or walking), not sprinting. A jump,
        /// a slide, a wallrun, a mantle or a sprint lets it ease back.</summary>
        public static bool Allowed(MoveMode mode, bool sprinting) => mode == MoveMode.Grounded && !sprinting;

        /// <summary>The stick, limited to a slow walk while leaning (<see cref="PeekLeanDef.walkShare"/> of a full push, where a
        /// full push is <paramref name="fullAt"/> of the stick's travel).</summary>
        public static System.Numerics.Vector2 LimitMove(System.Numerics.Vector2 move, float fullAt, PeekLeanDef def)
        {
            float most = Math.Max(0.05f, fullAt) * def.walkShare;
            float len = move.Length();
            return len > most && len > 1e-5f ? move * (most / len) : move;
        }

        /// <summary>Ease in and out (a half cosine): 0, 1 at the ends, flat at both.</summary>
        public static float Ease(float x)
        {
            x = Clamp01(x);
            return 0.5f - 0.5f * (float)Math.Cos(Math.PI * x);
        }

        public static float InverseEase(float y)
        {
            y = Clamp01(y);
            return (float)(Math.Acos(1.0 - 2.0 * y) / Math.PI);
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
