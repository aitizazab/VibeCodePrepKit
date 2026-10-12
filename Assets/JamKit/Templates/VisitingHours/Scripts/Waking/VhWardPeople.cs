using System.Collections.Generic;
using JamKit.VisitingHours.Characters;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The people on the ward and in the booth (docs/redesign/day.md, "People"). Each has an id; <see cref="Place"/> puts
    /// the People agent's 3D cast there (docs/redesign/people.md: <see cref="VhCharacter.Spawn"/> of Mags, Rafael, the
    /// LeBaget representative and EliasBed, rigged and animated, with talking faces), posed by animation state (standing:
    /// "idle"; sitting: "sit"; Elias: "lie"). The people with no model yet (Mrs. Okafor, Mr. and Mrs. Brandt, the man
    /// waiting in the family room) are stand-ins in their clothes (PBR fabric and skin), standing, sitting or in a bed.
    /// </summary>
    public sealed class VhWardPeople
    {
        public const string PrefabFolder = "VhPeople/";
        public const string Rafael = "rafael", Mags = "mags", June = "june_brandt", Okafor = "okafor", Brandt = "brandt",
            Visitor = "lebaget_visitor", WaitingMan = "waiting_man", Elias = "elias";

        public enum Pose { Standing, Sitting, InBed, LyingInBed }

        /// <summary>The People agent's prefab for a ward id (Resources/People/&lt;name&gt;), or null: no model, a stand-in.</summary>
        public static string ModelFor(string id)
        {
            switch (id)
            {
                case Rafael: return "Rafael";
                case Mags: return "Mags";
                case Visitor: return "Representative";
                case Elias: return "EliasBed";
                default: return null;
            }
        }

        /// <summary>The animation state a pose plays (every prefab's controller has the same names; EliasBed defaults to lie).</summary>
        public static string StateFor(Pose pose)
        {
            switch (pose)
            {
                case Pose.Sitting: return "sit";
                case Pose.InBed:
                case Pose.LyingInBed: return "lie";
                default: return "idle";
            }
        }

        /// <summary>The speakers in the ink who are one of the 3D cast (so their mouths move when they speak).</summary>
        public static string ModelForSpeaker(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            if (speaker == "Mags") return "Mags";
            if (speaker == "Rafael") return "Rafael";
            if (speaker.Contains("LeBaget") || speaker.Contains("Vasseur")) return "Representative";
            return null;
        }

        struct Look
        {
            public float height;
            public Color top, legs, hair, skin, accent;
            public string topSurface;
            public bool bun, glasses, tie, wrap, bald, lanyard;
        }

        static Look LookOf(string id)
        {
            var scrubs = new Color(0.24f, 0.43f, 0.45f);
            var gown = new Color(0.66f, 0.74f, 0.80f);
            switch (id)
            {
                case Rafael: return new Look { height = 1.77f, top = scrubs, legs = scrubs * 0.88f, hair = new Color(0.12f, 0.09f, 0.08f), skin = new Color(0.62f, 0.45f, 0.36f), topSurface = "fabric", lanyard = true };
                case Mags: return new Look { height = 1.64f, top = new Color(0.54f, 0.44f, 0.32f), legs = new Color(0.25f, 0.24f, 0.24f), hair = new Color(0.72f, 0.70f, 0.68f), skin = new Color(0.84f, 0.70f, 0.62f), topSurface = "fabric", bun = true, glasses = true };
                case June: return new Look { height = 1.6f, top = new Color(0.58f, 0.48f, 0.62f), legs = new Color(0.36f, 0.36f, 0.4f), hair = new Color(0.86f, 0.85f, 0.84f), skin = new Color(0.86f, 0.72f, 0.66f), topSurface = "fabric", glasses = true };
                case Okafor: return new Look { height = 1.62f, top = gown, legs = gown, hair = new Color(0.2f, 0.18f, 0.17f), skin = new Color(0.36f, 0.24f, 0.18f), topSurface = "linen", wrap = true, accent = new Color(0.72f, 0.32f, 0.2f) };
                case Brandt: return new Look { height = 1.75f, top = gown, legs = gown, hair = new Color(0.86f, 0.86f, 0.84f), skin = new Color(0.86f, 0.68f, 0.6f), topSurface = "linen", bald = true };
                // LeBaget's representative (a woman since 2026-10-13): the aubergine blazer, charcoal trousers, dark brown hair.
                case Visitor: return new Look { height = 1.72f, top = new Color(0.36f, 0.2f, 0.3f), legs = new Color(0.22f, 0.22f, 0.24f), hair = new Color(0.22f, 0.15f, 0.11f), skin = new Color(0.86f, 0.72f, 0.64f), topSurface = "fabric", bun = true, glasses = true };
                case WaitingMan: return new Look { height = 1.72f, top = new Color(0.42f, 0.32f, 0.22f), legs = new Color(0.3f, 0.32f, 0.38f), hair = new Color(0.55f, 0.53f, 0.5f), skin = new Color(0.78f, 0.6f, 0.5f), topSurface = "fabric", bald = true };
                default: return new Look { height = 1.72f, top = scrubs, legs = scrubs, hair = new Color(0.2f, 0.16f, 0.13f), skin = VhWakingStyle.Skin, topSurface = "fabric" };
            }
        }

        readonly Transform _root;
        readonly VhMeshKit _meshes;
        readonly VhWakingStyle _style;
        readonly VhWardLibrary _lib;
        readonly List<Material> _mats;
        readonly List<VhCharacter> _cast;

        public VhWardPeople(Transform root, VhMeshKit meshes, VhWakingStyle style, VhWardLibrary lib, List<Material> mats, List<VhCharacter> cast = null)
        {
            _root = root;
            _meshes = meshes;
            _style = style;
            _lib = lib;
            _mats = mats;
            _cast = cast;
        }

        Material Mat(Color c, string surface)
        {
            if (_lib != null)
            {
                var m = _lib.Colour(surface, c, _mats);
                if (m != null) return m;
            }
            return _style.Mat(c);
        }

        void Bit(Transform parent, string name, Mesh mesh, Color c, string surface, Vector3 at, Quaternion rot) =>
            VhMeshKit.Show(parent, name, mesh, Mat(c, surface), at, rot);

        /// <summary>
        /// Puts person <paramref name="id"/> at <paramref name="at"/> (their feet; for a seat, the seat's centre on the floor;
        /// in a bed, the bed's centre on the floor), facing <paramref name="yaw"/>. The People agent's model when it's in the
        /// build, else the stand-in. Returns the person's root (it carries a collider).
        /// </summary>
        public Transform Place(string id, Vector3 at, float yaw, Pose pose)
        {
            var c = Cast(id, at, yaw, pose);
            if (c != null) return c.transform;
            return StandIn(id, at, yaw, pose);
        }

        /// <summary>
        /// The 3D person for <paramref name="id"/> (People's prefab), placed and posed, or null when the id has no model or the
        /// prefab isn't in the build (then <see cref="Place"/> builds the stand-in). The model is listed in the place's cast
        /// (the game turns their heads to her and moves their mouths when they speak). A collider stands where they stand.
        /// </summary>
        public VhCharacter Cast(string id, Vector3 at, float yaw, Pose pose)
        {
            string model = ModelFor(id);
            if (model == null || Resources.Load<GameObject>(VhCharacter.ResourceFolder + model) == null) return null;
            var c = VhCharacter.Spawn(model, _root.TransformPoint(at), _root.rotation * Quaternion.Euler(0f, yaw, 0f), _root);
            if (c == null) return null;
            c.gameObject.name = model + " (" + id + ")";
            c.Play(StateFor(pose), 0f);
            if (pose == Pose.Standing || pose == Pose.Sitting)
            {
                var col = c.gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, pose == Pose.Sitting ? 0.6f : 0.85f, pose == Pose.Sitting ? 0.15f : 0f);
                col.height = pose == Pose.Sitting ? 1.2f : 1.7f;
                col.radius = 0.25f;
            }
            // The animator off-screen: no skinning (the ward has a dozen rooms; most people are out of view).
            foreach (var smr in c.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = false;
            if (c.animator != null) c.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            _cast?.Add(c);
            return c;
        }

        Transform StandIn(string id, Vector3 at, float yaw, Pose pose)
        {
            var look = LookOf(id);
            var p = new GameObject(id + " (stand-in)").transform;
            p.SetParent(_root, false);
            p.localPosition = at;
            p.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float s = look.height / 1.7f;
            var m = _meshes;
            var body = new GameObject("Body").transform;
            body.SetParent(p, false);

            // Where the body's parts go for the pose: the hips, the torso's tilt (lying back), the legs.
            Vector3 hips;
            float lean;
            switch (pose)
            {
                case Pose.Sitting: hips = new Vector3(0f, 0.52f, -0.04f); lean = -6f; break;
                case Pose.InBed: hips = new Vector3(0f, 0.72f, -0.15f); lean = -42f; break;
                case Pose.LyingInBed: hips = new Vector3(0f, 0.72f, 0.05f); lean = -86f; break;
                default: hips = new Vector3(0f, 0.86f * s, 0f); lean = -2f; break;
            }
            var torsoRot = Quaternion.Euler(lean, 0f, 0f);
            Vector3 Up(float h) => hips + torsoRot * (Vector3.up * h * s);

            // Legs: standing straight; sitting with thighs forward and shins down; in bed under the covers (not drawn).
            if (pose == Pose.Standing)
            {
                foreach (float x in new[] { -0.095f, 0.095f })
                {
                    Bit(body, "Shoe", m.RoundedBox(new Vector3(0.1f, 0.07f, 0.25f) * s, 0.03f * s, 3), new Color(0.14f, 0.14f, 0.15f), "plastic_dark", new Vector3(x * s, 0.035f * s, 0.04f * s), Quaternion.identity);
                    Bit(body, "Leg", m.Cylinder(0.068f * s, 0.058f * s, 0.8f * s, 12), look.legs, look.topSurface, new Vector3(x * s, 0.06f * s, 0f), Quaternion.identity);
                }
            }
            else if (pose == Pose.Sitting)
            {
                foreach (float x in new[] { -0.1f, 0.1f })
                {
                    Bit(body, "Thigh", m.Cylinder(0.075f * s, 0.065f * s, 0.44f * s, 12), look.legs, look.topSurface, new Vector3(x * s, 0.5f, -0.02f), Quaternion.Euler(90f, 0f, 0f));
                    Bit(body, "Shin", m.Cylinder(0.06f * s, 0.055f * s, 0.46f, 12), look.legs, look.topSurface, new Vector3(x * s, 0.04f, 0.42f * s), Quaternion.identity);
                    Bit(body, "Shoe", m.RoundedBox(new Vector3(0.1f, 0.07f, 0.25f) * s, 0.03f * s, 3), new Color(0.14f, 0.14f, 0.15f), "plastic_dark", new Vector3(x * s, 0.035f, 0.48f * s), Quaternion.identity);
                }
            }
            Bit(body, "Hips", m.RoundedBox(new Vector3(0.36f, 0.2f, 0.24f) * s, 0.08f * s, 4), pose == Pose.Standing || pose == Pose.Sitting ? look.legs : look.top, look.topSurface, hips, torsoRot);
            Bit(body, "Torso", m.RoundedBox(new Vector3(0.40f, 0.54f, 0.25f) * s, 0.11f * s, 5, 0.01f * s), look.top, look.topSurface, Up(0.32f), torsoRot);
            if (look.tie)
            {
                Bit(body, "Shirt", m.RoundedBox(new Vector3(0.12f, 0.2f, 0.02f) * s, 0.01f * s, 2), new Color(0.88f, 0.89f, 0.9f), "linen", Up(0.46f) + torsoRot * new Vector3(0f, 0f, 0.122f * s), torsoRot);
                Bit(body, "Tie", m.RoundedBox(new Vector3(0.045f, 0.3f, 0.012f) * s, 0.006f * s, 2), look.accent, "fabric", Up(0.4f) + torsoRot * new Vector3(0f, 0f, 0.132f * s), torsoRot);
            }
            if (look.lanyard)
            {
                Bit(body, "Lanyard", m.Tube(new List<Vector3> { new Vector3(-0.08f, 0.2f, 0.12f) * s, new Vector3(0f, -0.04f, 0.14f) * s, new Vector3(0.08f, 0.2f, 0.12f) * s }, 0.006f * s, 5), VhWakingStyle.Lanyard, "fabric", Up(0.36f), torsoRot);
                Bit(body, "Badge", m.RoundedBox(new Vector3(0.055f, 0.08f, 0.006f) * s, 0.004f * s, 2), new Color(0.92f, 0.92f, 0.9f), "plastic", Up(0.28f) + torsoRot * new Vector3(0f, 0f, 0.142f * s), torsoRot);
            }

            // Arms: hanging (standing), forearms on the lap (sitting), along the covers (in bed).
            foreach (float side in new[] { -1f, 1f })
            {
                var shoulder = Up(0.54f) + torsoRot * new Vector3(side * 0.235f * s, 0f, 0f);
                if (pose == Pose.Standing)
                {
                    Bit(body, "Arm", m.Cylinder(0.05f * s, 0.055f * s, 0.56f * s, 10), look.top, look.topSurface, shoulder - Vector3.up * 0.56f * s, Quaternion.Euler(0f, 0f, side * 4f));
                    Bit(body, "Hand", m.RoundedBox(new Vector3(0.07f, 0.11f, 0.045f) * s, 0.022f * s, 3), look.skin, "skin", shoulder - Vector3.up * 0.63f * s + Vector3.right * side * 0.01f, Quaternion.identity);
                }
                else
                {
                    var elbow = shoulder + torsoRot * (Vector3.down * 0.27f * s);
                    var wrist = pose == Pose.Sitting ? new Vector3(side * 0.08f * s, 0.62f, 0.28f) : elbow + new Vector3(side * 0.04f, 0f, 0.3f);
                    Limb(body, shoulder, elbow, 0.052f * s, look.top, look.topSurface);
                    Limb(body, elbow, wrist, 0.042f * s, pose == Pose.Standing || look.topSurface == "linen" ? look.skin : look.top, pose == Pose.Standing || look.topSurface == "linen" ? "skin" : look.topSurface);
                    Bit(body, "Hand", m.RoundedBox(new Vector3(0.075f, 0.035f, 0.1f) * s, 0.016f * s, 3), look.skin, "skin", wrist + (wrist - elbow).normalized * 0.05f * s, Quaternion.LookRotation(wrist - elbow, Vector3.up));
                }
            }

            // The head.
            var neck = Up(0.62f);
            var headRot = pose == Pose.LyingInBed ? Quaternion.Euler(-80f, 0f, 0f) : Quaternion.Euler(pose == Pose.InBed ? -18f : 4f, 0f, 0f);
            Bit(body, "Neck", m.Cylinder(0.05f * s, 0.05f * s, 0.1f * s, 10), look.skin, "skin", neck, torsoRot);
            var head = neck + torsoRot * (Vector3.up * 0.16f * s);
            Bit(body, "Head", m.RoundedBox(new Vector3(0.175f, 0.23f, 0.21f) * s, 0.085f * s, 6, 0.012f * s), look.skin, "skin", head, headRot);
            Bit(body, "Nose", m.RoundedBox(new Vector3(0.03f, 0.05f, 0.04f) * s, 0.012f * s, 2), look.skin * 0.97f, "skin", head + headRot * new Vector3(0f, -0.01f * s, 0.11f * s), headRot);
            Bit(body, "Ear", m.RoundedBox(new Vector3(0.02f, 0.06f, 0.04f) * s, 0.008f * s, 2), look.skin * 0.96f, "skin", head + headRot * new Vector3(-0.092f * s, 0f, 0f), headRot);
            Bit(body, "Ear", m.RoundedBox(new Vector3(0.02f, 0.06f, 0.04f) * s, 0.008f * s, 2), look.skin * 0.96f, "skin", head + headRot * new Vector3(0.092f * s, 0f, 0f), headRot);
            if (look.wrap)
                Bit(body, "Head wrap", m.RoundedBox(new Vector3(0.2f, 0.16f, 0.23f) * s, 0.07f * s, 5, 0.02f * s), look.accent, "fabric", head + headRot * new Vector3(0f, 0.09f * s, -0.01f * s), headRot);
            else if (look.bald)
                Bit(body, "Hair (fringe)", m.RoundedBox(new Vector3(0.19f, 0.08f, 0.16f) * s, 0.035f * s, 3), look.hair, "carpet", head + headRot * new Vector3(0f, 0.0f, -0.05f * s), headRot);
            else
            {
                Bit(body, "Hair", m.RoundedBox(new Vector3(0.195f, 0.12f, 0.225f) * s, 0.06f * s, 5, 0.01f * s), look.hair, "carpet", head + headRot * new Vector3(0f, 0.085f * s, -0.008f * s), headRot);
                Bit(body, "Hair (back)", m.RoundedBox(new Vector3(0.19f, 0.17f, 0.06f) * s, 0.03f * s, 3), look.hair, "carpet", head + headRot * new Vector3(0f, 0f, -0.095f * s), headRot);
            }
            if (look.bun) Bit(body, "Bun", m.RoundedBox(new Vector3(0.09f, 0.09f, 0.09f) * s, 0.04f * s, 4), look.hair, "carpet", head + headRot * new Vector3(0f, 0.06f * s, -0.14f * s), headRot);
            if (look.glasses)
                foreach (float x in new[] { -0.034f, 0.034f })
                    Bit(body, "Glasses", m.RoundedBox(new Vector3(0.05f, 0.032f, 0.006f) * s, 0.008f * s, 2), new Color(0.45f, 0.4f, 0.36f), "plastic", head + headRot * new Vector3(x * s, 0.015f * s, 0.112f * s), headRot);

            var col = p.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.85f * s, 0f);
            col.height = 1.7f * s;
            col.radius = 0.25f * s;
            if (pose == Pose.InBed || pose == Pose.LyingInBed) col.enabled = false;
            return p;
        }

        void Limb(Transform parent, Vector3 from, Vector3 to, float radius, Color c, string surface)
        {
            float len = Vector3.Distance(from, to);
            if (len < 0.01f) return;
            var rot = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
            Bit(parent, "Limb", _meshes.Cylinder(radius * 1.05f, radius, len, 10), c, surface, from, rot);
        }
    }
}
