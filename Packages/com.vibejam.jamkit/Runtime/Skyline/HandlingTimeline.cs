using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>The clips of a gun's handling set (docs/SKYLINE-DESIGN.md 2.3), each with sound and code animation.</summary>
    public enum HandlingClipKind { Equip, Fire, DryFire, ReloadPartial, ReloadEmpty, Inspect }

    /// <summary>One event of a clip (mag out, cell in, hydraulic seat, bolt...) at a fixed fraction of the clip's length,
    /// so a faster reload moves its sounds with its animation.</summary>
    public struct HandlingEvent
    {
        /// <summary>0..1 of the clip's length.</summary>
        public float at;
        /// <summary>The event's name inside its clip ("mag_out").</summary>
        public string id;
        /// <summary>The sound it plays, "&lt;gun&gt;/&lt;name&gt;": the forge recipe tools/skyline/guns/&lt;gun&gt;/recipes/&lt;name&gt;.json
        /// and the Unity clip Resources/SkylineAudio/&lt;gun&gt;/&lt;name&gt;. Null or empty: an animation-only event.</summary>
        public string sound;

        public HandlingEvent(float at, string id, string sound)
        {
            this.at = at;
            this.id = id;
            this.sound = sound;
        }
    }

    /// <summary>The viewmodel's offset from its rest pose at a fraction of a clip: the gun's position (metres, camera space:
    /// x right, y up, z forward), its rotation (Euler degrees: x pitches the muzzle down, y yaws it right, z rolls it), and
    /// the magazine's (or cell's) offset from its seat (gun space). The robot arms follow the gun.</summary>
    public struct PoseFrame
    {
        public float at;
        public Vector3 gunOffset;
        public Vector3 gunEuler;
        public Vector3 magOffset;

        public PoseFrame(float at, Vector3 gunOffset, Vector3 gunEuler, Vector3 magOffset)
        {
            this.at = at;
            this.gunOffset = gunOffset;
            this.gunEuler = gunEuler;
            this.magOffset = magOffset;
        }
    }

    /// <summary>One clip of a handling set: its length, its events and its pose frames (both in order of <c>at</c>).</summary>
    public sealed class HandlingClip
    {
        public HandlingClipKind kind;
        public float seconds;
        public HandlingEvent[] events = Array.Empty<HandlingEvent>();
        public PoseFrame[] pose = Array.Empty<PoseFrame>();

        public HandlingClip() { }

        public HandlingClip(HandlingClipKind kind, float seconds, HandlingEvent[] events, PoseFrame[] pose)
        {
            this.kind = kind;
            this.seconds = seconds;
            this.events = events ?? Array.Empty<HandlingEvent>();
            this.pose = pose ?? Array.Empty<PoseFrame>();
        }

        /// <summary>Null when the clip is usable; otherwise what's wrong, naming the clip and the event or frame.</summary>
        public string Validate()
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds)) return $"{kind}: seconds must be a number > 0, got {seconds}";
            var ids = new HashSet<string>();
            float last = 0f;
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                string name = $"{kind} event {i} ({e.id ?? "no id"})";
                if (!(e.at >= 0f && e.at <= 1f)) return $"{name}: at must be within [0, 1], got {e.at}";
                if (e.at < last) return $"{name}: events must be in order of at ({e.at} comes after {last})";
                if (string.IsNullOrEmpty(e.id)) return $"{name}: missing id";
                if (!ids.Add(e.id)) return $"{name}: the id is used twice in this clip";
                last = e.at;
            }
            last = 0f;
            for (int i = 0; i < pose.Length; i++)
            {
                float at = pose[i].at;
                if (!(at >= 0f && at <= 1f)) return $"{kind} pose frame {i}: at must be within [0, 1], got {at}";
                if (at < last) return $"{kind} pose frame {i}: frames must be in order of at ({at} comes after {last})";
                last = at;
            }
            return null;
        }

        /// <summary>The pose at <paramref name="t01"/> of the clip: eased (smoothstep) between the frames around it, held
        /// before the first frame and after the last; the rest pose (all zero) when the clip has no frames.</summary>
        public PoseFrame SamplePose(float t01)
        {
            if (float.IsNaN(t01)) t01 = 0f;
            if (pose.Length == 0) return new PoseFrame(t01, Vector3.Zero, Vector3.Zero, Vector3.Zero);
            if (t01 <= pose[0].at) return Held(pose[0], t01);
            var final = pose[pose.Length - 1];
            if (t01 >= final.at) return Held(final, t01);
            int i = 1;
            while (pose[i].at < t01) i++;
            var a = pose[i - 1];
            var b = pose[i];
            float span = b.at - a.at;
            float s = span > 1e-6f ? (t01 - a.at) / span : 1f;
            s = s * s * (3f - 2f * s);
            return new PoseFrame(t01, Vector3.Lerp(a.gunOffset, b.gunOffset, s), Vector3.Lerp(a.gunEuler, b.gunEuler, s),
                Vector3.Lerp(a.magOffset, b.magOffset, s));
        }

        static PoseFrame Held(PoseFrame f, float t01) => new PoseFrame(t01, f.gunOffset, f.gunEuler, f.magOffset);
    }

    /// <summary>Plays one clip at a time and reports the events it crosses. Frame-rate independent: a long step fires every
    /// event it crosses, in order, exactly once.</summary>
    public sealed class HandlingPlayer
    {
        HandlingClip _clip;
        float _duration, _t;
        int _next;

        public HandlingClip Clip => _clip;
        /// <summary>The clip's length after the speed scale (s).</summary>
        public float Duration => _duration;
        public float Elapsed => _t;
        public bool Playing => _clip != null && _t < _duration;
        public float Progress01 => _clip == null ? 0f : (_duration > 0f ? Math.Min(1f, _t / _duration) : 1f);
        public PoseFrame Pose => _clip == null ? new PoseFrame(0f, Vector3.Zero, Vector3.Zero, Vector3.Zero) : _clip.SamplePose(Progress01);

        /// <summary>Starts <paramref name="clip"/> from its beginning. <paramref name="speed"/> above 1 plays it faster (a
        /// reload-speed upgrade): it lasts seconds / speed and its events keep their fractions. Events at 0 fire on the next
        /// <see cref="Advance"/>, even one of 0 seconds.</summary>
        public void Play(HandlingClip clip, float speed = 1f)
        {
            _clip = clip ?? throw new ArgumentNullException(nameof(clip));
            if (!(speed > 0.01f)) speed = 0.01f;
            _duration = clip.seconds / speed;
            _t = 0f;
            _next = 0;
        }

        /// <summary>Stops without firing the rest of the clip's events (a swap, a cancelled reload).</summary>
        public void Stop()
        {
            _clip = null;
            _duration = 0f;
            _t = 0f;
            _next = 0;
        }

        /// <summary>Advances by <paramref name="dt"/> (NaN and negative count as 0) and appends the events now due to
        /// <paramref name="fired"/> (if given), in order. Returns how many fired.</summary>
        public int Advance(float dt, List<HandlingEvent> fired)
        {
            if (_clip == null) return 0;
            if (dt > 0f) _t = float.IsInfinity(dt) ? _duration : _t + dt;
            float p = Progress01;
            int n = 0;
            var ev = _clip.events;
            while (_next < ev.Length && ev[_next].at <= p + 1e-6f)
            {
                fired?.Add(ev[_next]);
                _next++;
                n++;
            }
            return n;
        }

        /// <summary>When event <paramref name="index"/> of <paramref name="clip"/> fires at <paramref name="speed"/>: seconds
        /// from the clip's start.</summary>
        public static float EventTime(HandlingClip clip, int index, float speed = 1f) =>
            clip.events[index].at * clip.seconds / (speed > 0.01f ? speed : 0.01f);
    }
}
