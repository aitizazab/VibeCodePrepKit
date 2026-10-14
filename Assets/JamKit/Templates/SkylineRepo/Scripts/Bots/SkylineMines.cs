using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>What happened to one of a gondola's wall mines (raised through <see cref="SkylineBot.MineEvent"/>).</summary>
    public enum SkylineMineEvent { Laid, Armed, Triggered, Blast, Expired }

    /// <summary>
    /// One wall mine drawn (the pure <see cref="WallMine"/> decides everything; this only shows it). It drops from the gondola's
    /// <c>MineDrop</c> onto the wall behind its logical position (the point on the rail line, where the pure rules measure the
    /// trigger and blast from), so it reads as stuck to the wall. Arming: a dark puck. Armed: a slow red blink. Triggered (the
    /// fuse): a fast blink and a ring of red ticks at the trigger radius round the logical point: the trigger telegraph.
    /// Blast: <see cref="SkylineMineBlast"/>. Expired: it shrinks away. No sparks (they belong to the Writ's cracks and fresh
    /// wrecks).
    /// </summary>
    public sealed class SkylineMineView
    {
        const float DropSeconds = 0.2f, RingTicks = 12;
        /// <summary>A hatch mine (on the inside of the façade) is drawn this much bigger, so it reads across the hall.</summary>
        const float FacadeScale = 1.8f;
        // the hostile hue #FF2E63 (SKYLINE-STYLE.md: hostile bots and their mines), the same as its outline
        static readonly Color Off = new Color(0.18f, 0.18f, 0.2f), Red = SkylineInkRoles.Hue(SkylineInkRoles.Hostile);

        public readonly WallMine mine;
        public MineState lastState;
        readonly GameObject _root;
        readonly Transform _ring;
        readonly Renderer _led;
        readonly Vector3 _from, _to;
        readonly float _base;
        float _t, _expire = -1f;

        public Vector3 Logical { get; }

        /// <summary>True for a mine on the inside of the façade (laid through a hatch): bigger, faces into the hall, and its trigger
        /// ring stays up while it is armed, so a player can path round, above or below it.</summary>
        public bool Facade { get; }

        public SkylineMineView(WallMine mine, Vector3 logical, Vector3 dropFrom, Vector3 wallBack, float triggerRadius, Transform owner)
        {
            this.mine = mine;
            lastState = mine.State;
            Logical = logical;
            _from = dropFrom;
            // stick it to the wall behind the logical point, if there's one within 1.5 m
            _to = logical;
            Vector3 normal = -wallBack;
            Facade = FacadeHatches.IsHatchPoint(new System.Numerics.Vector3(logical.x, logical.y, logical.z));
            if (Facade)
            {
                // on the glass's inside face: it faces into the hall (-x), whichever way the gondola outside faces
                normal = Vector3.left;
            }
            else if (wallBack.sqrMagnitude > 1e-6f && SkylineBot.CastClear(logical, wallBack.normalized, 1.5f, owner, out var hit))
            {
                _to = hit.point + hit.normal * 0.03f;
                normal = hit.normal;
            }
            _base = Facade ? FacadeScale : 1f;
            _root = new GameObject("Wall Mine");
            _root.transform.position = _from;
            _root.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 1e-6f ? normal : Vector3.forward);
            _root.transform.localScale = Vector3.one * _base;
            SkylineBotLook.Part(PrimitiveType.Cylinder, "Puck", _root.transform, Vector3.zero, new Vector3(0.32f, 0.03f, 0.32f), SkylineBotLook.Dark, false, new Vector3(90f, 0f, 0f));
            SkylineBotLook.Part(PrimitiveType.Cube, "Rim", _root.transform, new Vector3(0f, 0f, 0.035f), new Vector3(0.22f, 0.04f, 0.02f), SkylineBotLook.Gold, false);
            _led = SkylineBotLook.Part(PrimitiveType.Sphere, "Led", _root.transform, new Vector3(0f, 0.07f, 0.05f), Vector3.one * 0.06f, Off, false).GetComponent<Renderer>();
            // the trigger ring: round the logical point, in the wall's plane
            _ring = new GameObject("Trigger Ring").transform;
            _ring.position = logical;
            _ring.rotation = _root.transform.rotation;
            for (int i = 0; i < RingTicks; i++)
            {
                float a = i * Mathf.PI * 2f / RingTicks;
                SkylineBotLook.Part(PrimitiveType.Cube, "Tick", _ring, new Vector3(Mathf.Cos(a) * triggerRadius, Mathf.Sin(a) * triggerRadius, 0f), new Vector3(0.14f, 0.05f, 0.02f), Red, false, new Vector3(0f, 0f, a * Mathf.Rad2Deg + 90f));
            }
            SkylineInkRoles.Apply(_ring.gameObject, SkylineInkRoles.Hostile);
            _ring.gameObject.SetActive(false);
            SkylineInkRoles.Apply(_root, SkylineInkRoles.Hostile);   // comic ink: a hostile mine is outlined
            foreach (var r in _root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public bool Gone => _root == null;

        /// <summary>Animates the drop and the light for <paramref name="state"/>. Returns false once it has finished and is gone.</summary>
        public bool Tick(float dt, MineState state)
        {
            if (_root == null) return false;
            _t += dt;
            float k = Mathf.Clamp01(_t / DropSeconds);
            _root.transform.position = Vector3.Lerp(_from, _to, k * k);
            bool on;
            switch (state)
            {
                case MineState.Armed: on = Mathf.Repeat(_t, 1f) < 0.15f; break;
                case MineState.Fusing: on = Mathf.Repeat(_t, 0.1f) < 0.05f; break;
                default: on = false; break;
            }
            _led.sharedMaterial = SkylineBotLook.Lit(on ? Red : Off, SkylineInkRoles.Hostile);
            _ring.gameObject.SetActive(state == MineState.Fusing || (Facade && state == MineState.Armed));
            if (_expire >= 0f)
            {
                _expire += dt;
                float s = Mathf.Clamp01(1f - _expire / 0.3f);
                _root.transform.localScale = Vector3.one * (s * _base);
                if (s <= 0f) { Destroy(); return false; }
            }
            return true;
        }

        public void Blast(float radius)
        {
            SkylineMineBlast.Create(Logical, radius);
            Destroy();
        }

        public void Expire()
        {
            if (_expire < 0f) _expire = 0f;
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            if (_ring != null) Object.Destroy(_ring.gameObject);
        }
    }

    /// <summary>A mine's blast: an orange ball that swells to the blast radius in 0.15 s and collapses in 0.2 s.</summary>
    public sealed class SkylineMineBlast : MonoBehaviour
    {
        const float Grow = 0.15f, Shrink = 0.2f;
        float _t, _radius;

        public static void Create(Vector3 at, float radius)
        {
            var go = SkylineBotLook.Visual(PrimitiveType.Sphere, "Mine Blast", null, SkylineBotLook.Lit(new Color(1f, 0.55f, 0.15f)), false);
            go.transform.position = at;
            go.transform.localScale = Vector3.zero;
            go.AddComponent<SkylineMineBlast>()._radius = radius;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float d = _radius * 2f;
            float s = _t < Grow ? d * (_t / Grow) : d * Mathf.Clamp01(1f - (_t - Grow) / Shrink);
            transform.localScale = Vector3.one * s;
            if (_t >= Grow + Shrink) Destroy(gameObject);
        }
    }
}
