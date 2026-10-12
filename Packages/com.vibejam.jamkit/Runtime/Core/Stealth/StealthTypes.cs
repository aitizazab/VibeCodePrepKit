using System;
using System.Numerics;

namespace JamKit.Stealth
{
    /// <summary>A seeker's alert level (docs/STEALTH.md): unaware (patrolling, or counting in hide-and-seek), suspicious
    /// (saw a glimpse or heard something: turns, then goes to look), searching (at the last known position: looks around,
    /// checks the hiding spots near it), found (the detection meter filled, or it opened your hiding spot). Searching
    /// times out back to unaware in a patrol; hide-and-seek keeps searching until it finds you.</summary>
    public enum AlertState { Unaware, Suspicious, Searching, Found }

    /// <summary>Patrol: the generic guard (walks a loop, returns to it after a search). HideAndSeek: counts, then seeks
    /// every hiding spot and corner until it finds you.</summary>
    public enum SeekerMode { Patrol, HideAndSeek }

    /// <summary>Bush: you're hard to see unless the seeker is right next to it. Under (a deck, a table) and Closet:
    /// unseen, but a searching seeker can check the spot and find you.</summary>
    public enum HideKind { Bush, Under, Closet }

    /// <summary>Where the target sits in a seeker's senses.</summary>
    public enum SightZone { None, Peripheral, Cone, Close }

    public enum NoiseKind { None, Footsteps, Landing, Wallrun, Slide, Other }

    /// <summary>What the stealth kit asks of the world: line of sight and light. The Unity adapter uses physics and
    /// light zones; <see cref="StealthBoxWorld"/> is a plain box world for tests and simple games.</summary>
    public interface IStealthWorld
    {
        /// <summary>True when nothing that blocks sight lies between the two points.</summary>
        bool LineClear(Vector3 from, Vector3 to);
        /// <summary>0 (dark) to 1 (lit) at a point.</summary>
        float LightAt(Vector3 point);
    }

    /// <summary>The one being looked for, as the senses need it (one value per frame; a struct, no allocation).</summary>
    public struct StealthTarget
    {
        /// <summary>False: no target (nothing to see).</summary>
        public bool present;
        public Vector3 feet;
        /// <summary>Current body height (1.7 standing, about 1.0 crouched or sliding).</summary>
        public float height;
        public float speed;
        public bool crouched, sliding;
        /// <summary>Index of the hiding spot the target is in, -1 for none.</summary>
        public int hidingSpot;
        /// <summary>Peeking: where the head is, sideways from the body's centre line (m, horizontal; zero standing straight).
        /// Sight looks for the head there, so a head round a door frame is a small target and a step out the whole body.</summary>
        public Vector3 lean;

        public static StealthTarget At(Vector3 feet, float height = 1.7f)
            => new StealthTarget { present = true, feet = feet, height = height, hidingSpot = -1 };
    }

    /// <summary>A sound at a place: heard by a seeker within <see cref="radius"/> (less through walls).</summary>
    public struct StealthNoise
    {
        public Vector3 position;
        public float radius;
        public NoiseKind kind;
        public bool Any => radius > 0f && kind != NoiseKind.None;

        public static StealthNoise Make(Vector3 at, float radius, NoiseKind kind) => new StealthNoise { position = at, radius = radius, kind = kind };
    }

    /// <summary>A place to hide: an axis-aligned box (the volume you stand or lie in), the spot a seeker stands at to
    /// check it, and its kind.</summary>
    [Serializable]
    public class HidingSpot
    {
        public string name = "Spot";
        public HideKind kind = HideKind.Bush;
        /// <summary>Centre and size of the hiding volume (metres, world space).</summary>
        public float cx, cy, cz, sx = 2f, sy = 2f, sz = 2f;
        /// <summary>Where a seeker stands to check it (on its walking floor).</summary>
        public float ax, ay, az;

        public Vector3 Center => new Vector3(cx, cy, cz);
        public Vector3 Size => new Vector3(sx, sy, sz);
        public Vector3 Approach => new Vector3(ax, ay, az);

        public bool Contains(Vector3 p)
            => Math.Abs(p.X - cx) <= sx * 0.5f && Math.Abs(p.Z - cz) <= sz * 0.5f && p.Y >= cy - sy * 0.5f - 0.05f && p.Y <= cy + sy * 0.5f;

        public static HidingSpot Box(string name, HideKind kind, Vector3 center, Vector3 size, Vector3 approach)
            => new HidingSpot { name = name, kind = kind, cx = center.X, cy = center.Y, cz = center.Z, sx = size.X, sy = size.Y, sz = size.Z, ax = approach.X, ay = approach.Y, az = approach.Z };
    }

    /// <summary>What happened to a seeker in one tick.</summary>
    public struct SeekerEvents
    {
        public bool stateChanged;
        public AlertState from, to;
        /// <summary>The seeker found the target this tick (meter full, or it opened the spot the target is in).</summary>
        public bool found;
        /// <summary>Found by checking a hiding spot (the spot's index), -1 otherwise.</summary>
        public int foundInSpot;
        /// <summary>A hiding spot was checked this tick (its index), -1 for none.</summary>
        public int checkedSpot;
        public bool heard;
        public bool countingDone;
        /// <summary>It heard something and turned wary this tick (it wasn't before).</summary>
        public bool becameWary;
        /// <summary>Repeated noise escalated it to alert (searching) this tick.</summary>
        public bool escalated;
        /// <summary>It grew more alert this tick and called out (answer B: <see cref="SeekerSquad"/> passes it on).</summary>
        public bool called;

        public static SeekerEvents None => new SeekerEvents { foundInSpot = -1, checkedSpot = -1 };
    }

    static class StealthMath
    {
        public static Vector3 Forward(float yawDegrees)
        {
            double r = yawDegrees * Math.PI / 180.0;
            return new Vector3((float)Math.Sin(r), 0f, (float)Math.Cos(r));
        }

        /// <summary>The yaw (degrees, Unity: 0 = +z, 90 = +x) that looks along a flat direction.</summary>
        public static float YawOf(Vector3 d) => (float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI);

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.X - b.X, z = a.Z - b.Z;
            return (float)Math.Sqrt(x * x + z * z);
        }

        /// <summary>Signed smallest difference b - a in degrees (-180..180).</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float d = DeltaAngle(current, target);
            if (Math.Abs(d) <= maxDelta) return target;
            return current + Math.Sign(d) * maxDelta;
        }

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
