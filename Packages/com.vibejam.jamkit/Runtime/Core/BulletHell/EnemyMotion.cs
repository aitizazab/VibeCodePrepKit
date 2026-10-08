using System;
using System.Numerics;

namespace JamKit.BulletHell
{
    /// <summary>What an enemy does once it has flown in.</summary>
    public enum EnemyMoveKind
    {
        /// <summary>Flies to its spot and stays there (bobbing a little) until killed or its time is up.</summary>
        Hover,
        /// <summary>Flies to its spot, then slides left and right around it.</summary>
        Strafe,
        /// <summary>Flies to its spot, waits a moment, then charges straight at where the player is.</summary>
        Dive,
        /// <summary>Crosses the screen sideways in a wavy line, from the side it spawned on.</summary>
        Sweep,
        /// <summary>Drifts slowly down the screen, swaying.</summary>
        Drift,
    }

    /// <summary>Enemy movement as designer data. Distances in world units, times in seconds.</summary>
    [Serializable]
    public class EnemyMoveDef
    {
        public EnemyMoveKind kind = EnemyMoveKind.Hover;
        /// <summary>Seconds to fly from the spawn point to its spot (not used by Sweep and Drift).</summary>
        public float enterSeconds = 1.2f;
        /// <summary>Strafe / Sweep / Drift sway width.</summary>
        public float amplitude = 2f;
        /// <summary>Sways per second.</summary>
        public float frequency = 0.25f;
        /// <summary>Dive, Sweep and Drift speed; also how fast it leaves.</summary>
        public float speed = 5f;
        /// <summary>Hover / Strafe: seconds before it flies away up (0 = stays until killed).
        /// Dive: seconds it waits before charging.</summary>
        public float actSeconds;
    }

    public enum MoverPhase { Entering, Acting, Leaving, Gone }

    /// <summary>
    /// Runs one <see cref="EnemyMoveDef"/>: enter (ease from spawn to anchor), act (hover, strafe,
    /// dive, sweep, drift) and leave (fly off-screen), ending in <see cref="MoverPhase.Gone"/>
    /// once it is well outside the playfield. Pure maths, one per enemy, no allocation.
    /// </summary>
    public sealed class EnemyMover
    {
        const float TwoPi = (float)(Math.PI * 2);
        /// <summary>How far outside the playfield counts as gone.</summary>
        public float exitMargin = 1.5f;

        EnemyMoveDef _def;
        Vector2 _position, _from, _anchor, _velocity;
        MoverPhase _phase = MoverPhase.Gone;
        float _phaseTime, _totalTime;
        bool _wasInside;

        public Vector2 Position => _position;
        public MoverPhase Phase => _phase;
        public bool IsActing => _phase == MoverPhase.Acting;
        public bool IsGone => _phase == MoverPhase.Gone;
        public Vector2 Anchor => _anchor;
        /// <summary>Seconds since <see cref="Begin"/>.</summary>
        public float Time => _totalTime;

        /// <summary>Starts at <paramref name="spawn"/>, heading for <paramref name="anchor"/>.</summary>
        public void Begin(EnemyMoveDef def, Vector2 spawn, Vector2 anchor)
        {
            _def = def ?? new EnemyMoveDef();
            _position = spawn;
            _from = spawn;
            _anchor = anchor;
            _phaseTime = 0f;
            _totalTime = 0f;
            _wasInside = false;
            _velocity = Vector2.Zero;
            switch (_def.kind)
            {
                case EnemyMoveKind.Sweep:
                    _anchor = spawn;
                    _velocity = new Vector2(spawn.X <= 0f ? Math.Abs(_def.speed) : -Math.Abs(_def.speed), 0f);
                    _phase = MoverPhase.Acting;
                    break;
                case EnemyMoveKind.Drift:
                    _anchor = spawn;
                    _velocity = new Vector2(0f, -Math.Abs(_def.speed));
                    _phase = MoverPhase.Acting;
                    break;
                default:
                    _phase = _def.enterSeconds > 0f ? MoverPhase.Entering : MoverPhase.Acting;
                    if (_phase == MoverPhase.Acting) _position = anchor;
                    break;
            }
        }

        /// <summary>Starts flying away (up and off the screen) from wherever it is.</summary>
        public void Leave()
        {
            if (_phase == MoverPhase.Gone || _phase == MoverPhase.Leaving) return;
            _phase = MoverPhase.Leaving;
            _phaseTime = 0f;
            _velocity = new Vector2(0f, Math.Max(2f, Math.Abs(_def.speed)));
        }

        public void Tick(float dt, Vector2 player, PlayfieldBounds bounds)
        {
            if (_phase == MoverPhase.Gone || !(dt > 0f)) return;
            _phaseTime += dt;
            _totalTime += dt;
            switch (_phase)
            {
                case MoverPhase.Entering:
                {
                    float k = Math.Min(1f, _phaseTime / Math.Max(0.01f, _def.enterSeconds));
                    float e = k * k * (3f - 2f * k); // smoothstep
                    _position = Vector2.Lerp(_from, _anchor, e);
                    if (k >= 1f) { _phase = MoverPhase.Acting; _phaseTime = 0f; }
                    break;
                }
                case MoverPhase.Acting:
                    Act(dt, player);
                    break;
                case MoverPhase.Leaving:
                    _position += _velocity * dt;
                    break;
            }

            bool inside = bounds.Contains(_position);
            if (inside) _wasInside = true;
            bool canExit = _wasInside || _phase == MoverPhase.Leaving;
            if (canExit && !bounds.Contains(_position, exitMargin)) _phase = MoverPhase.Gone;
        }

        void Act(float dt, Vector2 player)
        {
            float t = _phaseTime;
            float w = TwoPi * _def.frequency;
            switch (_def.kind)
            {
                case EnemyMoveKind.Hover:
                    _position = _anchor + new Vector2(0f, 0.15f * (float)Math.Sin(TwoPi * 0.5f * t));
                    if (_def.actSeconds > 0f && t >= _def.actSeconds) Leave();
                    break;
                case EnemyMoveKind.Strafe:
                    _position = _anchor + new Vector2(_def.amplitude * (float)Math.Sin(w * t), 0.1f * (float)Math.Sin(TwoPi * 0.5f * t));
                    if (_def.actSeconds > 0f && t >= _def.actSeconds) Leave();
                    break;
                case EnemyMoveKind.Dive:
                    if (t < Math.Max(0f, _def.actSeconds))
                    {
                        _position = _anchor;
                        break;
                    }
                    Vector2 to = player - _position;
                    _velocity = to.LengthSquared() > 1e-6f ? Vector2.Normalize(to) * Math.Abs(_def.speed) : new Vector2(0f, -Math.Abs(_def.speed));
                    _phase = MoverPhase.Leaving;
                    _phaseTime = 0f;
                    break;
                case EnemyMoveKind.Sweep:
                    _position = new Vector2(_position.X + _velocity.X * dt, _anchor.Y + _def.amplitude * (float)Math.Sin(w * t));
                    break;
                case EnemyMoveKind.Drift:
                    _position = new Vector2(_anchor.X + _def.amplitude * (float)Math.Sin(w * t), _position.Y + _velocity.Y * dt);
                    break;
            }
        }
    }
}
