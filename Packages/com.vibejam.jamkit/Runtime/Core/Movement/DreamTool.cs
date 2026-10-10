using System;
using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>The dream tool's tunable numbers (docs/MOVEMENT.md "Dream tool prototype"). Metres, degrees, seconds.</summary>
    [Serializable]
    public sealed class DreamToolDef
    {
        /// <summary>The farthest a shot reaches (hitscan from the camera).</summary>
        public float range = 40f;
        /// <summary>Aim assist: a target this many degrees off the crosshair (from its edge) still locks, on mouse.</summary>
        public float coneMouse = 6f;
        /// <summary>The same on a gamepad: stronger, so the shot stays one action and not an aiming test.</summary>
        public float conePad = 8f;
        /// <summary>Seconds between shots.</summary>
        public float cooldown = 0.25f;
    }

    /// <summary>What one shot did (for the sound, the flash and the HUD).</summary>
    public struct DreamShot
    {
        /// <summary>The target hit, or -1 (a shot at nothing).</summary>
        public int target;
        public DreamTargetKind kind;
        /// <summary>The active colour after the shot.</summary>
        public int colour;
        /// <summary>False when the tool was still cooling down (nothing fired).</summary>
        public bool fired;
    }

    /// <summary>
    /// The dream tool chapter's state, pure: which tiles are popped and which sockets built, which colour is solid, the
    /// tool's cooldown. <see cref="Solid"/> says whether a <see cref="DreamToolPiece"/> stands; <see cref="Version"/>
    /// changes whenever that might have (so views and the test world update only then). No allocation per call.
    /// </summary>
    public sealed class DreamToolState
    {
        readonly DreamToolCourse _c;
        readonly bool[] _done, _hit;
        int _colour;
        int _version;
        float _cooldown;
        int _shots, _hits;

        public DreamToolState(DreamToolCourse course)
        {
            _c = course;
            _done = new bool[course.targets.Count];
            _hit = new bool[course.targets.Count];
            ResetFrom(float.MinValue, DreamToolCourse.Pink);
        }

        public DreamToolCourse Course => _c;
        /// <summary>The solid colour: <see cref="DreamToolCourse.Pink"/> or <see cref="DreamToolCourse.Blue"/>.</summary>
        public int Colour => _colour;
        public int Version => _version;
        public bool Ready => _cooldown <= 0f;
        public float Cooldown => _cooldown;
        /// <summary>Shots fired and targets hit since the last <see cref="ResetFrom"/> at the start.</summary>
        public int Shots => _shots;
        public int Hits => _hits;
        /// <summary>A tile popped or a socket built (switches never stay done).</summary>
        public bool Done(int target) => _done[target];
        /// <summary>Hit at least once since it was last put back (a switch included).</summary>
        public bool HitOnce(int target) => _hit[target];

        /// <summary>Tiles and sockets until they're done; switches always.</summary>
        public bool Valid(int target) => _c.targets[target].kind == DreamTargetKind.Switch || !_done[target];

        /// <summary>Required targets hit since they were last put back (tiles popped, sockets built, switches flipped).</summary>
        public int RequiredDone
        {
            get { int n = 0; for (int i = 0; i < _hit.Length; i++) if (_hit[i] && !_c.targets[i].expert) n++; return n; }
        }

        public bool Solid(int piece) => Solid(_c.pieces[piece]);

        public bool Solid(DreamToolPiece p)
        {
            switch (p.role)
            {
                case DreamPieceRole.Tile: return !_done[p.target];
                case DreamPieceRole.Built: return _done[p.target];
                case DreamPieceRole.Pink: return _colour == DreamToolCourse.Pink;
                default: return _colour == DreamToolCourse.Blue;
            }
        }

        public void Tick(float dt)
        {
            if (_cooldown > 0f) _cooldown -= dt;
        }

        /// <summary>Fires at <paramref name="target"/> (-1: at nothing) if the tool is ready, and applies the hit: a tile
        /// pops, a socket builds, a switch flips the colour. A done tile or socket takes the shot without changing.</summary>
        public DreamShot Fire(int target, float cooldown)
        {
            var shot = new DreamShot { target = -1, colour = _colour };
            if (_cooldown > 0f) return shot;
            _cooldown = cooldown;
            _shots++;
            shot.fired = true;
            if (target < 0 || target >= _done.Length) return shot;
            var t = _c.targets[target];
            shot.target = target;
            shot.kind = t.kind;
            _hit[target] = true;
            if (t.kind == DreamTargetKind.Switch) { _colour = 1 - _colour; _version++; _hits++; }
            else if (!_done[target]) { _done[target] = true; _version++; _hits++; }
            shot.colour = _colour;
            return shot;
        }

        /// <summary>Applies a hit without the cooldown (tests, chapter set-up).</summary>
        public void Apply(int target)
        {
            float c = _cooldown;
            _cooldown = 0f;
            Fire(target, 0f);
            _cooldown = c;
        }

        /// <summary>A respawn or a jump to <paramref name="s"/>: every target ahead (its <see cref="DreamToolTarget.resetS"/>
        /// at or after s) is put back, every one behind counts as done (the route behind you stays open), and the colour is
        /// the checkpoint's. float.MinValue resets everything (a fresh run).</summary>
        public void ResetFrom(float s, int colour)
        {
            for (int i = 0; i < _done.Length; i++)
            {
                var t = _c.targets[i];
                bool behind = t.resetS < s - 0.01f && !t.expert;
                _done[i] = behind && t.kind != DreamTargetKind.Switch;
                _hit[i] = behind;
            }
            _colour = colour;
            _cooldown = 0f;
            if (s == float.MinValue) { _shots = 0; _hits = 0; }
            _version++;
        }
    }

    /// <summary>Line of sight for the aim (Unity: a raycast against the world; tests: the box world).</summary>
    public interface IDreamToolSight
    {
        /// <summary>True when something solid stands between <paramref name="from"/> and <paramref name="to"/> nearer than <paramref name="clearance"/> short of it.</summary>
        bool Blocked(Vector3 from, Vector3 to, float clearance);
    }

    /// <summary>
    /// The aim assist: from the camera along its forward, the valid target whose edge is nearest the crosshair (the
    /// smallest angle once its own size is taken off) within the cone, in range, in front and in sight wins; ties go to
    /// the nearer. The cone is wider on a gamepad. So a shot stays one action: point roughly, press.
    /// </summary>
    public static class DreamToolAim
    {
        const float Rad = (float)(180.0 / Math.PI);

        /// <summary>The degrees between the aim line and the target's edge (0 when the line passes through it).</summary>
        public static float AngleOff(Vector3 eye, Vector3 forward, Vector3 center, float radius, out float distance)
        {
            Vector3 d = center - eye;
            distance = d.Length();
            if (distance < 1e-4f) return 0f;
            float cos = Vector3.Dot(d / distance, forward);
            cos = cos > 1f ? 1f : cos < -1f ? -1f : cos;
            float angle = (float)Math.Acos(cos) * Rad;
            float size = radius >= distance ? 90f : (float)Math.Asin(radius / distance) * Rad;
            return Math.Max(0f, angle - size);
        }

        /// <summary>The locked target, or -1. <paramref name="forward"/> must be unit length; <paramref name="sight"/> may be null.</summary>
        public static int Pick(DreamToolCourse course, DreamToolState state, Vector3 eye, Vector3 forward, float coneDegrees, float range, IDreamToolSight sight, out float angle)
        {
            int best = -1;
            float bestAngle = float.MaxValue, bestDist = float.MaxValue;
            var targets = course.targets;
            for (int i = 0; i < targets.Count; i++)
            {
                if (!state.Valid(i)) continue;
                var t = targets[i];
                float off = AngleOff(eye, forward, t.center, t.radius, out float dist);
                if (off > coneDegrees || dist - t.radius > range) continue;
                if (Vector3.Dot(t.center - eye, forward) <= 0f) continue;
                bool better = off < bestAngle - 0.01f || (Math.Abs(off - bestAngle) <= 0.01f && dist < bestDist);
                if (!better) continue;
                if (sight != null && sight.Blocked(eye, t.center, t.radius + 0.25f)) continue;
                best = i;
                bestAngle = off;
                bestDist = dist;
            }
            angle = best >= 0 ? bestAngle : 0f;
            return best;
        }
    }

    /// <summary>
    /// The course autopilot's trigger finger (it can't aim): at each cue's s along the route it fires at that cue's
    /// target if the target is in range and still needed (a switch only if the colour isn't the cue's yet). It reads only
    /// the state and the eye, so the same pilot fires in the dotnet tests and in Unity (tests, attract mode).
    /// </summary>
    public sealed class DreamToolPilot
    {
        readonly DreamToolCourse _c;
        readonly bool[] _done;

        public DreamToolPilot(DreamToolCourse course)
        {
            _c = course;
            _done = new bool[course.pilotCues.Count];
        }

        /// <summary>Forget cues behind <paramref name="fromS"/> (after a respawn or a jump).</summary>
        public void Reset(float fromS)
        {
            for (int i = 0; i < _done.Length; i++) _done[i] = _c.pilotCues[i].s < fromS - 0.5f;
        }

        /// <summary>The target to fire at now, or -1. Marks the cue done once it's fired or no longer needed.</summary>
        public int Next(float s, Vector3 eye, DreamToolState state, float range)
        {
            for (int i = 0; i < _done.Length; i++)
            {
                if (_done[i]) continue;
                var cue = _c.pilotCues[i];
                if (s < cue.s) break;
                var t = _c.targets[cue.target];
                bool needed = t.kind == DreamTargetKind.Switch ? state.Colour != cue.colour : !state.Done(cue.target);
                if (!needed) { _done[i] = true; continue; }
                if (!state.Ready) return -1;
                if (Vector3.Distance(eye, t.center) - t.radius > range) return -1;
                _done[i] = true;
                return cue.target;
            }
            return -1;
        }
    }
}
