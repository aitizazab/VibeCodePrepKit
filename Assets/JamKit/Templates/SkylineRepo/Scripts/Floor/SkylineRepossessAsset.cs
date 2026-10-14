using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// One asset on its socket (task R2): a placeholder made of primitives until the real art exists, in the objective ink role
    /// (violet outline, also drawn through walls by the x-ray pass), with a clamp that closes round its base while its tow runs and
    /// a winch that hoists it away in <see cref="WinchSeconds"/> once the tow is done. Not a MonoBehaviour: <see cref="SkylineRepossess"/>
    /// drives it, so no scene has to save it.
    /// </summary>
    public sealed class SkylineRepossessAsset
    {
        /// <summary>The winch (docs/SKYLINE-PLAN-FLOOR1.md, R2): the asset leaves in 1.2 s.</summary>
        public const float WinchSeconds = 1.2f;
        /// <summary>How far up the winch takes it before it is gone (m), and the share of its size it shrinks to.</summary>
        public const float WinchHeight = 7f, WinchEndScale = 0.12f;
        /// <summary>The clamp jaws' open and closed half-gaps (m) round the base.</summary>
        public const float JawOpen = 1.25f, JawClosed = 0.62f, JawY = 0.18f;

        public readonly JobEntry entry;
        readonly GameObject _root;
        readonly Transform _body, _jawA, _jawB;
        readonly Vector3 _base;
        float _winchT = -1f, _clamp, _shake;
        bool _gone;

        /// <summary>The root object (at the socket's floor point).</summary>
        public GameObject Root => _root;
        /// <summary>True from the first frame of the winch to the end (the asset is leaving).</summary>
        public bool Winching => _winchT >= 0f && !_gone;
        /// <summary>True once the winch has finished: the asset is gone from the floor.</summary>
        public bool Gone => _gone;
        /// <summary>The share of the winch done (0..1).</summary>
        public float WinchProgress => _winchT < 0f ? 0f : Mathf.Clamp01(_winchT / WinchSeconds);
        /// <summary>The world position of the asset's centre (about a metre over the floor point).</summary>
        public Vector3 Centre => _body != null ? _body.position + Vector3.up * 0.9f : _base;

        SkylineRepossessAsset(JobEntry e, Transform parent)
        {
            entry = e;
            _base = SkylineRepossessFx.U(e.socket.position);
            _root = new GameObject("Asset " + e.asset.id);
            if (parent != null) _root.transform.SetParent(parent, false);
            _root.transform.position = _base;
            _body = new GameObject("Body").transform;
            _body.SetParent(_root.transform, false);
            BuildBody(e.asset.kind, _body);

            // the clamp: two jaws that close in on the base while a tow runs
            var jaw = SkylineBotLook.Lit(SkylineBotLook.Grey, SkylineInkRoles.Objective);
            _jawA = SkylineRepossessFx.Piece(PrimitiveType.Cube, "Clamp jaw A", _root.transform, new Vector3(-JawOpen, JawY, 0f), new Vector3(0.22f, 0.3f, 1.3f), jaw).transform;
            _jawB = SkylineRepossessFx.Piece(PrimitiveType.Cube, "Clamp jaw B", _root.transform, new Vector3(JawOpen, JawY, 0f), new Vector3(0.22f, 0.3f, 1.3f), jaw).transform;
        }

        public static SkylineRepossessAsset Build(Transform parent, JobEntry e) => new SkylineRepossessAsset(e, parent);

        // Placeholder bodies, all in the objective role so they read violet-outlined and show through walls. Sizes are real
        // (a mascot 1.9 m, a chair 1.1 m, a cabinet 1.85 m) so the circle and the clamp read right.
        static void BuildBody(AssetKind kind, Transform body)
        {
            var o = SkylineInkRoles.Objective;
            switch (kind)
            {
                case AssetKind.Mascot:
                {
                    var fur = SkylineBotLook.Lit(new Color(0.92f, 0.55f, 0.2f), o);
                    var cream = SkylineBotLook.Lit(new Color(0.96f, 0.9f, 0.78f), o);
                    SkylineRepossessFx.Piece(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 0.7f, 0.7f), fur);
                    SkylineRepossessFx.Piece(PrimitiveType.Sphere, "Belly", body, new Vector3(0f, 0.75f, -0.28f), new Vector3(0.6f, 0.7f, 0.3f), cream);
                    SkylineRepossessFx.Piece(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 1.62f, 0f), new Vector3(0.85f, 0.8f, 0.8f), fur);
                    SkylineRepossessFx.Piece(PrimitiveType.Sphere, "Ear L", body, new Vector3(-0.36f, 2.02f, 0f), new Vector3(0.3f, 0.3f, 0.12f), fur);
                    SkylineRepossessFx.Piece(PrimitiveType.Sphere, "Ear R", body, new Vector3(0.36f, 2.02f, 0f), new Vector3(0.3f, 0.3f, 0.12f), fur);
                    SkylineRepossessFx.Piece(PrimitiveType.Sphere, "Snout", body, new Vector3(0f, 1.52f, -0.34f), new Vector3(0.4f, 0.28f, 0.22f), cream);
                    break;
                }
                case AssetKind.MassageChair:
                {
                    var leather = SkylineBotLook.Lit(new Color(0.42f, 0.26f, 0.22f), o);
                    var steel = SkylineBotLook.Lit(SkylineBotLook.Grey, o);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Base", body, new Vector3(0f, 0.12f, 0f), new Vector3(1.0f, 0.24f, 1.25f), steel);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Seat", body, new Vector3(0f, 0.45f, -0.05f), new Vector3(0.95f, 0.4f, 0.95f), leather);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Back", body, new Vector3(0f, 0.95f, 0.5f), new Vector3(0.95f, 1.0f, 0.3f), leather);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Arm L", body, new Vector3(-0.58f, 0.7f, -0.05f), new Vector3(0.2f, 0.25f, 0.9f), leather);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Arm R", body, new Vector3(0.58f, 0.7f, -0.05f), new Vector3(0.2f, 0.25f, 0.9f), leather);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Footrest", body, new Vector3(0f, 0.22f, -0.75f), new Vector3(0.8f, 0.12f, 0.4f), steel);
                    break;
                }
                default:
                {
                    var cab = SkylineBotLook.Lit(new Color(0.18f, 0.2f, 0.42f), o);
                    var trim = SkylineBotLook.Lit(new Color(0.9f, 0.82f, 0.2f), o);
                    var screen = SkylineBotLook.Glow(new Color(0.5f, 1.4f, 1.1f), o);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Cabinet", body, new Vector3(0f, 0.9f, 0.1f), new Vector3(0.85f, 1.8f, 0.8f), cab);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Screen", body, new Vector3(0f, 1.3f, -0.31f), new Vector3(0.62f, 0.5f, 0.06f), screen);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Control deck", body, new Vector3(0f, 0.92f, -0.42f), new Vector3(0.8f, 0.14f, 0.34f), trim);
                    SkylineRepossessFx.Piece(PrimitiveType.Cube, "Marquee", body, new Vector3(0f, 1.74f, -0.28f), new Vector3(0.8f, 0.2f, 0.3f), trim);
                    break;
                }
            }
        }

        /// <summary>Sets the clamp: 0 open, 1 shut round the base (smooth between), and how hard the asset shakes in it (0..1,
        /// scaled by the meter).</summary>
        public void SetClamp(float closed, float shake)
        {
            _clamp = Mathf.Clamp01(closed);
            _shake = Mathf.Clamp01(shake);
        }

        public float Clamp => _clamp;

        /// <summary>The tow is done: hoist the asset away over <see cref="WinchSeconds"/>.</summary>
        public void StartWinch()
        {
            if (_winchT >= 0f) return;
            _winchT = 0f;
        }

        /// <summary>Advances the clamp, the shake and the winch by <paramref name="dt"/>.</summary>
        public void Tick(float dt)
        {
            if (_gone || _root == null) return;
            float open = Mathf.Lerp(JawOpen, JawClosed, SkylineRepossessFx.Smooth01(_clamp));
            float jawY = JawY;

            if (_winchT < 0f)
            {
                float s = _shake * 0.025f * Mathf.Sin(Time.time * 47f);
                _body.localPosition = new Vector3(s, 0f, 0f);
                _jawA.localPosition = new Vector3(-open, jawY, 0f);
                _jawB.localPosition = new Vector3(open, jawY, 0f);
                return;
            }
            _winchT += Mathf.Max(0f, dt);
            float p = Mathf.Clamp01(_winchT / WinchSeconds);
            float rise = WinchHeight * p * p;                              // eases in, like a winch taking the load
            _body.localPosition = new Vector3(0f, rise, 0f);
            float scale = Mathf.Lerp(1f, WinchEndScale, SkylineRepossessFx.Smooth01(p));
            _body.localScale = new Vector3(scale, scale, scale);
            _jawA.localPosition = new Vector3(-open, jawY + rise, 0f);
            _jawB.localPosition = new Vector3(open, jawY + rise, 0f);
            if (p >= 1f)
            {
                _gone = true;
                _root.SetActive(false);
            }
        }

        /// <summary>A restart: the asset is back on its socket, whole, with the clamp open.</summary>
        public void Restore()
        {
            if (_root == null) return;
            _winchT = -1f;
            _gone = false;
            _clamp = 0f;
            _shake = 0f;
            _body.localPosition = Vector3.zero;
            _body.localScale = Vector3.one;
            _jawA.localPosition = new Vector3(-JawOpen, JawY, 0f);
            _jawB.localPosition = new Vector3(JawOpen, JawY, 0f);
            _root.SetActive(true);
        }
    }
}
