using System;
using System.Collections;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Everything a fight would otherwise make on first use, made at scene start instead (Task 17's frame-time fix), so the
    /// first bolt, spark, puddle, spot or wreck mid-fight creates no objects, materials or textures:
    /// <list type="bullet">
    /// <item><see cref="Pools"/>: the tracer, impact, spark, bolt and acid-puddle pools filled; the crack's, the beam's and
    /// the bots' shared materials made; every bot's generated pixel faces made.</item>
    /// <item><see cref="ShowOnce"/>: one tiny copy of each kind of runtime drawing (a lit primitive, a glowing one, a bot's
    /// face, a bark bubble's text) shown in front of the camera for two frames, so the graphics driver builds their
    /// pipeline states during the scene's first frames (before the recording starts and inside the frame-time probe's
    /// warm-up), not on the frame a bot first sparks or barks.</item>
    /// <item>Judge round 2 (perf): every sound's data decoded and, in the arena, every bot's body built ahead, spread over the
    /// scene's first frames (<see cref="SkylineWarmQueue"/>, <see cref="SkylineBodyPool"/>).</item>
    /// </list>
    /// </summary>
    public static class SkylinePrewarm
    {
        public static void Pools()
        {
            SkylineTracer.Prewarm();
            SkylineImpact.Prewarm();
            SkylineSpark.Prewarm();
            SkylineBolt.Prewarm();
            SkylinePuddle.Prewarm();
            SkylineSpotMarker.PrewarmLooks();
            SkylineBotLook.Lit(SkylineBotLook.White);
            SkylineBotLook.Lit(SkylineBotLook.Gold);
            SkylineBotLook.Lit(SkylineBotLook.Dark);
            SkylineBotLook.Lit(SkylineBotLook.Grey);
            SkylineBotLook.Lit(SkylineBotLook.Lens);
            SkylineBotLook.Lit(SkylineBotLook.Acid);
            SkylineBotLook.Lit(SkylineBotLook.Scorched);
            foreach (var def in SkylineEnemies.All())
                foreach (BotFace face in Enum.GetValues(typeof(BotFace)))
                    SkylineBotFace.Texture(def.id, face);
            // the bots' models (Task 20 review, Minor 10): loaded now, not on an encounter's first spawn; held so an unload
            // of unused assets can't drop them. ShowOnce draws each once.
            var models = new System.Collections.Generic.List<GameObject>();
            foreach (var def in SkylineEnemies.All())
                foreach (var name in new[] { def.id, def.id + "_mount" })
                {
                    var m = Resources.Load<GameObject>(BotModelFolder + name);
                    if (m != null) models.Add(m);
                }
            _models = models.ToArray();
            WarmSounds();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == ArenaScene) WarmArenaBodies();
        }

        const string BotModelFolder = "SkylineModels/bots/", SoundFolder = "SkylineAudio", ArenaScene = "SkylineArena";
        static GameObject[] _models = new GameObject[0];
        static AudioClip[] _clips = new AudioClip[0];

        /// <summary>
        /// Judge round 2 (perf): every Skyline sound is imported with "preload audio data" off and "decompress on load", so a
        /// sound's first play loaded and decoded the whole clip on the main thread, mid-fight: a bot's idle loop as each bot
        /// spawned, its alert, shot, hit and wreck sounds the first time each happened. The clips are loaded now and their
        /// data decoded on <see cref="SkylineWarmQueue"/>, a few a frame, during the scene's first second (held here, so an
        /// unload of unused assets can't drop them). <see cref="SkylineSounds.Get"/> later loads the same objects, already
        /// decoded.
        /// </summary>
        static void WarmSounds()
        {
            _clips = Resources.LoadAll<AudioClip>(SoundFolder);
            foreach (var clip in _clips)
            {
                var c = clip;
                if (c == null || c.loadState == AudioDataLoadState.Loaded) continue;
                SkylineWarmQueue.Add(() => { if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData(); });
            }
        }

        /// <summary>The arena's bot bodies, one for every spawn of its three fights in fight order, built ahead on
        /// <see cref="SkylineWarmQueue"/> (<see cref="SkylineBodyPool"/>): an encounter's start then only moves them into place.</summary>
        static void WarmArenaBodies()
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var e in GalleriaArena.Encounters())
                if (e != null && e.spawns != null)
                    foreach (var s in e.spawns) ids.Add(s.enemy);
            SkylineBodyPool.Fill(ids);
        }

        /// <summary>Shows one tiny copy of each runtime drawing 0.6 m in front of <paramref name="cam"/> for two frames, then
        /// removes it.</summary>
        /// <summary>True while <see cref="ShowOnce"/> is still warming (perf round 4: <see cref="SkylineStartCover"/> waits for
        /// it, so the warm-up frames stay under the cover; round 4's glyph frames, 36-41 ms, came just after it lifted).</summary>
        public static bool Busy { get; private set; }

        public static IEnumerator ShowOnce(Camera cam)
        {
            Busy = true;
            try
            {
                yield return ShowOnceSteps(cam);
            }
            finally
            {
                Busy = false;
            }
        }

        static IEnumerator ShowOnceSteps(Camera cam)
        {
            if (cam == null) yield break;
            var root = new GameObject("Prewarm (two frames)").transform;
            root.SetPositionAndRotation(cam.transform.position + cam.transform.forward * 0.6f, cam.transform.rotation);
            root.localScale = Vector3.one * 0.01f;
            SkylineBotLook.Visual(PrimitiveType.Cube, "Lit", root, SkylineBotLook.Lit(SkylineBotLook.Grey));
            SkylineBotLook.Visual(PrimitiveType.Sphere, "Lit sphere", root, SkylineBotLook.Lit(SkylineBotLook.Gold)).transform.localPosition = Vector3.right;
            SkylineBotLook.Visual(PrimitiveType.Cube, "Glow", root, SkylineBotLook.Glow(new Color(5f, 3.8f, 1.6f)), false).transform.localPosition = Vector3.left;
            var face = SkylineBotLook.Visual(PrimitiveType.Quad, "Face", root, SkylineBotLook.Lit(Color.white), false);
            face.transform.localPosition = Vector3.up;
            var faceMat = new Material(face.GetComponent<Renderer>().sharedMaterial) { mainTexture = SkylineBotFace.Texture(SkylineEnemies.DroneId, BotFace.Happy) };
            face.GetComponent<Renderer>().sharedMaterial = faceMat;
            var bubble = SkylineBarkBubble.Create(root, Vector3.down * 0.01f, "Halt!");
            bubble.transform.localScale = Vector3.one * 0.01f;
            // one tiny copy of each bot model (no colliders yet: those are added by FromModel at spawn), so its meshes are
            // uploaded and its imported materials' pipeline states built now
            for (int i = 0; i < _models.Length; i++)
                if (_models[i] != null)
                    Object.Instantiate(_models[i], root, false).transform.localPosition = new Vector3(2f + i, 0f, 0f);
            yield return null;
            yield return null;
            if (bubble != null) Object.Destroy(bubble.gameObject);
            Object.Destroy(root.gameObject);
            Object.Destroy(faceMat);
            if (cam == null) yield break;
            yield return DrawEverythingOnce(cam);
            yield return WarmGlyphs();
        }

        // ------------------------------------------------------------------ perf round 3: first-use hitches

        /// <summary>
        /// Perf round 3: round 3's perf run had a 197 ms frame right as the food court's first floor-care unit opened up, and
        /// fight frames of 20-25 ms with no garbage collection. On Direct3D 12 the first draw of any new combination of shader
        /// variant, mesh layout and render state builds a pipeline state on the main thread, so the first acid stream, puddle,
        /// bolt, spark, impact or gun effect each stalled a frame mid-fight. The shipped pipeline-state trace
        /// (<see cref="SkylineStartCover"/>) is the full fix, but it takes a pass to record. This one works now: two frames
        /// after the scene starts (the pools and the guns' effects are built by then), every switched-off pooled renderer's
        /// mesh and materials are drawn once, tiny, 0.6 m in front of the camera (one per distinct mesh layout and material
        /// set), and so are the acid's materials on its stream's and droplets' mesh layouts and every material in
        /// <c>Resources/SkylineFx</c>, with their shadows. Two frames later they are gone. It all happens behind the start cover.
        /// </summary>
        static IEnumerator DrawEverythingOnce(Camera cam)
        {
            // Perf round 4: round 4 still stalled 220 ms (waiting on the render thread) on the first Writ shot. Its muzzle
            // flash and gun light live in the viewmodel's rig, drawn by the viewmodel's overlay camera, and are switched off by
            // disabling the renderer, not the object, so round 3's scan (switched-off objects only, all drawn in front of the
            // main camera) missed them. Now every renderer that isn't drawing (object off, or renderer off) is warmed, on its
            // own layer, in front of a camera that draws that layer (the main camera when it does, else the viewmodel's).
            var roots = new Dictionary<Camera, Transform>();
            var seen = new HashSet<string>();
            var temp = new List<Object>();
            int n = 0;
            Vector3 Slot() { int i = n++; return new Vector3((i % 12 - 6) * 0.004f, (i / 12 % 12 - 6) * 0.004f, 0f); }
            Transform RootFor(int layer)
            {
                Camera pick = (cam.cullingMask & (1 << layer)) != 0 ? cam : null;
                if (pick == null)
                    foreach (var c in Camera.allCameras)
                        if (c != null && (c.cullingMask & (1 << layer)) != 0) { pick = c; break; }
                if (pick == null) return null;
                if (roots.TryGetValue(pick, out var t) && t != null) return t;
                float d = Mathf.Clamp(0.6f, pick.nearClipPlane * 4f, Mathf.Max(pick.nearClipPlane * 4f, pick.farClipPlane * 0.5f));
                t = new GameObject("Prewarm (every pooled drawing, two frames) " + pick.name).transform;
                t.SetPositionAndRotation(pick.transform.position + pick.transform.forward * d, pick.transform.rotation);
                roots[pick] = t;
                return t;
            }
            var root = RootFor(cam.gameObject.layer) ?? RootFor(0);
            if (root == null) yield break;

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r == null || (r.enabled && r.gameObject.activeInHierarchy) || n >= 400) continue;
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                var mats = r.sharedMaterials;
                if (mesh == null || mats == null || mats.Length == 0) continue;
                int layer = r.gameObject.layer;
                if (!seen.Add(layer + "/" + Layout(mesh) + Ids(mats))) continue;
                var at = RootFor(layer);
                if (at != null) Draw(at, mesh, mats, r.shadowCastingMode, Slot()).layer = layer;
            }
            foreach (var lr in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (lr != null && !(lr.enabled && lr.gameObject.activeInHierarchy) && lr.sharedMaterial != null && seen.Add(lr.gameObject.layer + "line" + lr.sharedMaterial.GetInstanceID()))
                {
                    var at = RootFor(lr.gameObject.layer);
                    if (at != null) Line(at, lr.sharedMaterial, Slot()).layer = lr.gameObject.layer;
                }
            foreach (var tr in Object.FindObjectsByType<TrailRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tr != null && tr.sharedMaterial != null && seen.Add(tr.gameObject.layer + "line" + tr.sharedMaterial.GetInstanceID()))
                {
                    var at = RootFor(tr.gameObject.layer);
                    if (at != null) Line(at, tr.sharedMaterial, Slot()).layer = tr.gameObject.layer;
                }

            // the acid: its stream (positions, normals and UVs, rebuilt each frame) and its droplets (spheres)
            var tube = StripMesh();
            temp.Add(tube);
            var sphere = SkylineBotLook.PrimitiveMesh(PrimitiveType.Sphere);
            foreach (var m in AcidMaterials())
            {
                if (m == null) continue;
                Draw(root, tube, new[] { m }, UnityEngine.Rendering.ShadowCastingMode.Off, Slot());
                if (sphere != null) Draw(root, sphere, new[] { m }, UnityEngine.Rendering.ShadowCastingMode.Off, Slot());
            }
            if (sphere != null)
                foreach (var m in Resources.LoadAll<Material>("SkylineFx"))
                    if (m != null && seen.Add("fx" + m.GetInstanceID()))
                        Draw(root, sphere, new[] { m }, UnityEngine.Rendering.ShadowCastingMode.On, Slot());

            yield return null;
            yield return null;
            foreach (var t in roots.Values) if (t != null) Object.Destroy(t.gameObject);
            foreach (var o in temp) if (o != null) Object.Destroy(o);
            Debug.Log("SKYLINE_PREWARM drew " + n + " idle drawings once (" + roots.Count + " cameras)");
        }

        static Material[] AcidMaterials() => new[]
        {
            SkylineBotLook.AcidMaterial(new Color(0.42f, 0.82f, 0.1f), new Color(0.12f, 0.38f, 0.02f), 0.97f),
            SkylineBotLook.Wet(SkylineAcid.AcidWet),
        };

        static GameObject Draw(Transform root, Mesh mesh, Material[] mats, UnityEngine.Rendering.ShadowCastingMode shadows, Vector3 at)
        {
            var go = new GameObject("warm");
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            go.transform.localScale = Vector3.one * 0.003f / Mathf.Max(0.01f, mesh.bounds.extents.magnitude);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = shadows;
            return go;
        }

        static GameObject Line(Transform root, Material m, Vector3 at)
        {
            var go = new GameObject("warm line");
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = m;
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, at);
            lr.SetPosition(1, at + Vector3.right * 0.003f);
            lr.widthMultiplier = 0.002f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A tiny strip with positions, normals and UVs (the acid stream's mesh layout).</summary>
        static Mesh StripMesh()
        {
            var m = new Mesh { name = "Prewarm strip" };
            m.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        static string Layout(Mesh mesh)
        {
            var sb = new System.Text.StringBuilder(64);
            foreach (var a in mesh.GetVertexAttributes()) sb.Append((int)a.attribute).Append(':').Append((int)a.format).Append('x').Append(a.dimension).Append(';');
            sb.Append("sub").Append(mesh.subMeshCount);
            return sb.ToString();
        }

        static string Ids(Material[] mats)
        {
            var sb = new System.Text.StringBuilder(32);
            foreach (var m in mats) sb.Append('|').Append(m != null ? m.GetInstanceID() : 0);
            return sb.ToString();
        }

        /// <summary>
        /// Perf round 3: the HUD (UI Toolkit) draws its text from a dynamic font atlas, so the first time a character shows (a
        /// new line's or a bark's letters) its glyph is rendered into the atlas on the main thread, and a line full of new
        /// characters (CONCIERGE's notices are long) costs a long frame. Every printable ASCII character plus every character
        /// of the teaching lines and barks is shown once, regular and bold, nearly transparent, for three frames (behind the
        /// start cover), so the atlas holds them all before the first fight.
        /// </summary>
        static IEnumerator WarmGlyphs()
        {
            for (int i = 0; i < 60 && SkylineHud.Instance == null; i++) yield return null;
            var hud = SkylineHud.Instance;
            var doc = hud != null ? hud.GetComponent<UnityEngine.UIElements.UIDocument>() : null;
            var docRoot = doc != null ? doc.rootVisualElement : null;
            if (docRoot == null) yield break;
            var chars = new HashSet<char>();
            for (char c = ' '; c <= '~'; c++) chars.Add(c);
            foreach (char c in "·…—–’‘“”•×°") chars.Add(c);
            foreach (var b in TeachingBeats.M1()) if (b != null && b.text != null) foreach (char c in b.text) chars.Add(c);
            foreach (var def in SkylineEnemies.All())
                foreach (var bark in SkylineBarks.Ids)
                {
                    var line = SkylineBarks.Line(def.id, bark);
                    if (line != null) foreach (char c in line) chars.Add(c);
                }
            var sb = new System.Text.StringBuilder(chars.Count);
            foreach (char c in chars) sb.Append(c);
            string text = sb.ToString();
            var labels = new List<UnityEngine.UIElements.Label>();
            foreach (var bold in new[] { false, true })
                foreach (var size in new[] { 12f, 24f })
                {
                    var l = new UnityEngine.UIElements.Label(text) { pickingMode = UnityEngine.UIElements.PickingMode.Ignore };
                    l.style.position = UnityEngine.UIElements.Position.Absolute;
                    l.style.left = 0f;
                    l.style.top = 0f;
                    l.style.fontSize = size;
                    l.style.opacity = 0.01f;
                    l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
                    docRoot.Add(l);
                    labels.Add(l);
                }
            yield return null;
            yield return null;
            yield return null;
            foreach (var l in labels) l.RemoveFromHierarchy();
            Debug.Log("SKYLINE_PREWARM glyphs shown: " + text.Length + " characters");
        }
    }
}
