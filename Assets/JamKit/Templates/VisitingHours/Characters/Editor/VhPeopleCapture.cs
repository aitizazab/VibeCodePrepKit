using System;
using System.Collections.Generic;
using System.IO;
using JamKit.DreamEscape;
using JamKit.Look;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JamKit.VisitingHours.Characters.EditorTools
{
    /// <summary>
    /// The judge's captures of the people (docs/redesign/people.md "Captures"), 1920x1080 PNGs through URP with the Look
    /// agent's own rigs (VhLook.ApplyGoldenHour / ApplyHospitalDay: the HDRI sky, the sun and its shadows, the post), to
    /// out/vh-people/&lt;time&gt;/:
    ///   &lt;light&gt;-lineup.png / -lineup-talk.png   the standing cast side by side, facing the camera, in idle / in talk
    ///   &lt;light&gt;-turn-&lt;Name&gt;.png       eight angles, 45 degrees apart (the ward's walls hidden for it)
    ///   &lt;light&gt;-face-&lt;Name&gt;.png       conversation-distance close-ups of the whole head: neutral, talking (half, open), a
    ///                                blink, every expression, then happy and talking
    ///   &lt;light&gt;-seq-&lt;Name&gt;.png        one second of talking (24 frames: a blink at frame 10, warm then worried), and the
    ///                                frames themselves in seq-&lt;light&gt;-&lt;Name&gt;/ (JPG) to play as motion
    ///   &lt;light&gt;-pair-&lt;A&gt;-&lt;B&gt;*.png    two people side by side, both facing the camera (A talks, B listens)
    ///   hospital-bed-EliasBed*.png      Elias asleep in a ward bed (wide, close)
    ///   paper-*.png                     Walt and Lena as paper cut-outs beside the 3D Elias, same light
    ///   watch-*.png                     Nadia's right forearm (textured) in first person with Lena's star watch, close-ups,
    ///                                and the watch seen along the arm (how it sits on the skin)
    /// Posing: each clip is sampled with AnimationMode (the Animation window's own sampler, which handles Humanoid clips);
    /// if a character still holds its bind pose afterwards a playable graph is tried, and the log says which worked
    /// ("JAMKIT_VH_PEOPLE pose ..."). Every standing shot turns the person so the FACE (from the face decal) looks where
    /// the shot wants, whatever the clip's own facing.
    /// Batch mode needs graphics (no -nographics): tools/tripo/vh-people-capture.ps1.
    /// </summary>
    public static class VhPeopleCapture
    {
        public const int Width = 1920, Height = 1080;
        public const string NadiaModel = VhPeopleBuild.ModelsDir + "Nadia/Nadia.fbx";
        const int SeqFrames = 24;
        const float SeqFps = 24f;

        [MenuItem("JamKit/Visiting Hours/People/Capture the Cast")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.RevealInFinder(Capture());
        }

        /// <summary>-executeMethod JamKit.VisitingHours.Characters.EditorTools.VhPeopleCapture.CaptureFromCommandLine</summary>
        public static void CaptureFromCommandLine()
        {
            int code = 0;
            try { Debug.Log("JAMKIT_VH_PEOPLE_CAPTURE_OK " + Capture()); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static string Capture()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "out", "vh-people", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            var prefabs = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { VhPeopleBuild.PeopleDir.TrimEnd('/') }))
                prefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            if (prefabs.Count == 0) throw new Exception("no prefabs in " + VhPeopleBuild.PeopleDir + ": run Build the Cast first");
            prefabs.Sort((a, b) => Order(a.name).CompareTo(Order(b.name)));
            try
            {
                Pass("golden", dir, prefabs);
                Pass("hospital", dir, prefabs);
                Paper(dir, Find(prefabs, "Elias"));
                Watch(dir);
            }
            finally
            {
                VhLookRig.previewShadowsInEditor = false;
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            }
            return dir;
        }

        static int Order(string name)
        {
            for (int i = 0; i < VhPeopleBuild.Cast.Length; i++) if (VhPeopleBuild.Cast[i].name == name) return i;
            return 100;
        }

        static GameObject Find(List<GameObject> prefabs, string name)
        {
            foreach (var p in prefabs) if (p != null && p.name == name) return p;
            return null;
        }

        // ---------------------------------------------------------------------------------------------------------
        // The stage
        // ---------------------------------------------------------------------------------------------------------

        sealed class Posed { public AnimationClip clip; public float time; }

        sealed class Stage
        {
            public Scene scene;
            public Camera cam;
            public VhLookRig rig;
            public RenderTexture rt;
            public readonly List<Renderer> walls = new List<Renderer>();
            public readonly Dictionary<VhCharacter, Posed> poses = new Dictionary<VhCharacter, Posed>();
            public readonly Dictionary<VhCharacter, Quaternion> bind = new Dictionary<VhCharacter, Quaternion>();
            public readonly List<PlayableGraph> graphs = new List<PlayableGraph>();
            public readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();

            public void Dispose()
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                foreach (var g in graphs) if (g.IsValid()) g.Destroy();
                foreach (var a in assets) if (a != null) UnityEngine.Object.DestroyImmediate(a);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            }
        }

        /// <summary>A fresh scene with the Look agent's rig: "golden" (the hill's late-summer evening, a patio, a fence,
        /// trees) or "hospital" (the ward by day: a room with cool overhead panels).</summary>
        static Stage BuildStage(string light)
        {
            var s = new Stage { scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single) };
            bool golden = light != "hospital";
            s.rig = golden ? VhLook.ApplyGoldenHour(s.scene, new VhLook.Options { sunAzimuth = 235f, sunElevation = 11f })
                           : VhLook.ApplyHospitalDay(s.scene);
            var set = new GameObject("Set").transform;
            if (golden)
            {
                Block(set, "Patio", new Vector3(0f, -0.1f, 0f), new Vector3(24f, 0.2f, 24f), VhLook.Material("Concrete_Sidewalk"));
                Block(set, "Kerb", new Vector3(0f, -0.12f, 30f), new Vector3(120f, 0.2f, 36f), VhLook.Material("Concrete_Kerb"));
                Block(set, "Fence", new Vector3(0f, 0.9f, 9f), new Vector3(20f, 1.8f, 0.08f), VhLook.Material("Fence_Planks"));
                VhLookTrees.Place(set, new Vector3(-7f, 0f, 14f), 3, 9f);
                VhLookTrees.Place(set, new Vector3(6f, 0f, 17f), 7, 11f);
                VhLookTrees.Place(set, new Vector3(14f, 0f, 26f), 11, 10f);
            }
            else
            {
                Block(set, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(12f, 0.2f, 12f), VhLook.Material("Hospital_Terrazzo"));
                var wall = Tint(s, VhLook.Material("Plaster_Interior"), new Color(0.78f, 0.88f, 0.82f));
                s.walls.Add(Block(set, "Back wall", new Vector3(0f, 1.45f, 3.2f), new Vector3(12f, 2.9f, 0.15f), wall).GetComponent<Renderer>());
                s.walls.Add(Block(set, "Left wall", new Vector3(-5f, 1.45f, 0f), new Vector3(0.15f, 2.9f, 12f), wall).GetComponent<Renderer>());
                s.walls.Add(Block(set, "Right wall", new Vector3(5f, 1.45f, 0f), new Vector3(0.15f, 2.9f, 12f), wall).GetComponent<Renderer>());
                s.walls.Add(Block(set, "Ceiling", new Vector3(0f, 2.95f, 0f), new Vector3(12f, 0.1f, 12f), VhLook.Material("Ceiling_Tile")).GetComponent<Renderer>());
                s.walls.Add(Block(set, "Dado rail", new Vector3(0f, 0.95f, 3.11f), new Vector3(12f, 0.06f, 0.04f),
                    Tint(s, VhLook.Material("Plaster_Painted"), new Color(0.45f, 0.62f, 0.6f))).GetComponent<Renderer>());
                var glow = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                glow.SetColor("_BaseColor", new Color(1.6f, 1.65f, 1.7f));
                s.assets.Add(glow);
                foreach (float x in new[] { -2.4f, 0f, 2.4f })
                {
                    var panel = Block(set, "Panel", new Vector3(x, 2.89f, 0.6f), new Vector3(0.6f, 0.02f, 1.2f), glow);
                    panel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    s.walls.Add(panel.GetComponent<Renderer>());
                    var l = new GameObject("Panel light").AddComponent<Light>();
                    l.transform.SetParent(set, false);
                    l.transform.position = new Vector3(x, 2.75f, 0.6f);
                    l.type = LightType.Point;
                    l.color = new Color(0.93f, 0.97f, 1f);
                    l.intensity = 2.2f;
                    l.range = 7f;
                    l.shadows = x == 0f ? LightShadows.Soft : LightShadows.None;
                }
            }
            var camGo = new GameObject("Capture camera");
            s.cam = camGo.AddComponent<Camera>();
            VhLook.SetupCamera(s.cam);
            s.cam.nearClipPlane = 0.02f;
            VhLookRig.previewShadowsInEditor = true;
            s.rig.Apply();
            s.rig.RefreshShadows();
            DynamicGI.UpdateEnvironment();
            s.rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            return s;
        }

        static void ShowWalls(Stage s, bool on) { foreach (var r in s.walls) if (r != null) r.enabled = on; }

        static Material Tint(Stage s, Material m, Color c)
        {
            if (m == null) return null;
            var t = new Material(m);
            t.SetColor("_BaseColor", m.GetColor("_BaseColor") * c);
            s.assets.Add(t);
            return t;
        }

        static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            VhLookUv.BoxProject(go);
            return go;
        }

        // ---------------------------------------------------------------------------------------------------------
        // The cast in one light
        // ---------------------------------------------------------------------------------------------------------

        static bool Lying(VhCharacter ch) => ch != null && ch.idleState.StartsWith("lie", StringComparison.Ordinal);

        static void Register(Stage s, VhCharacter ch)
        {
            if (ch.animator == null) return;
            ch.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var arm = ch.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (arm != null) s.bind[ch] = arm.localRotation;
        }

        static void Pass(string light, string dir, List<GameObject> prefabs)
        {
            var s = BuildStage(light);
            try
            {
                var people = new List<VhCharacter>();
                foreach (var p in prefabs)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(p, s.scene);
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
                    var ch = go.GetComponent<VhCharacter>();
                    if (ch == null) { UnityEngine.Object.DestroyImmediate(go); continue; }
                    people.Add(ch);
                    Register(s, ch);
                    PlayClip(s, ch, ch.idleState, 0.6f);
                }

                // The lineup: the standing cast side by side, facing the camera (-Z), in idle, then talking.
                var standing = people.FindAll(c => !Lying(c));
                foreach (var ch in people) ch.gameObject.SetActive(!Lying(ch));
                if (standing.Count > 0)
                {
                    LineUp(s, standing);
                    Frame(s.cam, PosedBounds(standing), 8f, 6f, 30f, 1.12f);
                    Save(s, Path.Combine(dir, light + "-lineup.png"));
                    foreach (var ch in standing)
                    {
                        PlayClip(s, ch, "talk", 1.1f);
                        if (ch.face != null) ch.face.Preview(Math.Max(0, ch.face.IndexOf("warm")), 0.6f, false);
                    }
                    LineUp(s, standing);
                    Frame(s.cam, PosedBounds(standing), 8f, 6f, 30f, 1.12f);
                    Save(s, Path.Combine(dir, light + "-lineup-talk.png"));
                    foreach (var ch in standing)
                    {
                        PlayClip(s, ch, ch.idleState, 0.6f);
                        if (ch.face != null) ch.face.Preview(0, 0f, false);
                    }
                }

                // Pairs: Elias and Rafael (the dream man and the intern), Mags and the representative (the ward's visitors).
                Pair(s, dir, light, people, "Elias", "Rafael");
                Pair(s, dir, light, people, "Mags", "Representative");

                foreach (var ch in people)
                {
                    foreach (var other in people) other.gameObject.SetActive(other == ch);
                    ch.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
                    GameObject bed = null;
                    if (Lying(ch))
                    {
                        if (light != "hospital") continue;
                        bed = Bed(s, ch);
                    }
                    else
                    {
                        Pose(s, ch);
                        FaceToward(ch, Vector3.back);
                    }
                    Pose(s, ch);
                    ShowWalls(s, false);   // the turntable's camera circles where the ward's walls are
                    Turntable(s, ch, bed, Path.Combine(dir, light + "-turn-" + ch.name + ".png"));
                    ShowWalls(s, true);
                    Faces(s, ch, Path.Combine(dir, light + "-face-" + ch.name + ".png"));
                    Sequence(s, ch, dir, light);
                    if (bed != null)
                    {
                        var b = PosedBounds(new List<VhCharacter> { ch });
                        foreach (var r in bed.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                        Frame(s.cam, b, -35f, 24f, 34f, 1.12f);
                        Save(s, Path.Combine(dir, "hospital-bed-" + ch.name + ".png"));
                        FaceShot(s, ch, -20f, 26f);
                        Save(s, Path.Combine(dir, "hospital-bed-" + ch.name + "-close.png"));
                        UnityEngine.Object.DestroyImmediate(bed);
                    }
                }
            }
            finally { s.Dispose(); }
        }

        static void LineUp(Stage s, List<VhCharacter> standing)
        {
            float x = -(standing.Count - 1) * 0.5f * 0.95f;
            foreach (var ch in standing)
            {
                ch.transform.SetPositionAndRotation(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
                x += 0.95f;
                Pose(s, ch);
                FaceToward(ch, Vector3.back);
            }
        }

        static void Pair(Stage s, string dir, string light, List<VhCharacter> people, string a, string b)
        {
            var A = people.Find(c => c.characterName == a);
            var B = people.Find(c => c.characterName == b);
            if (A == null || B == null) return;
            foreach (var other in people) other.gameObject.SetActive(other == A || other == B);
            A.transform.SetPositionAndRotation(new Vector3(-0.55f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
            B.transform.SetPositionAndRotation(new Vector3(0.55f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
            PlayClip(s, A, "talk", 1.1f);
            PlayClip(s, B, "listen", 0.9f);
            if (A.face != null) A.face.Preview(Math.Max(0, A.face.IndexOf("warm")), 0.6f, false);
            if (B.face != null) B.face.Preview(0, 0f, false);
            // Both face the camera (-Z), each turned 20 degrees toward the other.
            Pose(s, A);
            FaceToward(A, Quaternion.Euler(0f, -20f, 0f) * Vector3.back);
            Pose(s, B);
            FaceToward(B, Quaternion.Euler(0f, 20f, 0f) * Vector3.back);
            Frame(s.cam, PosedBounds(new List<VhCharacter> { A, B }), -12f, 4f, 28f, 1.08f);
            Save(s, Path.Combine(dir, light + "-pair-" + a + "-" + b + ".png"));
            if (A.Head != null && B.Head != null)
            {
                var mid = (A.Head.position + B.Head.position) * 0.5f;
                Aim(s.cam, mid - Vector3.up * 0.22f, -8f, 3f, 2.4f, 30f);
                Save(s, Path.Combine(dir, light + "-pair-" + a + "-" + b + "-close.png"));
            }
            if (A.face != null) A.face.Preview(0, 0f, false);
            PlayClip(s, A, A.idleState, 0.6f);
            PlayClip(s, B, B.idleState, 0.6f);
        }

        /// <summary>A ward bed under the lying character: its long axis along hips-to-head, the body on the mattress
        /// (its lowest posed vertex 1 cm into the sheet), the pillow under the head.</summary>
        static GameObject Bed(Stage s, VhCharacter ch)
        {
            Pose(s, ch);
            var hips = ch.animator != null ? ch.animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            Vector3 head = ch.Head != null ? ch.Head.position : ch.transform.position;
            Vector3 hp = hips != null ? hips.position : ch.transform.position;
            Vector3 axis = head - hp;
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-4f)
            {
                Debug.LogWarning("JAMKIT_VH_PEOPLE " + ch.characterName + ": not lying down (head over hips): is 'lie' playing?");
                axis = Vector3.forward;
            }
            axis.Normalize();
            const float top = 0.62f;
            var bodyBounds = PosedBounds(new List<VhCharacter> { ch });
            ch.transform.position += Vector3.up * (top - bodyBounds.min.y - 0.01f);
            Pose(s, ch);
            head = ch.Head != null ? ch.Head.position : head;
            hp = hips != null ? hips.position : hp;
            var center = head - axis * (2.05f * 0.5f - 0.3f);
            var side = Vector3.Cross(Vector3.up, axis);
            center += side * Vector3.Dot((hp + head) * 0.5f - center, side);
            center.y = 0f;
            var bed = new GameObject("Ward bed");
            SceneManager.MoveGameObjectToScene(bed, s.scene);
            bed.transform.SetPositionAndRotation(center, Quaternion.LookRotation(axis, Vector3.up));
            var sheet = Plain(s, new Color(0.87f, 0.91f, 0.93f), 0.15f, 0f);
            var frame = Plain(s, new Color(0.72f, 0.74f, 0.76f), 0.6f, 0.8f);
            var blanket = Plain(s, new Color(0.62f, 0.72f, 0.84f), 0.2f, 0f);
            Child(bed, "Mattress", new Vector3(0f, top - 0.1f, 0f), new Vector3(0.95f, 0.2f, 2.05f), sheet);
            Child(bed, "Frame", new Vector3(0f, top - 0.27f, 0f), new Vector3(1.0f, 0.12f, 2.15f), frame);
            Child(bed, "Pillow", new Vector3(0f, top + 0.02f, 0.78f), new Vector3(0.62f, 0.1f, 0.36f), sheet);
            Child(bed, "Blanket", new Vector3(0f, top + 0.03f, -0.42f), new Vector3(0.98f, 0.06f, 1.1f), blanket);
            Child(bed, "Headboard", new Vector3(0f, top + 0.3f, 1.05f), new Vector3(1.0f, 0.75f, 0.05f), frame);
            foreach (float sx in new[] { -0.45f, 0.45f })
                foreach (float sz in new[] { -0.98f, 0.98f })
                    Child(bed, "Leg", new Vector3(sx, (top - 0.33f) * 0.5f, sz), new Vector3(0.05f, top - 0.33f, 0.05f), frame);
            Debug.Log("JAMKIT_VH_PEOPLE bed: " + ch.characterName + " lying along " + axis.ToString("F2") + ", lowest point at " +
                      PosedBounds(new List<VhCharacter> { ch }).min.y.ToString("F3") + " m (mattress top " + top + ")");
            return bed;
        }

        static Material Plain(Stage s, Color c, float smooth, float metal)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            s.assets.Add(m);
            return m;
        }

        static void Child(GameObject parent, string name, Vector3 local, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        static void Turntable(Stage s, VhCharacter ch, GameObject bed, string path)
        {
            var sheet = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var b = PosedBounds(new List<VhCharacter> { ch });
            if (bed != null) foreach (var r in bed.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            for (int i = 0; i < 8; i++)
            {
                Frame(s.cam, b, i * 45f, bed != null ? 25f : 6f, 26f, 1.1f, 0.5f);
                Render(s);
                Tile(s, sheet, i % 4, i / 4, 4, 2);
            }
            Write(sheet, path);
        }

        /// <summary>Close-ups of the whole head at conversation distance: neutral, talking half / open, a blink, each
        /// expression, then happy and talking; four tiles a row.</summary>
        static void Faces(Stage s, VhCharacter ch, string path)
        {
            var face = ch.face;
            var faces = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var fill = new Color32[Width * Height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(24, 24, 28, 255);
            faces.SetPixels32(fill);
            int exprs = face != null ? face.ExpressionCount : 1;
            int tiles = Mathf.Min(16, 4 + Mathf.Max(0, exprs - 1) + 1);
            int rows = (tiles + 3) / 4;
            for (int i = 0; i < tiles; i++)
            {
                if (face != null)
                {
                    if (i == 0) face.Preview(0, 0f, false);
                    else if (i == 1) face.Preview(0, 0.5f, false);
                    else if (i == 2) face.Preview(0, 1f, false);
                    else if (i == 3) face.Preview(0, 0f, true);
                    else if (i == tiles - 1) face.Preview(Math.Max(0, face.IndexOf("happy")), 0.8f, false);
                    else face.Preview(i - 3, 0f, false);
                }
                FaceShot(s, ch, i % 2 == 0 ? -12f : 12f, 22f);
                Render(s);
                Tile(s, faces, i % 4, i / 4, 4, rows);
            }
            if (face != null) face.Preview(0, 0f, false);
            Write(faces, path);
        }

        /// <summary>One second of talking at 24 fps on the face's own clock (deterministic): warm, a blink at frame 10,
        /// worried from frame 15; the idle clip runs under it. A 6 x 4 sheet plus the frames as JPGs.</summary>
        static void Sequence(Stage s, VhCharacter ch, string dir, string light)
        {
            var face = ch.face;
            if (face == null) return;
            string frames = Path.Combine(dir, "seq-" + light + "-" + ch.name);
            Directory.CreateDirectory(frames);
            var sheet = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            UnityEngine.Random.InitState(1234);
            face.Preview(Math.Max(0, face.IndexOf("warm")), 0f, false);
            face.ResetClock(0f);
            bool blinked = face.blinking;
            face.blinking = false;   // the one blink is placed by hand
            face.SetTalking(true);
            s.poses.TryGetValue(ch, out var posed);
            float t0 = posed != null ? posed.time : 0f;
            float dt = 1f / SeqFps;
            for (int i = 0; i < SeqFrames; i++)
            {
                float t = i * dt;
                if (posed != null)
                {
                    posed.time = Mathf.Repeat(t0 + t, Mathf.Max(0.1f, posed.clip.length));
                    Sample(s, ch, false);
                }
                ch.Pose(dt);
                if (i == 9 && blinked) face.BlinkAt(t);
                if (i == 15) face.SetExpression("worried", 0.12f);
                face.Tick(t, dt);
                FaceShot(s, ch, -10f, 22f);
                Render(s);
                Tile(s, sheet, i % 6, i / 6, 6, 4);
                WriteJpg(s, Path.Combine(frames, i.ToString("000") + ".jpg"));
            }
            face.SetTalking(false);
            face.blinking = blinked;
            if (posed != null) { posed.time = t0; Sample(s, ch, false); }
            face.Preview(0, 0f, false);
            Write(sheet, Path.Combine(dir, light + "-seq-" + ch.name + ".png"));
        }

        /// <summary>The camera in front of the face (the face decal's posed centre against the head bone), framing the
        /// whole head from the top of the hair (HeadTop_End) to below the chin.</summary>
        static void FaceShot(Stage s, VhCharacter ch, float yawOffset, float fov)
        {
            var head = ch.Head;
            Vector3 hp = head != null ? head.position : ch.transform.position + Vector3.up * 1.6f;
            Vector3 fwd = ch.transform.forward, up = Vector3.up;
            var neck = ch.animator != null ? ch.animator.GetBoneTransform(HumanBodyBones.Neck) : null;
            if (neck != null && head != null) up = (hp - neck.position).normalized;
            Vector3 faceC = hp + fwd * 0.08f;
            float chin = -0.12f;
            var decal = ch.face != null ? ch.face.decal as SkinnedMeshRenderer : null;
            if (decal != null && Baked(decal, out var pts))
            {
                faceC = Vector3.zero;
                foreach (var p in pts) faceC += p;
                faceC /= pts.Count;
                fwd = Vector3.ProjectOnPlane(faceC - hp, up);
                if (fwd.sqrMagnitude < 1e-6f) fwd = ch.transform.forward;
                fwd.Normalize();
                chin = 0f;
                foreach (var p in pts) chin = Mathf.Min(chin, Vector3.Dot(p - faceC, up));
            }
            up = Vector3.ProjectOnPlane(up, fwd).normalized;
            if (up.sqrMagnitude < 0.5f) up = Vector3.up;
            var topT = FindBone(ch.transform, "HeadTop_End");
            float top = topT != null ? Vector3.Dot(topT.position - faceC, up) + 0.015f : 0.16f;
            float bottom = chin - 0.04f;
            Vector3 centre = faceC + up * ((top + bottom) * 0.5f);
            float half = (top - bottom) * 0.5f * 1.12f;
            float dist = half / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var dir = Quaternion.AngleAxis(yawOffset, up) * fwd;
            s.cam.transform.position = centre + dir * dist;
            s.cam.transform.rotation = Quaternion.LookRotation(-dir, up);
            s.cam.fieldOfView = fov;
            s.cam.aspect = (float)Width / Height;
        }

        static Transform FindBone(Transform root, string bone)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                int c = n.LastIndexOf(':');
                if ((c >= 0 ? n.Substring(c + 1) : n) == bone) return t;
            }
            return null;
        }

        /// <summary>Turns a standing character about its feet so its face (the face decal's posed centre, seen from the
        /// head) looks along <paramref name="want"/> (horizontal).</summary>
        static void FaceToward(VhCharacter ch, Vector3 want)
        {
            want.y = 0f;
            if (want.sqrMagnitude < 1e-6f || ch.Head == null) return;
            Vector3 fwd = ch.transform.forward;
            var decal = ch.face != null ? ch.face.decal as SkinnedMeshRenderer : null;
            if (decal != null && Baked(decal, out var pts))
            {
                Vector3 c = Vector3.zero;
                foreach (var p in pts) c += p;
                fwd = c / pts.Count - ch.Head.position;
            }
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) return;
            float ang = Vector3.SignedAngle(fwd, want, Vector3.up);
            if (Mathf.Abs(ang) < 0.5f) return;
            ch.transform.RotateAround(ch.transform.position, Vector3.up, ang);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Paper beside the 3D Elias
        // ---------------------------------------------------------------------------------------------------------

        static void Paper(string dir, GameObject eliasPrefab)
        {
            var s = BuildStage("golden");
            try
            {
                if (eliasPrefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(eliasPrefab, s.scene);
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 200f, 0f));
                    var elias = go.GetComponent<VhCharacter>();
                    Register(s, elias);
                    PlayClip(s, elias, "talk", 1.3f);
                    if (elias.face != null) elias.face.Preview(Math.Max(0, elias.face.IndexOf("warm")), 0.5f, false);
                    Pose(s, elias);
                    FaceToward(elias, Quaternion.Euler(0f, 20f, 0f) * Vector3.back);
                }
                var walt = CutOut(s, "Walt (cut-out)", "dream_walt", new Vector3(1.25f, 0f, 0.35f), 1.86f, 160f);
                var lena = CutOut(s, "Lena (cut-out)", "dream_lena", new Vector3(-1.3f, 0f, 0.55f), 1.88f, 200f);
                var center = new Vector3(0f, 0.95f, 0.3f);
                Aim(s.cam, center, -12f, 4f, 5.4f, 34f);
                Save(s, Path.Combine(dir, "paper-front.png"));
                Aim(s.cam, center + Vector3.right * 0.6f, 52f, 6f, 4.6f, 34f);
                Save(s, Path.Combine(dir, "paper-edge.png"));
                Aim(s.cam, center, 150f, 8f, 5.2f, 34f);
                Save(s, Path.Combine(dir, "paper-back.png"));
                Aim(s.cam, new Vector3(0.62f, 1.55f, 0.2f), -25f, 2f, 2.2f, 30f);
                Save(s, Path.Combine(dir, "paper-close-walt-elias.png"));
                if (lena != null)
                {
                    // Lena's wrists (her star watch, an overlay on her cut-out).
                    Aim(s.cam, lena.transform.position + Vector3.up * 0.95f, 20f, 2f, 1.4f, 30f);
                    Save(s, Path.Combine(dir, "paper-close-lena.png"));
                }
                if (walt != null && walt.PaperRenderer == null) Debug.LogWarning("JAMKIT_VH_PEOPLE paper: Walt has no paper mesh (no GPU?)");
                if (lena != null && lena.PaperRenderer == null) Debug.LogWarning("JAMKIT_VH_PEOPLE paper: Lena has no paper mesh (no GPU?)");
            }
            finally { s.Dispose(); }
        }

        static DreamCutoutView CutOut(Stage s, string name, string art, Vector3 feet, float height, float yaw)
        {
            string path = "Assets/JamKit/Templates/VisitingHours/Art/Dream/" + art + ".png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) { Debug.LogWarning("JAMKIT_VH_PEOPLE no " + path); return null; }
            var mat = new Material(Shader.Find(DreamCutoutView.ShaderName));
            mat.SetTexture("_MainTex", tex);
            s.assets.Add(mat);
            var view = DreamCutoutView.Create(name, tex, null, feet, height, yaw, mat);
            SceneManager.MoveGameObjectToScene(view.gameObject, s.scene);
            view.Rebuild();
            return view;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Lena's watch on Nadia's right wrist, first person
        // ---------------------------------------------------------------------------------------------------------

        static void Watch(string dir)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(NadiaModel);
            if (model == null) { Debug.LogWarning("JAMKIT_VH_PEOPLE no " + NadiaModel); return; }
            var s = BuildStage("golden");
            try
            {
                var nadia = (GameObject)PrefabUtility.InstantiatePrefab(model, s.scene);
                nadia.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var b = RendererBounds(nadia);
                if (b.size.y > 0.1f && b.size.y < 1.3f) nadia.transform.localScale *= 1.65f / b.size.y;   // the raw Tripo export: about 1 m
                // Her own textures (the game's first-person body gets its material from World's builders; the raw model
                // has none, which was round 1's "grey mitten").
                var skin = NadiaMaterial(s);
                if (skin != null)
                    foreach (var r in nadia.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++) mats[i] = skin;
                        r.sharedMaterials = mats;
                    }
                var t = nadia.transform;
                Transform arm = FindBone(t, "RightArm"), fore = FindBone(t, "RightForeArm"), hand = FindBone(t, "RightHand"), head = FindBone(t, "Head");
                Transform foot = FindBone(t, "LeftFoot"), toe = FindBone(t, "LeftToeBase");
                if (arm == null || fore == null || hand == null || head == null) { Debug.LogWarning("JAMKIT_VH_PEOPLE Nadia: no arm bones"); return; }
                Vector3 f = toe != null && foot != null ? Vector3.ProjectOnPlane(toe.position - foot.position, Vector3.up).normalized : Vector3.forward;
                if (f.sqrMagnitude < 0.5f) f = Vector3.forward;
                Vector3 up = Vector3.up, right = Vector3.Cross(up, f).normalized;
                // Her right arm raised to look at the watch: the elbow down and forward, the forearm across her body.
                float l1 = (fore.position - arm.position).magnitude, l2 = (hand.position - fore.position).magnitude;
                Vector3 elbow = arm.position + (-0.62f * up + 0.62f * f + 0.18f * right).normalized * l1;
                arm.rotation = Quaternion.FromToRotation(fore.position - arm.position, elbow - arm.position) * arm.rotation;
                Vector3 wrist = fore.position + (0.32f * up + 0.5f * f - 0.8f * right).normalized * l2;
                fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, wrist - fore.position) * fore.rotation;
                // Turn the forearm about its own axis so the back of the wrist looks up at her eyes, then fit the watch
                // (the fit measures the posed skin, so it comes after the pose).
                Vector3 eye = head.position + f * 0.11f + up * 0.05f;
                var probe = VhKeepsakeWatch.Attach(t);
                if (probe == null) return;
                Vector3 axis = (hand.position - fore.position).normalized;
                Vector3 want = Vector3.ProjectOnPlane(eye - probe.transform.position, axis);
                Vector3 have = Vector3.ProjectOnPlane(probe.transform.up, axis);
                UnityEngine.Object.DestroyImmediate(probe.gameObject);
                fore.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(have, want, axis), axis) * fore.rotation;
                var watch = VhKeepsakeWatch.Attach(t);
                if (watch == null) return;
                watch.SetTime(9f, 41f, 12f);
                watch.SetOwned(true);
                Debug.Log("JAMKIT_VH_PEOPLE watch: wrist " + (watch.measured ? "measured" : "ESTIMATED") + ", half-axes " +
                          (watch.wrist * 1000f).ToString("F1") + " mm");

                // First person: from her eyes, the head hidden (as the first-person body does).
                head.localScale = Vector3.one * 0.001f;
                s.cam.transform.position = eye;
                s.cam.transform.rotation = Quaternion.LookRotation(watch.transform.position - eye - up * 0.05f, up);
                s.cam.fieldOfView = 62f;
                Save(s, Path.Combine(dir, "watch-first-person.png"));
                s.cam.transform.position = eye + (watch.transform.position - eye) * 0.6f;
                s.cam.transform.rotation = Quaternion.LookRotation(watch.transform.position - s.cam.transform.position, up);
                s.cam.fieldOfView = 30f;
                Save(s, Path.Combine(dir, "watch-close.png"));
                // The hands move (for the Night 4 mechanic): the same shot at another time.
                watch.SetTime(2f, 5f, 40f);
                Save(s, Path.Combine(dir, "watch-close-2-05.png"));
                // Along the arm, from the hand's side: how the case sits on the strap and the strap on the skin.
                Vector3 wp = watch.transform.position, wa = watch.transform.forward, wb = watch.transform.up;
                s.cam.transform.position = wp + wa * 0.16f + wb * 0.03f;
                s.cam.transform.rotation = Quaternion.LookRotation(wp - s.cam.transform.position, wb);
                s.cam.fieldOfView = 30f;
                Save(s, Path.Combine(dir, "watch-along-arm.png"));
            }
            finally { s.Dispose(); }
        }

        /// <summary>URP Lit with Nadia's own base colour (and normal map when it is imported as one).</summary>
        static Material NadiaMaterial(Stage s)
        {
            string folder = Path.GetDirectoryName(NadiaModel).Replace('\\', '/');
            Texture2D baseMap = null, normal = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (n == "color" || n.EndsWith("_color")) baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                else if (n.Contains("normal") && imp != null && imp.textureType == TextureImporterType.NormalMap) normal = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            if (baseMap == null) { Debug.LogWarning("JAMKIT_VH_PEOPLE Nadia: no Color texture in " + folder); return null; }
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Nadia (capture)" };
            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.3f);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            s.assets.Add(m);
            return m;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Posing, framing, rendering
        // ---------------------------------------------------------------------------------------------------------

        static AnimationClip FindClip(VhCharacter ch, string state)
        {
            var ctrl = ch.animator != null ? ch.animator.runtimeAnimatorController : null;
            if (ctrl == null) return null;
            foreach (var c in ctrl.animationClips) if (c != null && c.name == state) return c;
            return null;
        }

        static void PlayClip(Stage s, VhCharacter ch, string state, float time)
        {
            if (ch == null || ch.animator == null) return;
            var clip = FindClip(ch, state);
            if (clip == null) { Debug.LogWarning("JAMKIT_VH_PEOPLE " + ch.characterName + ": no clip '" + state + "' (bind pose)"); return; }
            s.poses[ch] = new Posed { clip = clip, time = Mathf.Min(time, clip.length * 0.5f) };
            Sample(s, ch, true);
        }

        /// <summary>Samples the character's stored clip at its time: AnimationMode first; a playable graph if the left
        /// upper arm is still in its bind rotation afterwards.</summary>
        static void Sample(Stage s, VhCharacter ch, bool log)
        {
            if (!s.poses.TryGetValue(ch, out var p) || ch.animator == null || !ch.gameObject.activeInHierarchy) return;
            var anim = ch.animator;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var arm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(anim.gameObject, p.clip, p.time);
            AnimationMode.EndSampling();
            string how = "animation mode";
            if (arm != null && s.bind.TryGetValue(ch, out var b0) && Quaternion.Angle(b0, arm.localRotation) < 2f)
            {
                var playable = AnimationPlayableUtilities.PlayClip(anim, p.clip, out var graph);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playable.SetTime(p.time);
                graph.Evaluate();
                s.graphs.Add(graph);
                how = Quaternion.Angle(b0, arm.localRotation) < 2f ? "FAILED (still the bind pose)" : "a playable graph";
            }
            if (log) Debug.Log("JAMKIT_VH_PEOPLE pose " + ch.characterName + " '" + p.clip.name + "' at " + p.time.ToString("F2") + " s via " + how);
        }

        /// <summary>Re-samples every active character's clip (after moving one), then applies breathing and look-at.</summary>
        static void Pose(Stage s, VhCharacter ch)
        {
            foreach (var kv in s.poses) Sample(s, kv.Key, false);
            if (ch != null) ch.Pose(0f);
        }

        static readonly List<Vector3> bakeVerts = new List<Vector3>();
        static readonly List<Vector3> bakedWorld = new List<Vector3>();
        static Mesh bakeMesh;

        /// <summary>A skinned mesh's posed vertices in world space (every 3rd). The list is reused by the next call.</summary>
        static bool Baked(SkinnedMeshRenderer smr, out List<Vector3> world)
        {
            world = bakedWorld;
            world.Clear();
            if (smr == null || smr.sharedMesh == null || !smr.gameObject.activeInHierarchy) return false;
            if (bakeMesh == null) bakeMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            smr.BakeMesh(bakeMesh, true);
            bakeMesh.GetVertices(bakeVerts);
            var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            for (int i = 0; i < bakeVerts.Count; i += 3) world.Add(m.MultiplyPoint3x4(bakeVerts[i]));
            return world.Count > 0;
        }

        /// <summary>The posed bounds of characters: their skinned meshes baked (not the bind-pose AABBs).</summary>
        static Bounds PosedBounds(List<VhCharacter> list)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var ch in list)
                foreach (var r in ch.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r.enabled) continue;
                    if (r is SkinnedMeshRenderer smr)
                    {
                        if (!Baked(smr, out var pts)) continue;
                        foreach (var p in pts) { if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p); }
                    }
                    else { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                }
            return any ? b : new Bounds(Vector3.up, Vector3.one * 2f);
        }

        static Bounds RendererBounds(GameObject go)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Frames bounds from a direction (yaw about Y, 0 = from -Z; pitch down), tight with a margin.</summary>
        static void Frame(Camera cam, Bounds b, float yaw, float pitchDown, float fov, float margin, float aspect = 16f / 9f)
        {
            float vHalf = fov * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * aspect);
            float radiusV = b.extents.y * margin, radiusH = Mathf.Max(b.extents.x, b.extents.z) * margin;
            float dist = Mathf.Max(radiusV / Mathf.Tan(vHalf), radiusH / Mathf.Tan(hHalf)) + Mathf.Max(b.extents.x, b.extents.z);
            Aim(cam, b.center, yaw, pitchDown, dist, fov);
        }

        /// <summary>The camera on a circle around target, looking along yaw (0 sees a -Z-facing character's front), tilted
        /// pitchDown degrees, at distance, vertical field of view fov. Renders are 16:9; tiles crop their centre.</summary>
        static void Aim(Camera cam, Vector3 target, float yaw, float pitchDown, float distance, float fov)
        {
            var rot = Quaternion.Euler(pitchDown, yaw, 0f);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
            cam.fieldOfView = fov;
            cam.aspect = (float)Width / Height;
        }

        static void Render(Stage s)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = s.rt };
            for (int i = 0; i < 2; i++)   // warm-up: shadow atlases, the volume stack
            {
                if (RenderPipeline.SupportsRenderRequest(s.cam, request)) RenderPipeline.SubmitRenderRequest(s.cam, request);
                else { s.cam.targetTexture = s.rt; s.cam.Render(); s.cam.targetTexture = null; }
            }
        }

        static void Tile(Stage s, Texture2D sheet, int col, int row, int cols, int rows)
        {
            int tw = Width / cols, th = Height / rows;
            float aspect = (float)tw / th;
            int cw = Mathf.Min(Width, Mathf.RoundToInt(Height * aspect)), chh = Mathf.Min(Height, Mathf.RoundToInt(Width / aspect));
            var prev = RenderTexture.active;
            RenderTexture.active = s.rt;
            var crop = new Texture2D(cw, chh, TextureFormat.RGB24, false);
            crop.ReadPixels(new Rect((Width - cw) / 2, (Height - chh) / 2, cw, chh), 0, 0);
            crop.Apply();
            RenderTexture.active = prev;
            var scaled = Scale(crop, tw, th);
            UnityEngine.Object.DestroyImmediate(crop);
            sheet.SetPixels(col * tw, Height - (row + 1) * th, tw, th, scaled);
            sheet.Apply();
        }

        static Color[] Scale(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tmp = new Texture2D(w, h, TextureFormat.RGB24, false);
            tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tmp.GetPixels();
            UnityEngine.Object.DestroyImmediate(tmp);
            return px;
        }

        static void Save(Stage s, string path)
        {
            Render(s);
            var prev = RenderTexture.active;
            RenderTexture.active = s.rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Write(tex, path);
        }

        /// <summary>The last render at half size as a JPG (sequence frames).</summary>
        static void WriteJpg(Stage s, string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = s.rt;
            var full = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            full.Apply();
            RenderTexture.active = prev;
            var half = new Texture2D(Width / 2, Height / 2, TextureFormat.RGB24, false);
            half.SetPixels(Scale(full, Width / 2, Height / 2));
            half.Apply();
            File.WriteAllBytes(path, half.EncodeToJPG(90));
            UnityEngine.Object.DestroyImmediate(full);
            UnityEngine.Object.DestroyImmediate(half);
        }

        static void Write(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("JAMKIT_VH_PEOPLE_CAPTURE " + path);
        }
    }
}
