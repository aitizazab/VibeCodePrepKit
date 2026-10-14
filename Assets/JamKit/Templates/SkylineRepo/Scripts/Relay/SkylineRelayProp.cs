using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// One station's prop in the Relay (docs/SKYLINE-RELAY.md), built from primitives in flat InkLit colours. The shape says what
    /// it is: <see cref="Role.Button"/> a pedestal with a big dome button, <see cref="Role.Reel"/> a cable reel on a stand,
    /// <see cref="Role.Socket"/> a panel with a round socket and a card slot, <see cref="Role.Reader"/> a card reader on a post,
    /// <see cref="Role.Door"/> a shutter door in a frame, <see cref="Role.Lift"/> a lift cab with sliding doors. Every prop stands on
    /// a pad and has a tall beacon post (readable at 30 m) with a lamp that turns green when its job is done. The prop's roles
    /// (ink outline): <see cref="SetObjective"/> gives every part of it the objective violet outline, which shows through walls.
    /// It faces the middle of the hall. Front is local +Z.
    /// </summary>
    public sealed class SkylineRelayProp
    {
        public enum Role { Button, Reel, Socket, Reader, Door, Lift }

        public static readonly Color Slate = new Color32(0x3A, 0x3F, 0x52, 255), Steel = new Color32(0x8A, 0x93, 0xA8, 255),
            Cream = new Color32(0xE9, 0xE3, 0xD0, 255), Orange = new Color32(0xFF, 0x9E, 0x3F, 255), Teal = new Color32(0x2F, 0xD3, 0xB5, 255),
            Done = new Color32(0x5C, 0xFF, 0x8A, 255), Ink = new Color32(0x16, 0x14, 0x1F, 255), Pad = new Color32(0xB9, 0xBF, 0xCD, 255),
            Amber = new Color32(0xFF, 0xB3, 0x47, 255), Paper = new Color32(0xF7, 0xF3, 0xE8, 255);

        public const float BeaconHeight = 4.5f, LabelHeight = 5.4f;

        public readonly Role role;
        public readonly int station;
        public readonly Transform root;

        readonly List<Material> _mats = new List<Material>();
        readonly List<Collider> _doorColliders = new List<Collider>();
        Material _lamp;
        Transform _press, _slabA, _slabB, _plug, _plugInSocket, _drum;
        Vector3 _slabAStart, _slabBStart, _pressStart;
        float _open, _openTarget, _pressT, _pressTarget, _pulse;
        bool _done;

        /// <summary>The flat radius round the station's centre in which the Unit counts as being at it.</summary>
        public float ZoneRadius => role == Role.Lift ? 1.7f : 2.6f;

        SkylineRelayProp(Role role, int station, Transform root)
        {
            this.role = role;
            this.station = station;
            this.root = root;
        }

        /// <summary>The cable reel's drum centre (where the cable is anchored).</summary>
        public Vector3 CableAnchor => _drum != null ? _drum.position : root.position + Vector3.up * 1.3f;
        /// <summary>The socket's mouth (where the cable plugs in).</summary>
        public Vector3 PlugPoint => root.TransformPoint(new Vector3(0f, 1.25f, 0.4f));
        /// <summary>Where the keycard pops out of the socket, and where it hangs waiting.</summary>
        public Vector3 CardSlot => root.TransformPoint(new Vector3(0f, 0.65f, 0.25f));
        public Vector3 CardHover => root.TransformPoint(new Vector3(0f, 1.15f, 1.0f));
        /// <summary>The label's world position, over the beacon.</summary>
        public Vector3 LabelPoint => root.position + Vector3.up * LabelHeight;

        // ------------------------------------------------------------------ building

        /// <summary>Builds the prop of <paramref name="role"/> at <paramref name="at"/> (feet level) facing <paramref name="faceTo"/>.</summary>
        public static SkylineRelayProp Create(Role role, int station, Vector3 at, Vector3 faceTo, Transform parent)
        {
            var go = new GameObject("Relay " + RelayName(role) + " " + (char)('A' + station));
            go.transform.SetParent(parent, false);
            Vector3 f = faceTo - at;
            f.y = 0f;
            go.transform.SetPositionAndRotation(at, f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f.normalized) : Quaternion.identity);
            var p = new SkylineRelayProp(role, station, go.transform);
            p.Build();
            return p;
        }

        static string RelayName(Role r) => r.ToString().ToLowerInvariant();

        Material Mat(Color c, bool lamp = false)
        {
            var m = SkylineInkRoles.Flat(c, SkylineInkRoles.None) ?? new Material(SkylineBotLook.Lit(c));
            if (lamp && m.HasProperty("_Unlit")) m.SetFloat("_Unlit", 1f);
            _mats.Add(m);
            return m;
        }

        /// <summary>A box part (a cube mesh); with a collider when <paramref name="solid"/>.</summary>
        GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 size, Color c, bool solid, bool lamp = false)
        {
            var go = SkylineBotLook.Visual(PrimitiveType.Cube, name, parent, Mat(c, lamp));
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            if (solid) go.AddComponent<BoxCollider>();
            return go;
        }

        /// <summary>A cylinder part with radius <paramref name="r"/> and length <paramref name="h"/> along local Y (rotate it for another axis).</summary>
        GameObject Cyl(Transform parent, string name, Vector3 pos, float r, float h, Color c, bool solid = false, bool lamp = false)
        {
            var go = SkylineBotLook.Visual(PrimitiveType.Cylinder, name, parent, Mat(c, lamp));
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
            if (solid) go.AddComponent<BoxCollider>();
            return go;
        }

        void Build()
        {
            var t = root;
            // the pad and the beacon: every station has them
            Cyl(t, "Pad", new Vector3(0f, 0.03f, 0f), role == Role.Lift ? 2.3f : 2.0f, 0.06f, Pad);
            Cube(t, "Beacon Post", new Vector3(-1.5f, BeaconHeight * 0.5f, -1.1f), new Vector3(0.18f, BeaconHeight, 0.18f), Slate, true);
            var lampGo = SkylineBotLook.Visual(PrimitiveType.Sphere, "Beacon Lamp", t, _lamp = Mat(Teal, true));
            lampGo.transform.localPosition = new Vector3(-1.5f, BeaconHeight + 0.2f, -1.1f);
            lampGo.transform.localScale = Vector3.one * 0.6f;

            switch (role)
            {
                case Role.Button: BuildButton(t); break;
                case Role.Reel: BuildReel(t); break;
                case Role.Socket: BuildSocket(t); break;
                case Role.Reader: BuildReader(t); break;
                case Role.Door: BuildDoor(t); break;
                default: BuildLift(t); break;
            }
        }

        void BuildButton(Transform t)
        {
            Cube(t, "Pedestal", new Vector3(0f, 0.5f, 0f), new Vector3(1.1f, 1.0f, 1.1f), Slate, true);
            Cyl(t, "Ring", new Vector3(0f, 1.04f, 0f), 0.62f, 0.1f, Ink);
            var b = Cyl(t, "Button", new Vector3(0f, 1.2f, 0f), 0.48f, 0.26f, Orange);
            _press = b.transform;
            _pressStart = _press.localPosition;
            Cube(t, "Sign Board", new Vector3(0f, 2.1f, -0.45f), new Vector3(1.3f, 0.9f, 0.1f), Slate, false);
            Cyl(t, "Sign Dot", new Vector3(0f, 2.1f, -0.38f), 0.28f, 0.06f, Orange).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void BuildReel(Transform t)
        {
            Cube(t, "Stand Left", new Vector3(-0.8f, 0.8f, 0f), new Vector3(0.2f, 1.6f, 0.9f), Slate, true);
            Cube(t, "Stand Right", new Vector3(0.8f, 0.8f, 0f), new Vector3(0.2f, 1.6f, 0.9f), Slate, true);
            var drum = Cyl(t, "Drum", new Vector3(0f, 1.3f, 0f), 0.62f, 1.3f, Cream, true);
            drum.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            _drum = drum.transform;
            Cyl(t, "Coil", new Vector3(0f, 1.3f, 0f), 0.52f, 1.5f, Ink).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Cyl(t, "Flange Left", new Vector3(-0.68f, 1.3f, 0f), 0.78f, 0.08f, Steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Cyl(t, "Flange Right", new Vector3(0.68f, 1.3f, 0f), 0.78f, 0.08f, Steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // the cable's loose end, hung on the front of the stand
            var plug = Cube(t, "Cable End", new Vector3(0f, 0.7f, 0.75f), new Vector3(0.22f, 0.22f, 0.42f), Amber, false);
            _plug = plug.transform;
        }

        void BuildSocket(Transform t)
        {
            Cube(t, "Panel", new Vector3(0f, 1.0f, 0f), new Vector3(1.6f, 2.0f, 0.3f), Slate, true);
            Cyl(t, "Socket Ring", new Vector3(0f, 1.25f, 0.17f), 0.34f, 0.08f, Teal).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Cyl(t, "Socket Hole", new Vector3(0f, 1.25f, 0.2f), 0.22f, 0.08f, Ink).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Cube(t, "Card Slot", new Vector3(0f, 0.62f, 0.16f), new Vector3(0.9f, 0.08f, 0.06f), Ink, false);
            var plug = Cube(t, "Plug", new Vector3(0f, 1.25f, 0.4f), new Vector3(0.22f, 0.22f, 0.42f), Amber, false);
            _plugInSocket = plug.transform;
            _plugInSocket.gameObject.SetActive(false);
        }

        void BuildReader(Transform t)
        {
            Cube(t, "Post", new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 1.4f, 0.4f), Slate, true);
            var head = Cube(t, "Head", new Vector3(0f, 1.55f, 0.08f), new Vector3(0.8f, 0.55f, 0.45f), Steel, true);
            head.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            Cube(t, "Slot", new Vector3(0f, 1.74f, 0.3f), new Vector3(0.7f, 0.06f, 0.08f), Ink, false).transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            Cube(t, "Screen", new Vector3(0f, 1.46f, 0.34f), new Vector3(0.5f, 0.18f, 0.06f), Amber, false, true).transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        }

        void BuildDoor(Transform t)
        {
            Cube(t, "Post Left", new Vector3(-1.75f, 1.7f, 0f), new Vector3(0.5f, 3.4f, 0.5f), Slate, true);
            Cube(t, "Post Right", new Vector3(1.75f, 1.7f, 0f), new Vector3(0.5f, 3.4f, 0.5f), Slate, true);
            Cube(t, "Lintel", new Vector3(0f, 3.65f, 0f), new Vector3(4.0f, 0.5f, 0.5f), Slate, true);
            for (int i = 0; i < 6; i++)
                Cube(t, "Stripe " + i, new Vector3(-1.5f + i * 0.6f, 3.65f, 0.27f), new Vector3(0.3f, 0.34f, 0.05f), i % 2 == 0 ? Orange : Ink, false);
            var slab = Cube(t, "Shutter", new Vector3(0f, 1.7f, 0f), new Vector3(3.0f, 3.4f, 0.22f), Steel, true);
            for (int i = 0; i < 6; i++)
                Cube(slab.transform, "Rib " + i, new Vector3(0f, -0.42f + i * 0.17f, 0.6f), new Vector3(1f, 0.04f, 0.5f), Slate, false);
            _slabA = slab.transform;
            _slabAStart = _slabA.localPosition;
            _doorColliders.Add(slab.GetComponent<Collider>());
        }

        void BuildLift(Transform t)
        {
            Cube(t, "Floor", new Vector3(0f, 0.1f, 0f), new Vector3(3.0f, 0.2f, 3.0f), Slate, true);
            Cube(t, "Back Wall", new Vector3(0f, 1.7f, -1.4f), new Vector3(3.0f, 3.2f, 0.24f), Steel, true);
            Cube(t, "Wall Left", new Vector3(-1.4f, 1.7f, 0f), new Vector3(0.24f, 3.2f, 3.0f), Steel, true);
            Cube(t, "Wall Right", new Vector3(1.4f, 1.7f, 0f), new Vector3(0.24f, 3.2f, 3.0f), Steel, true);
            Cube(t, "Roof", new Vector3(0f, 3.4f, 0f), new Vector3(3.2f, 0.3f, 3.2f), Slate, true);
            Cube(t, "Light Panel", new Vector3(0f, 3.1f, 1.3f), new Vector3(1.8f, 0.2f, 0.1f), Teal, false, true);
            var a = Cube(t, "Door Left", new Vector3(-0.38f, 1.7f, 1.4f), new Vector3(0.72f, 3.0f, 0.14f), Orange, true);
            var b = Cube(t, "Door Right", new Vector3(0.38f, 1.7f, 1.4f), new Vector3(0.72f, 3.0f, 0.14f), Orange, true);
            _slabA = a.transform;
            _slabB = b.transform;
            _slabAStart = _slabA.localPosition;
            _slabBStart = _slabB.localPosition;
            _doorColliders.Add(a.GetComponent<Collider>());
            _doorColliders.Add(b.GetComponent<Collider>());
        }

        // ------------------------------------------------------------------ states

        /// <summary>Gives every part the objective outline (violet, through walls) or takes it away.</summary>
        public void SetObjective(bool on)
        {
            int r = on ? SkylineInkRoles.Objective : SkylineInkRoles.None;
            foreach (var m in _mats) SkylineInkRoles.SetRole(m, r);
        }

        /// <summary>The prop's own job is done: its lamp turns green, and the part that moves does (the button presses, the cable's
        /// end leaves the stand, the plug sits in the socket).</summary>
        public void Complete()
        {
            _done = true;
            if (_lamp != null && _lamp.HasProperty("_BaseColor")) _lamp.SetColor("_BaseColor", Done);
            _pressTarget = 1f;
            if (role == Role.Reel && _plug != null) _plug.gameObject.SetActive(false);
            if (role == Role.Socket && _plugInSocket != null) _plugInSocket.gameObject.SetActive(true);
            _pulse = 1f;
        }

        public void Pulse() => _pulse = 1f;

        /// <summary>Door and lift: slide open (the door's colliders go when it is mostly open).</summary>
        public void Open()
        {
            _openTarget = 1f;
        }

        public bool IsOpen => _open > 0.98f;

        public void Tick(float dt)
        {
            _pressT = Mathf.MoveTowards(_pressT, _pressTarget, dt * 8f);
            if (_press != null) _press.localPosition = _pressStart + Vector3.down * 0.14f * _pressT;
            if (_open != _openTarget)
            {
                _open = Mathf.MoveTowards(_open, _openTarget, dt / 1.1f);
                float e = _open * _open * (3f - 2f * _open);
                if (role == Role.Door && _slabA != null) _slabA.localPosition = _slabAStart + Vector3.up * (3.1f * e);
                if (role == Role.Lift && _slabA != null && _slabB != null)
                {
                    _slabA.localPosition = _slabAStart + Vector3.left * (0.62f * e);
                    _slabB.localPosition = _slabBStart + Vector3.right * (0.62f * e);
                }
                if (_open > 0.6f) foreach (var c in _doorColliders) if (c != null && c.enabled) c.enabled = false;
            }
            if (_pulse > 0f)
            {
                _pulse = Mathf.Max(0f, _pulse - dt / 0.4f);
                float s = 1f + 0.12f * Mathf.Sin(_pulse * Mathf.PI);
                root.localScale = new Vector3(s, s, s);
            }
        }

        public void Destroy()
        {
            foreach (var m in _mats) if (m != null) Object.Destroy(m);
            if (root != null) Object.Destroy(root.gameObject);
        }
    }
}
