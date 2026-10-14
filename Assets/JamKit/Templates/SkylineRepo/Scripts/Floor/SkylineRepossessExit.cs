using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The west service lift for the finale (task R2; docs/SKYLINE-FLOOR-REPOSSESS.md section 2): when the third asset is towed its
    /// shutter (the solid <see cref="RepossessArena.LiftShutterId"/>) rolls fully up with the objective outline, a violet frame
    /// lights the doorway and a glowing arrow bobs in front of it; the floor ends when the Unit's feet are inside
    /// <see cref="RepossessArena.ExitLiftZone"/>. A restart shuts it again. Not a MonoBehaviour. (The lobby's lift cab is
    /// L2's <c>SkylineLiftDoor</c>, not this.)
    /// </summary>
    public sealed class SkylineRepossessExit
    {
        /// <summary>The roll takes this long and lifts the shutter this far (the doorway is 3.2 m; it ends behind the lintel).</summary>
        public const float RollSeconds = 1.4f, RollHeight = 3.5f;
        /// <summary>The placeholder lift sound (an existing CONCIERGE clip).</summary>
        public const string OpenSound = "concierge/chime";
        /// <summary>The zone test's slack (m): the feet at the zone's own edge count as inside.</summary>
        public const float ZoneSlack = 0.1f;

        readonly Transform _shutter;
        readonly Collider _shutterCollider;
        readonly Renderer[] _shutterRenderers;
        readonly Material[][] _originalMaterials;
        readonly Vector3 _shutterStart;
        readonly Transform _marks;      // the frame and the arrow
        readonly Transform _arrow;
        readonly Vector3 _arrowBase;
        float _roll = -1f;
        bool _open;

        public bool Open => _open;
        /// <summary>The shutter object (null if the scene has none: the finale still runs without it).</summary>
        public Transform Shutter => _shutter;
        /// <summary>0..1: how far up the shutter has rolled.</summary>
        public float RollProgress => _roll < 0f ? 0f : Mathf.Clamp01(_roll / RollSeconds);
        public Transform Arrow => _arrow;
        public Transform Marks => _marks;

        public SkylineRepossessExit(Transform arena, Transform parent)
        {
            _shutter = SkylineRepossessFx.FindDeep(arena, RepossessArena.LiftShutterId);
            if (_shutter != null)
            {
                _shutterStart = _shutter.position;
                _shutterCollider = _shutter.GetComponent<Collider>();
                _shutterRenderers = _shutter.GetComponentsInChildren<Renderer>(true);
                _originalMaterials = new Material[_shutterRenderers.Length][];
                for (int i = 0; i < _shutterRenderers.Length; i++) _originalMaterials[i] = _shutterRenderers[i].sharedMaterials;
            }
            else
            {
                _shutterRenderers = new Renderer[0];
                _originalMaterials = new Material[0][];
                Debug.LogWarning("SKYLINE_FLOOR no " + RepossessArena.LiftShutterId + " in the scene: the exit lift has no shutter to roll");
            }

            // The doorway: x -24.4..-24 is the front wall, the opening is z -2..2 and 3.2 m tall (RepossessArena). The frame stands a
            // hand's breadth in front of it (east), the arrow floats further out, pointing west into the lift.
            _marks = new GameObject("Exit lift marks").transform;
            if (parent != null) _marks.SetParent(parent, false);
            var glow = SkylineBotLook.Glow(SkylineRepossessFx.VioletHdr, SkylineInkRoles.Objective);
            const float fx = -23.85f;
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Frame N", _marks, new Vector3(fx, 1.6f, 2.05f), new Vector3(0.1f, 3.3f, 0.12f), glow);
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Frame S", _marks, new Vector3(fx, 1.6f, -2.05f), new Vector3(0.1f, 3.3f, 0.12f), glow);
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Frame top", _marks, new Vector3(fx, 3.28f, 0f), new Vector3(0.1f, 0.12f, 4.22f), glow);
            _arrow = new GameObject("Arrow").transform;
            _arrow.SetParent(_marks, false);
            _arrowBase = new Vector3(-21.8f, 2.2f, 0f);
            _arrow.localPosition = _arrowBase;
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Shaft", _arrow, new Vector3(0.45f, 0f, 0f), new Vector3(1.1f, 0.2f, 0.2f), glow);
            var head = SkylineRepossessFx.Piece(PrimitiveType.Cube, "Head", _arrow, new Vector3(-0.35f, 0f, 0f), new Vector3(0.42f, 0.2f, 0.42f), glow);
            head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);      // a diamond whose point faces west
            _marks.gameObject.SetActive(false);
        }

        /// <summary>The finale: the shutter starts to roll up and the marks light. No-op when it is already open.</summary>
        public void OpenUp()
        {
            if (_open) return;
            _open = true;
            _roll = 0f;
            _marks.gameObject.SetActive(true);
            for (int i = 0; i < _shutterRenderers.Length; i++)
            {
                var mats = _shutterRenderers[i].sharedMaterials;
                bool changed = false;
                for (int k = 0; k < mats.Length; k++)
                {
                    var ink = SkylineInkRoles.FromLit(mats[k], SkylineInkRoles.Objective);
                    if (ink == null) continue;
                    mats[k] = ink;
                    changed = true;
                }
                if (changed) _shutterRenderers[i].sharedMaterials = mats;
            }
            SkylineRepossessFx.Sound(OpenSound, _shutter != null ? _shutter.position : Vector3.zero);
        }

        /// <summary>Advances the roll and bobs the arrow.</summary>
        public void Tick(float dt)
        {
            if (!_open) return;
            if (_shutter != null && _roll >= 0f && _roll < RollSeconds)
            {
                _roll = Mathf.Min(RollSeconds, _roll + Mathf.Max(0f, dt));
                float p = SkylineRepossessFx.Smooth01(_roll / RollSeconds);
                _shutter.position = _shutterStart + new Vector3(0f, RollHeight * p, 0f);
                if (_roll >= RollSeconds)
                {
                    if (_shutterCollider != null) _shutterCollider.enabled = false;
                    for (int i = 0; i < _shutterRenderers.Length; i++) _shutterRenderers[i].enabled = false;
                }
            }
            else if (_shutter == null && _roll >= 0f && _roll < RollSeconds) _roll = Mathf.Min(RollSeconds, _roll + Mathf.Max(0f, dt));
            _arrow.localPosition = _arrowBase + new Vector3(0.35f * Mathf.Sin(Time.time * 4f), 0.08f * Mathf.Sin(Time.time * 2.3f), 0f);
        }

        /// <summary>True when the Unit's feet are inside the lift (<see cref="RepossessArena.ExitLiftZone"/>, with a small slack).</summary>
        public static bool InLift(Vector3 feet)
        {
            var z = RepossessArena.ExitLiftZone;
            return feet.x >= z.min.X - ZoneSlack && feet.x <= z.max.X + ZoneSlack
                && feet.y >= z.min.Y - ZoneSlack && feet.y <= z.max.Y + ZoneSlack
                && feet.z >= z.min.Z - ZoneSlack && feet.z <= z.max.Z + ZoneSlack;
        }

        /// <summary>A restart: the shutter is down and solid again, the marks out, the materials back.</summary>
        public void Close()
        {
            _open = false;
            _roll = -1f;
            _marks.gameObject.SetActive(false);
            if (_shutter == null) return;
            _shutter.position = _shutterStart;
            if (_shutterCollider != null) _shutterCollider.enabled = true;
            for (int i = 0; i < _shutterRenderers.Length; i++)
            {
                _shutterRenderers[i].enabled = true;
                _shutterRenderers[i].sharedMaterials = _originalMaterials[i];
            }
        }
    }
}
