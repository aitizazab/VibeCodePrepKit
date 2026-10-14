using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Dot's tow drone for one asset (task R2): it comes down over the asset when its meter starts, hangs there with a beam of
    /// moving violet stripes (a stack of glowing discs travelling from the drone to the asset) and a violet spot light, and
    /// goes up with the asset when the tow is done. The beam is bright while the Unit stands in the circle and dim while the
    /// tow is paused. Not a MonoBehaviour: <see cref="SkylineRepossess"/> drives it.
    /// <para>Dot's drone is white and gold like CONCIERGE's fleet's opposite number, never the hostile red-magenta; its only glow
    /// is the objective violet.</para>
    /// </summary>
    public sealed class SkylineRepossessDrone
    {
        /// <summary>How high over the socket's floor point the drone hangs (m), how far above that it starts its descent, and the
        /// seconds that descent takes.</summary>
        public const float Hover = 3.3f, ArriveFrom = 6f, ArriveSeconds = 0.8f;
        /// <summary>Stripes in the beam and how fast they travel (stripe spacings per second).</summary>
        public const int Stripes = 7;
        const float StripeSpeed = 1.4f;

        readonly GameObject _root;
        readonly Transform _rotor, _beam;
        readonly Transform[] _stripes = new Transform[Stripes];
        readonly Light _spot;
        readonly Vector3 _floor;
        float _arrive = -1f, _phase, _lift;
        bool _beamBright, _leaving;

        public GameObject Root => _root;
        /// <summary>True from the descent's first frame until it is reset.</summary>
        public bool Present => _arrive >= 0f;
        /// <summary>True when the beam is at full strength (the Unit is in the circle).</summary>
        public bool BeamBright => _beamBright;
        public Light Spot => _spot;
        public int StripeCount => Stripes;

        SkylineRepossessDrone(Vector3 floor, Transform parent, string name)
        {
            _floor = floor;
            _root = new GameObject(name);
            if (parent != null) _root.transform.SetParent(parent, false);

            var white = SkylineBotLook.Lit(SkylineBotLook.White);
            var gold = SkylineBotLook.Lit(SkylineBotLook.Gold);
            var dark = SkylineBotLook.Lit(SkylineBotLook.Dark);
            SkylineRepossessFx.Piece(PrimitiveType.Sphere, "Hull", _root.transform, Vector3.zero, new Vector3(0.7f, 0.34f, 0.7f), white);
            SkylineRepossessFx.Piece(PrimitiveType.Cylinder, "Ring", _root.transform, new Vector3(0f, -0.05f, 0f), new Vector3(0.95f, 0.025f, 0.95f), gold);
            SkylineRepossessFx.Piece(PrimitiveType.Sphere, "Lens", _root.transform, new Vector3(0f, -0.17f, 0f), new Vector3(0.22f, 0.12f, 0.22f), dark);
            _rotor = new GameObject("Rotor").transform;
            _rotor.SetParent(_root.transform, false);
            _rotor.localPosition = new Vector3(0f, 0.2f, 0f);
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Blade A", _rotor, Vector3.zero, new Vector3(1.0f, 0.02f, 0.09f), dark);
            SkylineRepossessFx.Piece(PrimitiveType.Cube, "Blade B", _rotor, Vector3.zero, new Vector3(0.09f, 0.02f, 1.0f), dark);

            // the beam: flat glowing discs, widening toward the asset; they slide down the beam and wrap
            _beam = new GameObject("Beam").transform;
            _beam.SetParent(_root.transform, false);
            var glow = SkylineBotLook.Glow(SkylineRepossessFx.VioletHdr);
            for (int i = 0; i < Stripes; i++)
                _stripes[i] = SkylineRepossessFx.Piece(PrimitiveType.Cylinder, "Stripe " + i, _beam, Vector3.zero, new Vector3(0.5f, 0.012f, 0.5f), glow).transform;

            var aim = new GameObject("Aim").transform;
            aim.SetParent(_root.transform, false);
            aim.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _spot = SkylineRepossessFx.MakeLight(aim, "Beam light", LightType.Spot, SkylineRepossessFx.VioletLight, 0f, Hover + 3f, 55f);

            _root.SetActive(false);
        }

        /// <summary>Makes the drone for an asset whose floor point is <paramref name="floor"/> (hidden until <see cref="Arrive"/>).</summary>
        public static SkylineRepossessDrone Build(Transform parent, Vector3 floor, string name) => new SkylineRepossessDrone(floor, parent, name);

        /// <summary>Starts the descent (the meter has started). No-op when it is already there.</summary>
        public void Arrive()
        {
            if (_arrive >= 0f) return;
            _arrive = 0f;
            _leaving = false;
            _lift = 0f;
            _root.SetActive(true);
            Place(0f);
        }

        /// <summary>The tow is done: the drone goes up with the asset (at the winch's pace, <see cref="SkylineRepossessAsset.WinchSeconds"/>).</summary>
        public void Leave() => _leaving = true;

        /// <summary>Sets whether the beam is at full strength. Call it every frame.</summary>
        public void SetBeam(bool bright) => _beamBright = bright;

        void Place(float arrive01)
        {
            float h = Mathf.Lerp(Hover + ArriveFrom, Hover, SkylineRepossessFx.Smooth01(arrive01)) + _lift;
            _root.transform.position = _floor + new Vector3(0f, h, 0f);
        }

        /// <summary>Advances the descent, the rotor, the stripes and the light.</summary>
        public void Tick(float dt)
        {
            if (_arrive < 0f || !_root.activeSelf) return;
            if (_arrive < ArriveSeconds) _arrive = Mathf.Min(ArriveSeconds, _arrive + Mathf.Max(0f, dt));
            if (_leaving) _lift += SkylineRepossessAsset.WinchHeight * Mathf.Max(0f, dt) / SkylineRepossessAsset.WinchSeconds;
            Place(_arrive / ArriveSeconds);
            _rotor.Rotate(0f, 900f * dt, 0f);

            // the beam runs from just under the hull down to the asset's top (about 2 m over the floor point)
            float height = Hover - 1.9f;                 // hull to the asset's top: constant, the pair rises together
            height = Mathf.Max(0.2f, height);
            bool show = _arrive >= ArriveSeconds && (!_leaving || _lift < SkylineRepossessAsset.WinchHeight * 0.9f);
            _phase = (_phase + dt * StripeSpeed) % 1f;
            for (int i = 0; i < Stripes; i++)
            {
                float t = ((float)i / Stripes + _phase) % 1f;               // 0 at the drone, 1 at the asset
                var tr = _stripes[i];
                tr.localPosition = new Vector3(0f, -0.2f - t * height, 0f);
                float w = Mathf.Lerp(0.35f, 2.1f, t) * (_beamBright ? 1f : 0.7f);
                tr.localScale = new Vector3(w, 0.012f, w);
                tr.gameObject.SetActive(show && (_beamBright || i % 2 == 0));
            }
            _spot.intensity = _arrive >= ArriveSeconds ? (_beamBright ? 6f : 1.2f) : 0f;
        }

        /// <summary>A restart: the drone is gone until the next meter start.</summary>
        public void Reset()
        {
            _arrive = -1f;
            _leaving = false;
            _lift = 0f;
            _beamBright = false;
            _root.SetActive(false);
        }
    }
}
