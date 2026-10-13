using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The robot arms' per-gun hold (Task 18). SkylineViewmodel instantiates Resources/SkylineModels/arms/arms (HandR,
    /// HandL) under each gun's root as it is; the FBX holds the Writ. The right hand holds both guns unchanged (the Writ's
    /// grip column sits on the Breacher's grip axis). The left hand cups the Writ's grip under the right hand; for the
    /// Breacher this moves it onto the pump. During a reload the left hand leaves its hold, takes the magazine (the Writ's
    /// stone drum, the Breacher's cell) as it drops, brings the new one up and lets go after the seat: it follows the clip's
    /// magOffset between the "mag_out"/"cell_out" and "seat" events, so a faster reload moves it in step.
    /// Added to the player by <see cref="SkylineWritFx.AttachAll"/>. Leaves the viewmodel's primitive arms alone.
    /// The poses below are build_arms.py's POSES (tools/skyline/guns/writ/model/): keep the two in step.
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
        static readonly Pose HandLBreacher = new Pose(new Vector3(0f, -0.016f, 0.218f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f));
        // the Breacher's left hand under its cell while it carries it (the cell hangs at 0, -0.055, 0.07)
        static readonly Pose HandLBreacherCell = new Pose(new Vector3(0f, -0.072f, 0.07f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f));
        const float Reach = 0.07f;   // of the clip: how early the hand starts for the magazine and how late it lets go

        public SkylinePlayer player;

        sealed class Arm
        {
            public Transform gunRoot, handL;
            public Vector3 restPos;          // the hand's place and turn in the gun's frame as imported (the Writ's hold)
            public Quaternion restRot;
            public bool breacher;
        }

        readonly Arm[] _arms = new Arm[2];
        bool _resolved;

        void Awake()
        {
            if (player == null) player = GetComponent<SkylinePlayer>();
        }

        bool Resolve()
        {
            if (_resolved) return true;
            var vm = player != null ? player.viewmodel : null;
            if (vm == null || vm.OverlayCamera == null) return false;
            var cam = vm.OverlayCamera.transform;
            bool any = false;
            string[] ids = { SkylineGuns.WritId, SkylineGuns.BreacherId };
            for (int i = 0; i < ids.Length; i++)
            {
                var root = cam.Find("Gun " + ids[i]);
                if (root == null) continue;
                any = true;
                var hand = SkylineBotBodies.Find(root, "HandL");
                if (hand == null || hand.parent == root) continue;   // the viewmodel's primitive arms: not ours to move
                _arms[i] = new Arm
                {
                    gunRoot = root, handL = hand, breacher = ids[i] == SkylineGuns.BreacherId,
                    restPos = root.InverseTransformPoint(hand.position),
                    restRot = Quaternion.Inverse(root.rotation) * hand.rotation,
                };
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
                if (a == null || a.handL == null || !a.gunRoot.gameObject.activeInHierarchy) continue;
                SkylineGun gun = null;
                for (int s = 0; s < 2; s++)
                    if (player.Loadout[s] != null && player.Loadout[s].Def.id == (a.breacher ? SkylineGuns.BreacherId : SkylineGuns.WritId))
                        gun = player.Loadout[s];
                Pose hold = a.breacher ? HandLBreacher : HandLWrit;
                Vector3 pos = hold.origin;
                Quaternion turn = hold.Rotation;
                float w = gun != null ? ReloadWeight(gun.Handling) : 0f;
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
                a.handL.SetPositionAndRotation(a.gunRoot.TransformPoint(pos + (a.restPos - HandLWrit.origin)),
                    a.gunRoot.rotation * (delta * a.restRot));
            }
        }

        /// <summary>0 holding, 1 carrying the magazine: up from (out - Reach) to the mag-out event, held to the seat, down by
        /// (seat + Reach).</summary>
        static float ReloadWeight(HandlingPlayer h)
        {
            var c = h != null ? h.Clip : null;
            if (c == null || !h.Playing || (c.kind != HandlingClipKind.ReloadPartial && c.kind != HandlingClipKind.ReloadEmpty)) return 0f;
            float outAt = -1f, seatAt = -1f;
            for (int i = 0; i < c.events.Length; i++)
            {
                var id = c.events[i].id;
                if (outAt < 0f && (id == "mag_out" || id == "cell_out")) outAt = c.events[i].at;
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
