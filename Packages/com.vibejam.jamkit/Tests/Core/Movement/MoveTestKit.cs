using System;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Tests.Movement
{
    /// <summary>Small helpers for driving a <see cref="MoveSim"/> step by step in tests.</summary>
    public static class MoveTestKit
    {
        public const float H = 1f / 120f;

        /// <summary>Forward at a run: sprint held (sprint is opt-in since MoveDef.autoSprint went off).</summary>
        public static MoveInput Forward(float yaw = 0f) => new MoveInput { move = new Vector2(0f, 1f), yaw = yaw, sprintHeld = true };

        public static MoveInput With(this MoveInput i, bool jump = false, bool crouchHeld = false, bool crouchPressed = false)
        {
            i.jumpPressed = jump;
            i.jumpHeld = jump;
            i.crouchHeld = crouchHeld || crouchPressed;
            i.crouchPressed = crouchPressed;
            return i;
        }

        /// <summary>Steps until <paramref name="until"/> is true or the time runs out. Returns the seconds taken.</summary>
        public static float Run(MoveSim sim, MoveInput input, float maxSeconds, Func<MoveSim, bool> until = null)
        {
            float t = 0f;
            var first = input;
            var rest = input;
            rest.jumpPressed = false;
            rest.crouchPressed = false;
            bool started = false;
            while (t < maxSeconds - 1e-6f)
            {
                sim.Step(started ? rest : first, H);
                started = true;
                t += H;
                if (until != null && until(sim)) break;
            }
            return t;
        }

        /// <summary>A flat floor from z0 to z1 (x -10..10) with its top at y 0.</summary>
        public static BoxWorld Flat(float z0 = -200f, float z1 = 200f)
        {
            var w = new BoxWorld();
            w.Floor(-10f, 10f, z0, z1, 0f);
            return w;
        }

        public static MoveSim Standing(BoxWorld w, Vector3 at, MoveDef def = null, float yaw = 0f, Vector3 velocity = default)
        {
            var sim = new MoveSim(w, def ?? new MoveDef());
            sim.Teleport(at, yaw, velocity);
            return sim;
        }
    }
}
