using System;
using System.Numerics;

namespace JamKit.DreamEscape
{
    /// <summary>The rain's tunable numbers (docs/DREAM-ESCAPE.md "The umbrella and the rain"). The cost of going on
    /// without the umbrella is a placeholder for the creator to decide: modest and readable.</summary>
    [Serializable]
    public sealed class DreamRainDef
    {
        /// <summary>Seconds in the rain, uncovered, to be soaked through (soak 0 to 1).</summary>
        public float soakTime = 5f;
        /// <summary>How fast the soak fades under the umbrella or back indoors (per second).</summary>
        public float dryRate = 0.6f;
        /// <summary>Soaked through, she runs and sprints at this share of her speed (heavier).</summary>
        public float soakedSpeed = 0.72f;
        /// <summary>Soaked through, her jumps reach this share of their height.</summary>
        public float soakedJump = 0.8f;
        /// <summary>Soaked through, the screen's edges darken this much (0..1).</summary>
        public float soakedVignette = 0.75f;
    }

    /// <summary>What happened in one <see cref="DreamRain.Tick"/>.</summary>
    public struct DreamRainEvents
    {
        public bool enteredRain, leftRain;
        /// <summary>She went on into the rain without the umbrella, past the point of no return: the Despair route.</summary>
        public bool despairChosen;
    }

    /// <summary>
    /// The umbrella and the rain at the escape's front door, pure: the umbrella waits by the door until she takes it
    /// with the dream tool (<see cref="PickUp"/>); outside, rain soaks her unless it's open (the soak slows her and
    /// darkens the view); going on past <see cref="DreamEscapeCourse.despairS"/> without it is the Despair choice, made
    /// once per run (the game keeps it in the save). <see cref="Reset"/> puts the umbrella back (a new run, or a place
    /// before the porch). No allocation per tick.
    /// </summary>
    public sealed class DreamRain
    {
        public const string DespairFlag = "vh_despair";

        readonly DreamEscapeCourse _d;
        public DreamRainDef def = new DreamRainDef();
        bool _has, _inRain, _despair;
        float _soak;

        public DreamRain(DreamEscapeCourse course) { _d = course; }

        public bool HasUmbrella => _has;
        public bool InRain => _inRain;
        /// <summary>In the rain and not covered.</summary>
        public bool Exposed => _inRain && !_has;
        /// <summary>0 dry .. 1 soaked through.</summary>
        public float Soak => _soak;
        /// <summary>This run went on without the umbrella (the save's <see cref="DespairFlag"/> keeps it across runs).</summary>
        public bool DespairThisRun => _despair;
        public float SpeedScale => 1f + (def.soakedSpeed - 1f) * _soak;
        public float JumpScale => 1f + (def.soakedJump - 1f) * _soak;
        public float Vignette => def.soakedVignette * _soak;

        public static bool IsRain(DreamEscapeCourse d, float s) => s >= d.rainStartS;

        /// <summary>Takes the umbrella (the dream tool hit it). False if she already has it.</summary>
        public bool PickUp()
        {
            if (_has) return false;
            _has = true;
            return true;
        }

        public void Reset()
        {
            _has = false;
            _inRain = false;
            _despair = false;
            _soak = 0f;
        }

        public DreamRainEvents Tick(float dt, float s)
        {
            var ev = new DreamRainEvents();
            bool rain = IsRain(_d, s);
            if (rain != _inRain)
            {
                _inRain = rain;
                if (rain) ev.enteredRain = true; else ev.leftRain = true;
            }
            if (Exposed) _soak = Math.Min(1f, _soak + dt / Math.Max(0.01f, def.soakTime));
            else _soak = Math.Max(0f, _soak - dt * def.dryRate);
            if (!_despair && !_has && s >= _d.despairS)
            {
                _despair = true;
                ev.despairChosen = true;
            }
            return ev;
        }

        /// <summary>Whether the umbrella is in reach of the tool from <paramref name="eye"/> along <paramref name="forward"/>
        /// (the dream tool's aim assist: the cone from its edge, the range, in front).</summary>
        public bool Locks(Vector3 eye, Vector3 forward, float coneDegrees, float range)
        {
            if (_has) return false;
            float off = JamKit.Movement.DreamToolAim.AngleOff(eye, forward, _d.umbrella, _d.umbrellaRadius, out float dist);
            return off <= coneDegrees && dist - _d.umbrellaRadius <= range && Vector3.Dot(_d.umbrella - eye, forward) > 0f;
        }
    }
}
