using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.VisitingHours.Characters.EditorTools
{
    /// <summary>
    /// Captures of the 3D cast for the judge (docs/redesign/people.md "Captures"): every prefab in
    /// Characters/Resources/People under two lights, golden hour outdoors and the hospital ward's cool overhead light,
    /// at 1920x1080 through URP with post-processing:
    ///   &lt;light&gt;-lineup.png          the whole cast side by side, idling
    ///   &lt;light&gt;-turn-&lt;Name&gt;.png     a turntable sheet: eight angles, 45 degrees apart
    ///   &lt;light&gt;-face-&lt;Name&gt;.png     conversation-distance close-ups: neutral, talking (half / open), a blink, then
    ///                               each expression
    /// written to out/vh-people/&lt;time&gt;/. Batch mode needs graphics (no -nographics): tools/tripo/vh-people-capture.ps1.
    /// The lights here are a neutral stand-in for judging the models; the Look agent's scene lighting is the real one.
    /// </summary>
    public static class VhPeopleCapture
    {
        public const int Width = 1920, Height = 1080;

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
            // Height order reads best in a lineup: tallest in the middle would hide nobody; keep the cast order instead.
            prefabs.Sort((a, b) => Order(a.name).CompareTo(Order(b.name)));
            Light("golden", dir, prefabs);
            Light("hospital", dir, prefabs);
            return dir;
        }

        static int Order(string name)
        {
            for (int i = 0; i < VhPeopleBuild.Cast.Length; i++) if (VhPeopleBuild.Cast[i].name == name) return i;
            return 100;
        }

        sealed class Stage
        {
            public Camera cam;
            public readonly List<GameObject> owned = new List<GameObject>();
            public readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            public RenderTexture rt;
        }

        static void Light(string light, string dir, List<GameObject> prefabs)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var stage = BuildStage(light);
            var graphs = new List<PlayableGraph>();
            try
            {
                // The lineup.
                var people = new List<VhCharacter>();
                float x = -(prefabs.Count - 1) * 0.55f;
                foreach (var p in prefabs)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
                    go.transform.SetPositionAndRotation(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
                    x += 1.1f;
                    stage.owned.Add(go);
                    var ch = go.GetComponent<VhCharacter>();
                    people.Add(ch);
                    Idle(ch, graphs);
                }
                float span = Mathf.Max(2.5f, prefabs.Count * 1.1f + 0.6f);
                Aim(stage.cam, new Vector3(0f, 1.0f, 0f), 0f, 4f, span / (2f * Mathf.Tan(16f * Mathf.Deg2Rad)) * 0.62f, 32f);
                Render(stage);   // warm-up: shadow atlases, post-processing textures
                Save(stage, Path.Combine(dir, light + "-lineup.png"));

                // One at a time: a turntable sheet and a face sheet.
                foreach (var ch in people)
                {
                    foreach (var other in people) other.gameObject.SetActive(other == ch);
                    var t = ch.transform;
                    Vector3 home = t.position;
                    t.position = Vector3.zero;
                    float h = StandingHeight(ch);
                    var sheet = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    for (int i = 0; i < 8; i++)
                    {
                        Aim(stage.cam, new Vector3(0f, h * 0.52f, 0f), i * 45f, 6f, h * 2.5f, 26f);
                        Render(stage);
                        Tile(stage, sheet, i % 4, i / 4, 4, 2);
                    }
                    Write(sheet, Path.Combine(dir, light + "-turn-" + ch.name + ".png"));

                    var face = ch.face;
                    var head = ch.Head;
                    Vector3 headPos = head != null ? head.position + Vector3.up * 0.05f : new Vector3(0f, h * 0.92f, 0f);
                    int expressions = face != null ? face.ExpressionCount : 0;
                    int tiles = 4 + Mathf.Max(0, expressions - 1);
                    int cols = 4, rows = tiles > 4 ? 2 : 1;
                    var faces = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    // Fill the background so unused tiles aren't black.
                    var fill = new Color32[Width * Height];
                    for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(24, 24, 28, 255);
                    faces.SetPixels32(fill);
                    for (int i = 0; i < Mathf.Min(tiles, cols * 2); i++)
                    {
                        if (face != null)
                        {
                            if (i == 0) face.Preview(0, 0f, false);
                            else if (i == 1) face.Preview(0, 0.5f, false);
                            else if (i == 2) face.Preview(0, 1f, false);
                            else if (i == 3) face.Preview(0, 0f, true);
                            else face.Preview(i - 3, 0f, false);
                        }
                        // Conversation distance: head and shoulders, a little off-axis.
                        Aim(stage.cam, headPos - Vector3.up * 0.08f, (i % 2 == 0 ? -12f : 12f), 2f, 1.3f, 24f);
                        Render(stage);
                        Tile(stage, faces, i % cols, i / cols, cols, rows);
                    }
                    if (face != null) face.Preview(0, 0f, false);
                    Write(faces, Path.Combine(dir, light + "-face-" + ch.name + ".png"));
                    t.position = home;
                }
            }
            finally
            {
                foreach (var g in graphs) if (g.IsValid()) g.Destroy();
                foreach (var o in stage.owned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
                foreach (var a in stage.assets) if (a != null) UnityEngine.Object.DestroyImmediate(a);
                if (stage.rt != null) { stage.rt.Release(); UnityEngine.Object.DestroyImmediate(stage.rt); }
            }
        }

        // ---- The stage ----

        static Stage BuildStage(string light)
        {
            var s = new Stage();
            bool golden = light == "golden";

            var camGo = new GameObject("Capture Camera");
            s.owned.Add(camGo);
            s.cam = camGo.AddComponent<Camera>();
            s.cam.nearClipPlane = 0.05f;
            s.cam.farClipPlane = 400f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            // Sky and ambient.
            if (golden)
            {
                var sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_SunSize", 0.05f);
                sky.SetFloat("_AtmosphereThickness", 1.35f);
                sky.SetColor("_SkyTint", new Color(0.62f, 0.62f, 0.7f));
                sky.SetColor("_GroundColor", new Color(0.42f, 0.36f, 0.3f));
                sky.SetFloat("_Exposure", 1.15f);
                s.assets.Add(sky);
                RenderSettings.skybox = sky;
                s.cam.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.52f, 0.6f, 0.78f);
                RenderSettings.ambientEquatorColor = new Color(0.72f, 0.6f, 0.5f);
                RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.2f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.92f, 0.76f, 0.6f);
                RenderSettings.fogStartDistance = 25f;
                RenderSettings.fogEndDistance = 180f;
            }
            else
            {
                RenderSettings.skybox = null;
                s.cam.clearFlags = CameraClearFlags.SolidColor;
                s.cam.backgroundColor = new Color(0.62f, 0.68f, 0.68f);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.5f, 0.54f, 0.56f);
                RenderSettings.fog = false;
            }

            // Key and fill.
            var sun = Directional("Key", golden ? new Vector3(14f, -38f, 0f) : new Vector3(62f, -20f, 0f),
                golden ? new Color(1f, 0.74f, 0.48f) : new Color(0.9f, 0.96f, 1f), golden ? 2.4f : 1.5f, true);
            s.owned.Add(sun.gameObject);
            RenderSettings.sun = sun;
            var rim = Directional("Rim", golden ? new Vector3(20f, 150f, 0f) : new Vector3(35f, 160f, 0f),
                golden ? new Color(0.55f, 0.65f, 1f) : new Color(1f, 0.93f, 0.82f), golden ? 0.55f : 0.35f, false);
            s.owned.Add(rim.gameObject);

            // Ground: a broad disc, grass-green at golden hour, ward lino in the hospital; a back wall in the ward.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(golden ? 120f : 14f, 0.01f, golden ? 120f : 14f);
            ground.transform.position = new Vector3(0f, -0.01f, 0f);
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            gm.SetColor("_BaseColor", golden ? new Color(0.42f, 0.47f, 0.27f) : new Color(0.7f, 0.72f, 0.68f));
            gm.SetFloat("_Smoothness", golden ? 0.1f : 0.45f);
            s.assets.Add(gm);
            ground.GetComponent<Renderer>().sharedMaterial = gm;
            s.owned.Add(ground);
            if (!golden)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall";
                wall.transform.position = new Vector3(0f, 1.6f, 3.5f);
                wall.transform.localScale = new Vector3(14f, 3.2f, 0.1f);
                var wm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                wm.SetColor("_BaseColor", new Color(0.78f, 0.84f, 0.8f));
                wm.SetFloat("_Smoothness", 0.2f);
                s.assets.Add(wm);
                wall.GetComponent<Renderer>().sharedMaterial = wm;
                s.owned.Add(wall);
            }

            // Post-processing: filmic tonemapping, a little bloom on highlights, a touch of warmth at golden hour.
            var volGo = new GameObject("Capture Volume");
            s.owned.Add(volGo);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            s.assets.Add(profile);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(golden ? 0.35f : 0.2f);
            color.saturation.Override(golden ? 8f : -4f);
            color.contrast.Override(8f);
            if (golden)
            {
                var wb = profile.Add<WhiteBalance>(true);
                wb.temperature.Override(8f);
            }
            vol.sharedProfile = profile;

            s.rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            return s;
        }

        static Light Directional(string name, Vector3 euler, Color c, float intensity, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.85f;
            return l;
        }

        // ---- Posing ----

        static void Idle(VhCharacter ch, List<PlayableGraph> graphs)
        {
            if (ch == null || ch.animator == null) return;
            AnimationClip idle = null;
            var ctrl = ch.animator.runtimeAnimatorController;
            if (ctrl != null)
                foreach (var c in ctrl.animationClips) { if (c != null && c.name == ch.idleState) { idle = c; break; } }
            if (idle == null) return;   // no clips yet: the bind pose
            var playable = AnimationPlayableUtilities.PlayClip(ch.animator, idle, out var graph);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable.SetTime(Mathf.Min(0.6f, idle.length * 0.3f));
            graph.Evaluate();
            graphs.Add(graph);
        }

        static float StandingHeight(VhCharacter ch)
        {
            var cap = ch.GetComponent<CapsuleCollider>();
            return cap != null ? cap.height : 1.75f;
        }

        /// <summary>Places the camera on a circle around target, looking along yaw (degrees about Y; the characters face -Z,
        /// so yaw 0 sees their front from -Z), tilted pitchDown degrees, at distance, with the vertical field of view.
        /// The render is always 16:9; tiles crop its centre.</summary>
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
            if (RenderPipeline.SupportsRenderRequest(s.cam, request)) RenderPipeline.SubmitRenderRequest(s.cam, request);
            else { s.cam.targetTexture = s.rt; s.cam.Render(); s.cam.targetTexture = null; }
        }

        /// <summary>Copies the centre of the last render (cropped to the tile's aspect) into one tile of the sheet.</summary>
        static void Tile(Stage s, Texture2D sheet, int col, int row, int cols, int rows)
        {
            int tw = Width / cols, th = Height / rows;
            float aspect = (float)tw / th;   // the render is 16:9: crop its centre to the tile's shape
            int cw = Mathf.Min(Width, Mathf.RoundToInt(Height * aspect)), chh = Mathf.Min(Height, Mathf.RoundToInt(Width / aspect));
            var prev = RenderTexture.active;
            RenderTexture.active = s.rt;
            var crop = new Texture2D(cw, chh, TextureFormat.RGB24, false);
            crop.ReadPixels(new Rect((Width - cw) / 2, (Height - chh) / 2, cw, chh), 0, 0);
            crop.Apply();
            RenderTexture.active = prev;
            var scaled = Scale(crop, tw, th);
            UnityEngine.Object.DestroyImmediate(crop);
            // Rows count from the top of the image.
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

        static void Write(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("JAMKIT_VH_PEOPLE_CAPTURE " + path);
        }
    }
}
