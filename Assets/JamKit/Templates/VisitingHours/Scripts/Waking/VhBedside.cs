using System.Collections.Generic;
using JamKit.Doomlike;
using JamKit.VisitingHours.Characters;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The bedside, shown (docs/redesign/day.md; the creator: "a short animation of sitting down, holding Elias' hand, and
    /// getting up after the player puts in an input"). In first person: she sits (the view glides to the chair and lowers,
    /// turns to him, and the room softens behind his hand), her hand comes into frame and takes his, she talks (the ink:
    /// the topics), and his body answers on screen: the monitor's numbers climb or settle, his fingers move against hers.
    /// When she's said what she'll say, "E  Stand up": she lets go and rises. Owned and ticked by VisitingHoursGame
    /// (VisitingHoursGame.Day.cs); everything it builds (her arm) goes when she stands.
    /// </summary>
    public sealed class VhBedside
    {
        public enum Phase { None, Sitting, Reaching, Talking, Ready, Releasing, Rising, Done }

        public const float SitSeconds = 1.6f, ReachSeconds = 1.0f, ReleaseSeconds = 0.7f, RiseSeconds = 1.3f;
        static readonly Color ScrubsNavy = new Color(0.2f, 0.27f, 0.42f);
        static readonly Color NadiaSkin = new Color(0.82f, 0.66f, 0.56f);

        readonly DoomPlayer _player;
        readonly VhArea _area;
        readonly VhMeshKit _meshes = new VhMeshKit();
        readonly List<Material> _mats = new List<Material>();
        readonly VhWakingStyle _style;
        Phase _phase;
        float _t, _clock;
        Vector3 _fromFeet;
        float _fromYaw, _fromPitch, _standEye;
        Vector3 _seatFeet;
        float _seatEye;

        // Her arm: the shoulder (fixed under the view), the upper arm, the forearm, the hand.
        Transform _armRoot, _upperSleeve, _upperSkin, _fore, _foreBone;
        VhHandRig _hand;
        VhKeepsakeWatch _watch;
        Vector3 _shoulder, _restWrist, _holdWrist, _pole;
        Quaternion _restRot, _holdRot;
        float _squeeze, _squeezeLeft;

        public Phase Current => _phase;
        public bool Active => _phase != Phase.None && _phase != Phase.Done;
        /// <summary>Her hand is on his (the talk can start).</summary>
        public bool Holding => _phase == Phase.Talking || _phase == Phase.Ready;

        public VhBedside(DoomPlayer player, VhArea area, VhWakingStyle style)
        {
            _player = player;
            _area = area;
            _style = style;
        }

        /// <summary>She sits down (the view moves to the chair).</summary>
        public void Begin()
        {
            _fromFeet = _player.transform.position;
            _fromYaw = _player.Yaw;
            _fromPitch = _player.Pitch;
            _standEye = _player.head != null ? _player.head.localPosition.y : 1.6f;
            _seatFeet = new Vector3(_area.bedsideEye.x, _area.root.position.y + 0.05f, _area.bedsideEye.z);
            _seatEye = _area.bedsideEye.y - 0.05f;
            _phase = Phase.Sitting;
            _t = 0f;
            // The 3D model's hand is where its animation put it: hold it there (the back of his hand, along his fingers).
            if (_area.eliasFingers != null && _area.eliasFingers.Ready)
            {
                _area.bedsideHold = _area.eliasFingers.HoldPoint;
                _area.bedsideHandForward = _area.eliasFingers.Fingers;
            }
            BuildArm();
            SetArm(0f);
        }

        /// <summary>She's said what she'll say: wait for E to stand up.</summary>
        public void ReadyToRise()
        {
            if (_phase == Phase.Talking) _phase = Phase.Ready;
        }

        /// <summary>E: she lets go and stands.</summary>
        public void Rise()
        {
            if (_phase != Phase.Ready && _phase != Phase.Talking) return;
            _phase = Phase.Releasing;
            _t = 0f;
        }

        /// <summary>His body answers what she's saying: "fingers" (his fingers move against hers), "heart" (the monitor
        /// climbs), "calm" (it settles while she holds his hand).</summary>
        public void Respond(string kind)
        {
            var mon = _area.patientMonitor;
            float rest = mon != null ? mon.restingRate : 62f;
            switch (kind)
            {
                case "fingers":
                    _area.TwitchElias(0.55f, 1.8f);
                    if (mon != null) mon.Raise(rest + 8f, 9f);
                    _squeeze = 0.2f;
                    _squeezeLeft = 1.4f;
                    break;
                case "heart":
                    if (mon != null) mon.Raise(rest + 16f, 14f);
                    _squeeze = 0.15f;
                    _squeezeLeft = 1.8f;
                    break;
                case "calm":
                    if (mon != null) mon.Calm(rest - 5f, 22f);
                    if (_hand != null) _hand.stroke = 1f;
                    break;
            }
        }

        /// <summary>One frame. Returns true while it still runs.</summary>
        public bool Tick(float dt)
        {
            if (_phase == Phase.None || _phase == Phase.Done) return false;
            _t += dt;
            _clock += dt;
            switch (_phase)
            {
                case Phase.Sitting:
                {
                    float u = VhUtil.Ease(_t / SitSeconds);
                    // The view glides to the chair, a little lower in the middle of the move (she bends to sit).
                    var feet = Vector3.Lerp(_fromFeet, _seatFeet, u);
                    float eye = Mathf.Lerp(_standEye, _seatEye, u) - Mathf.Sin(u * Mathf.PI) * 0.05f;
                    Place(feet, eye, Mathf.LerpAngle(_fromYaw, _area.bedsideYaw, u), Mathf.Lerp(_fromPitch, _area.bedsidePitch, u));
                    if (_area.bedsideFocus != null) _area.bedsideFocus.weight = u;
                    if (_t >= SitSeconds) { _phase = Phase.Reaching; _t = 0f; }
                    break;
                }
                case Phase.Reaching:
                {
                    Breathe();
                    float u = VhUtil.Ease(_t / ReachSeconds);
                    SetArm(u);
                    if (_t >= ReachSeconds) { _phase = Phase.Talking; _t = 0f; }
                    break;
                }
                case Phase.Talking:
                case Phase.Ready:
                    Breathe();
                    SetArm(1f);
                    break;
                case Phase.Releasing:
                {
                    Breathe();
                    float u = 1f - VhUtil.Ease(_t / ReleaseSeconds);
                    SetArm(u);
                    if (_t >= ReleaseSeconds) { _phase = Phase.Rising; _t = 0f; HideArm(); }
                    break;
                }
                case Phase.Rising:
                {
                    float u = VhUtil.Ease(_t / RiseSeconds);
                    var stand = _area.bedsideStand + Vector3.up * 0.05f;
                    var feet = Vector3.Lerp(_seatFeet, stand, u);
                    float eye = Mathf.Lerp(_seatEye, _standEye, u);
                    Place(feet, eye, _area.bedsideYaw, Mathf.Lerp(_area.bedsidePitch, 4f, u));
                    if (_area.bedsideFocus != null) _area.bedsideFocus.weight = 1f - u;
                    if (_t >= RiseSeconds)
                    {
                        _phase = Phase.Done;
                        _player.ResetView();
                        Dispose();
                        return false;
                    }
                    break;
                }
            }
            return true;
        }

        void Breathe()
        {
            if (_player.head == null) return;
            var p = _player.head.localPosition;
            p.y = _seatEye + Mathf.Sin(_clock * 1.6f) * 0.004f;
            _player.head.localPosition = p;
        }

        void Place(Vector3 feet, float eye, float yaw, float pitch)
        {
            _player.Teleport(feet);
            if (_player.head != null) _player.head.localPosition = new Vector3(0f, eye, 0f);
            _player.Look(0f, pitch - _player.Pitch);
            _player.FaceYaw(yaw);
        }

        // ---------- Her arm ----------

        void BuildArm()
        {
            var lib = VhWardLibrary.Load();
            Material Mat(Color c, string surface)
            {
                var m = lib != null ? lib.Colour(surface, c, _mats) : null;
                return m != null ? m : _style.Mat(c);
            }
            var skin = Mat(NadiaSkin, "skin");
            var sleeve = Mat(ScrubsNavy, "fabric");
            _armRoot = new GameObject("Nadia's arm (bedside)").transform;
            _armRoot.SetParent(_area.root, false);
            _upperSleeve = Segment("Upper arm (scrubs)", _meshes.Cylinder(0.056f, 0.05f, 1f, 16), sleeve);
            _upperSkin = Segment("Upper arm", _meshes.Cylinder(0.042f, 0.04f, 1f, 16), skin);
            _fore = Segment("Forearm", _meshes.Cylinder(0.038f, 0.03f, 1f, 16), skin);
            // Named as a Mixamo rig's bones (RightForeArm, RightHand...), so People's VhKeepsakeWatch can sit on this arm too.
            _foreBone = new GameObject("RightForeArm").transform;
            _foreBone.SetParent(_armRoot, false);
            var handGo = new GameObject("RightHand");
            handGo.transform.SetParent(_armRoot, false);
            _hand = handGo.AddComponent<VhHandRig>();
            _hand.Build(_meshes, skin, null, 0.96f, -1f, 0.02f);
            _hand.curl = 0.12f;
            _hand.thumbCurl = 0.1f;
            Marker("RightHandMiddle1", handGo.transform, new Vector3(0f, 0f, 0.09f));
            Marker("RightHandThumb1", handGo.transform, new Vector3(-0.04f, -0.005f, 0.025f));

            // Where it all hangs, from the seated view: the shoulder below and right of her eyes; the rest pose below the
            // frame; the hold on the back of his hand, her fingers over its far edge.
            var eye = _area.bedsideEye;
            var yawRot = Quaternion.Euler(0f, _area.bedsideYaw, 0f);
            var fwd = yawRot * Vector3.forward;
            var right = yawRot * Vector3.right;
            _shoulder = eye + right * 0.2f - Vector3.up * 0.3f - fwd * 0.04f;
            _restWrist = _shoulder + Vector3.down * 0.42f + fwd * 0.12f + right * 0.05f;
            _restRot = Quaternion.LookRotation(Vector3.down * 0.7f + fwd * 0.3f, -right);
            var along = _area.bedsideHold - _shoulder;
            along.y = 0f;
            along = along.sqrMagnitude > 1e-4f ? along.normalized : fwd;
            _holdRot = Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);
            _holdWrist = _area.bedsideHold - _holdRot * new Vector3(0f, 0f, 0.05f) + Vector3.up * 0.018f;
            _pole = _shoulder + right * 0.35f + Vector3.down * 0.25f;
            WearTheWatch();
        }

        static void Marker(string name, Transform parent, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
        }

        /// <summary>Her forearm's radius at the wrist (the strap has to go round it).</summary>
        const float WristRadius = 0.031f;

        /// <summary>
        /// Lena's keepsake (Heard out), her star-shaped watch, on Nadia's right wrist when the chain carries it into the waking
        /// world (<see cref="Chain.HasLenaWatch"/>): People's <see cref="VhKeepsakeWatch"/> on this arm (docs/redesign/people.md).
        /// </summary>
        void WearTheWatch()
        {
            if (!JamKit.Shell.GameRoot.Exists || !Chain.HasLenaWatch(JamKit.Shell.GameRoot.Save.Data)) return;
            SetArm(1f);
            _watch = VhKeepsakeWatch.Attach(_armRoot);
            if (_watch == null) return;
            // This forearm is rounder than a rig's wrist: the strap a little wider, the case lifted onto it.
            float lift = WristRadius + 0.002f - _watch.wrist.y;
            _watch.wrist = new Vector2(WristRadius + 0.002f, WristRadius + 0.002f);
            _watch.Build();
            _watch.transform.position += _watch.transform.up * lift;
            _watch.Apply();
            _watch.SetOwned(true);
        }

        /// <summary>The watch on her wrist now (null: the keepsake isn't hers).</summary>
        public VhKeepsakeWatch Watch => _watch;

        Transform Segment(string name, Mesh unitCylinder, Material mat)
        {
            var r = VhMeshKit.Show(_armRoot, name, unitCylinder, mat, Vector3.zero, Quaternion.identity);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return r.transform;
        }

        /// <summary>The arm between rest (0, below the frame) and holding his hand (1).</summary>
        void SetArm(float u)
        {
            if (_armRoot == null) return;
            // A small arc on the way (the hand rises then settles), and the grip closing at the end.
            var wrist = Vector3.Lerp(_restWrist, _holdWrist, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.06f;
            var rot = Quaternion.Slerp(_restRot, _holdRot, u);
            VhNadiaRig.SolveTwoBone(_shoulder, 0.29f, 0.27f, wrist, _pole, out var elbow, out var w);
            var sleeveEnd = Vector3.Lerp(_shoulder, elbow, 0.55f);
            Stretch(_upperSleeve, _shoulder, sleeveEnd);
            Stretch(_upperSkin, sleeveEnd, elbow);
            Stretch(_fore, elbow, w);
            _hand.transform.SetPositionAndRotation(w, rot);
            var along = w - elbow;
            if (along.sqrMagnitude > 1e-8f) _foreBone.SetPositionAndRotation(elbow, Quaternion.LookRotation(along, rot * Vector3.up));
            float grip = Mathf.Clamp01((u - 0.75f) / 0.25f);
            if (_squeezeLeft > 0f) _squeezeLeft -= Time.deltaTime;
            float squeeze = _squeezeLeft > 0f ? _squeeze * Mathf.Sin(Mathf.Clamp01(_squeezeLeft / 1.4f) * Mathf.PI) : 0f;
            _hand.curl = Mathf.Lerp(0.12f, 0.58f, grip) + squeeze;
            _hand.thumbCurl = Mathf.Lerp(0.1f, 0.35f, grip);
        }

        static void Stretch(Transform t, Vector3 from, Vector3 to)
        {
            var d = to - from;
            float len = d.magnitude;
            if (len < 1e-4f) { t.localScale = Vector3.zero; return; }
            t.SetPositionAndRotation(from, Quaternion.FromToRotation(Vector3.up, d / len));
            t.localScale = new Vector3(1f, len, 1f);
        }

        void HideArm()
        {
            if (_armRoot != null) _armRoot.gameObject.SetActive(false);
            _area.RestEliasHand();
        }

        /// <summary>Frees her arm (meshes, materials).</summary>
        public void Dispose()
        {
            if (_armRoot != null) VhUtil.Kill(_armRoot.gameObject);
            _armRoot = null;
            _meshes.Dispose();
            foreach (var m in _mats) VhUtil.Kill(m);
            _mats.Clear();
            if (_area.bedsideFocus != null) _area.bedsideFocus.weight = 0f;
        }
    }
}
