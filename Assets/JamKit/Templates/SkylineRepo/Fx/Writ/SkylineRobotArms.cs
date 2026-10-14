using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The robot arms' per-gun hold (Task 18). SkylineViewmodel instantiates Resources/SkylineModels/arms/arms under each
    /// gun's root: HandR and HandL (the hands), ForearmR and ForearmL (origin at the wrist) and ElbowR / ElbowL (empties at
    /// the forearms' far ends). The FBX holds the Writ.
    /// <list type="bullet">
    /// <item>The right hand holds both guns unchanged (the Writ's grip column sits on the Breacher's grip axis).</item>
    /// <item>The left hand cups the Writ's grip under the right hand; for the Breacher it cups the pump from below with its
    /// fingers up the receiver's left side, where the camera sees them.</item>
    /// <item>During the Breacher's reload the left hand leaves its hold, takes the cell as it drops, brings the new one up and
    /// lets go after the seat: it follows the clip's magOffset between the "cell_out" and "seat" events, so a faster reload
    /// moves it in step. The Writ's hands never move on the gun (the creator, 2026-10-14: "make sure our hands don't move"):
    /// its reload has no magazine, the quill does the work, and the gun itself tilts up.</item>
    /// <item>Each forearm stays on its hand's wrist and is aimed every frame at a shoulder below and behind the screen
    /// (camera space), so whatever the hand does the arm runs out of the bottom of the view (fix round 1: a forearm baked to
    /// the hand jutted sideways in the reload and hid the Breacher's pump hand).</item>
    /// </list>
    /// Added to the player by <see cref="SkylineWritFx.AttachAll"/>; leaves the viewmodel's primitive arms alone. The hand
    /// poses are build_arms.py's POSES (tools/skyline/guns/writ/model/): keep the two in step.
    /// </summary>
    [DefaultExecutionOrder(160)]
    public sealed class SkylineRobotArms : MonoBehaviour
    {
        struct Pose
        {
            public Vector3 origin, forward, up;
            public Pose(Vector3 o, Vector3 f, Vector3 u) { origin = o; forward = f; up = u; }
            public Quaternion Rotation => Quaternion.LookRotation(forward, up);
        }

        // gun frame (x right, y up, z forward); the hand's own frame: y along what it holds, z where its fingers wrap
        static readonly Pose HandLWrit = new Pose(new Vector3(-0.004f, -0.0802f, -0.061f), new Vector3(-0.2326f, -0.2756f, 0.9327f), new Vector3(0f, 0.9613f, 0.2756f));
        static readonly Pose HandLBreacher = new Pose(new Vector3(-0.004f, -0.012f, 0.214f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, -1f));
        // the Breacher's left hand under its cell while it carries it (the cell hangs at 0, -0.055, 0.07)
        static readonly Pose HandLBreacherCell = new Pose(new Vector3(-0.004f, -0.03f, 0.07f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, -1f));
        // the shoulders, in the viewmodel camera's frame: below and a little behind the screen's bottom corners
        public static readonly Vector3 ShoulderR = new Vector3(0.30f, -0.42f, 0f), ShoulderL = new Vector3(-0.22f, -0.44f, 0.02f);
        const float Reach = 0.07f;   // of the clip: how early the hand starts for the magazine and how late it lets go

        public SkylinePlayer player;

        sealed class Limb
        {
            public Transform hand, forearm, elbow;
            public Vector3 wristInHand;           // the forearm's origin in the hand's frame
            public Vector3 restDir;               // the forearm's direction (wrist to elbow) in the hand's frame
            public Quaternion forearmInHand;      // the forearm's turn in the hand's frame
        }

        sealed class Arm
        {
            public Transform gunRoot;
            public Limb right, left;
            public Vector3 leftRestPos;          // the left hand's place and turn in the gun's frame as imported (the Writ's hold)
            public Quaternion leftRestRot;
            public bool breacher;
        }

        readonly Arm[] _arms = new Arm[2];
        Transform _cam;
        bool _resolved;

        void Awake()
        {
            if (player == null) player = GetComponent<SkylinePlayer>();
        }

        static Limb MakeLimb(Transform root, string side)
        {
            var hand = SkylineBotBodies.Find(root, "Hand" + side);
            if (hand == null || hand.parent == root) return null;   // missing, or the viewmodel's primitive arms: not ours
            var l = new Limb { hand = hand, forearm = SkylineBotBodies.Find(root, "Forearm" + side), elbow = SkylineBotBodies.Find(root, "Elbow" + side) };
            if (l.forearm != null)
            {
                l.wristInHand = hand.InverseTransformPoint(l.forearm.position);
                l.forearmInHand = Quaternion.Inverse(hand.rotation) * l.forearm.rotation;
                if (l.elbow != null) l.restDir = hand.InverseTransformDirection(l.elbow.position - l.forearm.position).normalized;
            }
            return l;
        }

        bool Resolve()
        {
            if (_resolved) return true;
            var vm = player != null ? player.viewmodel : null;
            if (vm == null || vm.OverlayCamera == null) return false;
            _cam = vm.OverlayCamera.transform;
            bool any = false;
            string[] ids = { SkylineGuns.WritId, SkylineGuns.BreacherId };
            for (int i = 0; i < ids.Length; i++)
            {
                var root = _cam.Find("Gun " + ids[i]);
                if (root == null) continue;
                any = true;
                var right = MakeLimb(root, "R");
                var left = MakeLimb(root, "L");
                if (right == null && left == null) continue;
                var a = new Arm { gunRoot = root, right = right, left = left, breacher = ids[i] == SkylineGuns.BreacherId };
                if (left != null)
                {
                    a.leftRestPos = root.InverseTransformPoint(left.hand.position);
                    a.leftRestRot = Quaternion.Inverse(root.rotation) * left.hand.rotation;
                }
                _arms[i] = a;
            }
            _resolved = any;
            return any;
        }

        void LateUpdate()
        {
            if (!Resolve() || player.Loadout == null) return;
            for (int i = 0; i < _arms.Length; i++)
            {
                var a = _arms[i];
                if (a == null || !a.gunRoot.gameObject.activeInHierarchy) continue;
                if (a.left != null)
                {
                    SkylineGun gun = null;
                    for (int s = 0; s < 2; s++)
                        if (player.Loadout[s] != null && player.Loadout[s].Def.id == (a.breacher ? SkylineGuns.BreacherId : SkylineGuns.WritId))
                            gun = player.Loadout[s];
                    Pose hold = a.breacher ? HandLBreacher : HandLWrit;
                    Vector3 pos = hold.origin;
                    Quaternion turn = hold.Rotation;
                    float w = gun != null && a.breacher ? ReloadWeight(gun.Handling) : 0f;   // the Writ's hands stay where they are
                    if (w > 0f)
                    {
                        var mo = gun.Handling.Pose.magOffset;
                        var mag = new Vector3(mo.X, mo.Y, mo.Z);
                        Pose carry = a.breacher ? HandLBreacherCell : HandLWrit;
                        pos = Vector3.Lerp(pos, carry.origin + mag, w);
                        turn = Quaternion.Slerp(turn, carry.Rotation, w);
                    }
                    // the imported hand is the Writ's hold: turn it by (target * writ^-1) about its own origin
                    Quaternion delta = turn * Quaternion.Inverse(HandLWrit.Rotation);
                    a.left.hand.SetPositionAndRotation(a.gunRoot.TransformPoint(pos + (a.leftRestPos - HandLWrit.origin)),
                        a.gunRoot.rotation * (delta * a.leftRestRot));
                    Aim(a.left, ShoulderL);
                }
                if (a.right != null) Aim(a.right, ShoulderR);
            }
        }

        /// <summary>Puts the forearm on its hand's wrist and turns it about the wrist so it points at the shoulder.</summary>
        void Aim(Limb l, Vector3 shoulderInCamera)
        {
            if (l.forearm == null) return;
            Vector3 wrist = l.hand.TransformPoint(l.wristInHand);
            Quaternion rest = l.hand.rotation * l.forearmInHand;
            if (l.elbow == null || l.restDir.sqrMagnitude < 0.5f) { l.forearm.SetPositionAndRotation(wrist, rest); return; }
            Vector3 from = l.hand.TransformDirection(l.restDir);
            Vector3 to = _cam.TransformPoint(shoulderInCamera) - wrist;
            if (to.sqrMagnitude < 1e-6f) to = from;
            l.forearm.SetPositionAndRotation(wrist, Quaternion.FromToRotation(from, to) * rest);
        }

        /// <summary>0 holding, 1 carrying the Breacher's cell: up from (out - Reach) to the cell-out event, held to the seat, down
        /// by (seat + Reach).</summary>
        static float ReloadWeight(HandlingPlayer h)
        {
            var c = h != null ? h.Clip : null;
            if (c == null || !h.Playing || (c.kind != HandlingClipKind.ReloadPartial && c.kind != HandlingClipKind.ReloadEmpty)) return 0f;
            float outAt = -1f, seatAt = -1f;
            for (int i = 0; i < c.events.Length; i++)
            {
                var id = c.events[i].id;
                if (outAt < 0f && id == "cell_out") outAt = c.events[i].at;
                if (seatAt < 0f && id == "seat") seatAt = c.events[i].at;
            }
            if (outAt < 0f || seatAt < outAt) return 0f;
            float t = h.Progress01;
            float w;
            if (t < outAt - Reach) w = 0f;
            else if (t < outAt) w = (t - (outAt - Reach)) / Reach;
            else if (t <= seatAt) w = 1f;
            else w = 1f - Mathf.Clamp01((t - seatAt) / Reach);
            return w * w * (3f - 2f * w);
        }
    }
}
