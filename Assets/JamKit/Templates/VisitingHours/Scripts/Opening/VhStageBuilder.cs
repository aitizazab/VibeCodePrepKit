using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Builds the opening's sets in code (art style E, the waking world): Nadia's bedroom at night and
    /// the title table. Kenney's CC0 furniture (the boot asset's models) where it fits, restyled into the
    /// palette; everything else made here (the room, the bed and its blanket, the phone, the tablet, the
    /// mug, the notepad, the badge). Pictures come from the boot asset's art list, or stand-ins drawn in
    /// code (<see cref="VhTextures"/>). Nadia is her rigged model when the boot asset has it, or a
    /// stand-in arm and hair (<see cref="VhNadiaRig"/>).
    /// Layout: metres, Y up. The bedroom's floor centre is the origin, its bed's head against +Z, the
    /// window in the -X wall; the table is at <see cref="VhStage.TableOrigin"/>, its top at 0.75 m.
    /// </summary>
    public static class VhStageBuilder
    {
        public const string RootName = "Visiting Hours: opening and title";

        /// <summary>Nadia's eyes on the pillow (the first-person camera).</summary>
        public static readonly Vector3 Eyes = new Vector3(-0.40f, 0.80f, 1.22f);
        /// <summary>Her right (upper) shoulder while she lies on her left side, facing the nightstand.</summary>
        public static readonly Vector3 Shoulder = new Vector3(-0.34f, 0.88f, 0.98f);

        static bool _loggedMissing;

        /// <summary>Builds both sets into the active scene. <paramref name="screens"/>: draw the phone's and the tablet's screens (play mode).</summary>
        public static VhStage Build(VisitingHoursBoot boot, bool screens)
        {
            var root = new GameObject(RootName);
            var s = root.AddComponent<VhStage>();
            s.boot = boot;
            s.style = new VhWakingStyle(boot != null ? boot.wakingMaterial : null);
            s.meshes = new VhMeshKit();
            s.textures = new VhTextures();
            s.bedSky = new Color(0.075f, 0.085f, 0.125f);
            s.bedGround = new Color(0.035f, 0.035f, 0.05f);
            s.tableSky = new Color(0.36f, 0.39f, 0.45f);
            s.tableGround = new Color(0.20f, 0.19f, 0.19f);

            // One camera for both sets; the title scene's own camera rests while we're here.
            foreach (var c in Camera.allCameras)
                if (c != null && c.enabled && c.gameObject.scene == root.scene)
                {
                    s.hiddenSceneCamera = c;
                    c.enabled = false;
                    break;
                }
            var camGo = new GameObject("Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.012f, 0.02f);
            cam.depth = 5f;
            cam.fieldOfView = 60f;
            s.cam = cam;

            BuildBedroom(s);
            BuildTable(s);
            Poses(s);
            // The creator's edits (the opening editor's saved overrides, by stable ids): everything but the lights the
            // lighting places itself goes before the lighting (so the blinds' stripes follow a moved streetlight)...
            var edits = VhSceneEdits.Load();
            s.edits = VhSceneEdits.Register(s);
            s.edits.Apply(edits, false);
            VhLighting.Apply(s);
            // ...and the rest after it (the morning sun and the lighting's own lamps, which it places).
            s.edits.RegisterLights();
            s.edits.Apply(edits, true);
            if (s.blanketDirty) ReshapeBlanket(s);
            Sounds(s);
            if (screens) Screens(s);
            else
            {
                SetScreenPicture(s.phoneScreenMat, Art(s, "phone_wallpaper", () => s.textures.Wallpaper()), 1f);
                SetScreenPicture(s.tabletScreenMat, Art(s, "news_0", () => s.textures.NewsStill(1)), 1f);
            }
            if (s.missingArt.Count > 0 && !_loggedMissing)
            {
                _loggedMissing = true;
                Debug.Log("JAMKIT_VH art missing (opening and title; stand-ins drawn in code): " + string.Join(", ", s.missingArt));
            }
            // perf.md (Day items 6 and 7): the stand-in pictures compressed and off the CPU; Nadia's skinning only on screen.
            s.textures.Seal();
            VhTitlePerf.Settle(s);
            return s;
        }

        // ---------- Helpers ----------

        static Texture2D Art(VhStage s, string name, Func<Texture2D> fallback)
        {
            var t = s.boot != null ? s.boot.Art(name) : null;
            if (t != null) return t;
            if (!s.missingArt.Contains(name)) s.missingArt.Add(name);
            return fallback();
        }

        static MeshRenderer Box(VhStage s, Transform parent, string name, Vector3 size, Vector3 pos, Material mat, float uvScale = 1f, Quaternion? rot = null, bool shadows = true) =>
            VhMeshKit.Show(parent, name, s.meshes.Box(size, uvScale), mat, pos, rot ?? Quaternion.identity, shadows);

        static Transform Pivot(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = rot;
            return t;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>
        /// Places one of the boot asset's models (Kenney's furniture), restyled into the palette, scaled so
        /// its height (or, with <paramref name="byWidth"/>, its widest side) is <paramref name="size"/>,
        /// standing on <paramref name="floor"/> and turned <paramref name="yaw"/> degrees. Null when the model is missing.
        /// </summary>
        static GameObject PlaceModel(VhStage s, string name, Transform parent, Vector3 floor, float yaw, float size, bool byWidth = false)
        {
            var prefab = s.boot != null ? s.boot.Model(name) : null;
            if (prefab == null)
            {
                if (!s.missingArt.Contains("model " + name)) s.missingArt.Add("model " + name);
                return null;
            }
            var go = UnityEngine.Object.Instantiate(prefab, parent);
            go.name = name;
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            s.style.Restyle(go);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var b = WorldBounds(go);
            float have = byWidth ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
            if (have > 1e-4f) go.transform.localScale = Vector3.one * (size / have);
            b = WorldBounds(go);
            Vector3 bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 target = parent.TransformPoint(floor);
            go.transform.position += target - bottom;
            go.transform.RotateAround(target, Vector3.up, yaw);
            return go;
        }

        static float TopOf(GameObject go) => WorldBounds(go).max.y;

        /// <summary>Shows the centre of a picture at <paramref name="aspect"/> (width / height) on a material,
        /// <paramref name="zoom"/> > 1 cropping in further (the art is 16:9; a coffee is round, a page tall).</summary>
        static Material Crop(Material m, Texture t, float aspect, float zoom = 1f)
        {
            if (m == null || t == null || t.height <= 0 || !m.HasProperty("_BaseMap")) return m;
            float have = (float)t.width / t.height;
            float sx = 1f, sy = 1f;
            if (have > aspect) sx = aspect / have;
            else sy = have / aspect;
            sx /= Mathf.Max(1f, zoom);
            sy /= Mathf.Max(1f, zoom);
            m.SetTextureScale("_BaseMap", new Vector2(sx, sy));
            m.SetTextureOffset("_BaseMap", new Vector2((1f - sx) * 0.5f, (1f - sy) * 0.5f));
            return m;
        }

        static Material ScreenMat(VhStage s, string name) => s.style.Screen(null, 1f, name);

        static void SetScreenPicture(Material m, Texture t, float brightness)
        {
            if (m == null) return;
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", t != null ? t : Texture2D.whiteTexture);
            VhWakingStyle.SetEmission(m, Color.white * brightness);
        }

        // ---------- The bedroom ----------

        static void BuildBedroom(VhStage s)
        {
            var b = new GameObject("Bedroom").transform;
            b.SetParent(s.transform, false);
            s.bedroom = b;
            var st = s.style;
            var tx = s.textures;
            var plaster = Art(s, "tex_bedroom_wall", () => tx.Plaster());
            var floorTex = Art(s, "tex_bedroom_floor", () => tx.Wood(VhWakingStyle.Floor, 2));
            var woodTex = Art(s, "tex_wood", () => tx.Wood(VhWakingStyle.Wood, 4));
            var knit = Art(s, "tex_blanket", () => tx.Knit());
            Material wall = st.Mat(VhWakingStyle.Wall, plaster), ceiling = st.Mat(VhWakingStyle.Ceiling, plaster);
            Material floor = st.Mat(VhWakingStyle.Floor, floorTex), wood = st.Mat(VhWakingStyle.Wood, woodTex);
            Material trim = st.Mat(new Color(0.70f, 0.70f, 0.68f)), slat = st.Mat(new Color(0.74f, 0.73f, 0.70f), null, 0f);

            // The room: 3.6 x 3.4 m, 2.6 m high, the window in the -X wall.
            Box(s, b, "Floor", new Vector3(3.76f, 0.05f, 3.56f), new Vector3(0f, -0.025f, 0f), floor, 1.2f);
            Box(s, b, "Ceiling", new Vector3(3.76f, 0.05f, 3.56f), new Vector3(0f, 2.625f, 0f), ceiling, 0.6f);
            Box(s, b, "Wall (head)", new Vector3(3.76f, 2.6f, 0.08f), new Vector3(0f, 1.3f, 1.74f), wall, 0.6f);
            Box(s, b, "Wall (foot)", new Vector3(3.76f, 2.6f, 0.08f), new Vector3(0f, 1.3f, -1.74f), wall, 0.6f);
            Box(s, b, "Wall (right)", new Vector3(0.08f, 2.6f, 3.56f), new Vector3(1.84f, 1.3f, 0f), wall, 0.6f);
            // The window wall, around the opening (z -0.45..0.75, y 0.95..2.15).
            Box(s, b, "Wall (window, below)", new Vector3(0.08f, 0.95f, 3.56f), new Vector3(-1.84f, 0.475f, 0f), wall, 0.6f);
            Box(s, b, "Wall (window, above)", new Vector3(0.08f, 0.45f, 3.56f), new Vector3(-1.84f, 2.375f, 0f), wall, 0.6f);
            Box(s, b, "Wall (window, foot side)", new Vector3(0.08f, 1.2f, 1.33f), new Vector3(-1.84f, 1.55f, -1.115f), wall, 0.6f);
            Box(s, b, "Wall (window, head side)", new Vector3(0.08f, 1.2f, 1.03f), new Vector3(-1.84f, 1.55f, 1.265f), wall, 0.6f);
            Box(s, b, "Sill", new Vector3(0.22f, 0.04f, 1.34f), new Vector3(-1.77f, 0.94f, 0.15f), trim);
            Box(s, b, "Window head", new Vector3(0.12f, 0.05f, 1.3f), new Vector3(-1.84f, 2.15f, 0.15f), trim);
            Box(s, b, "Window jamb", new Vector3(0.12f, 1.2f, 0.05f), new Vector3(-1.84f, 1.55f, -0.45f), trim);
            Box(s, b, "Window jamb", new Vector3(0.12f, 1.2f, 0.05f), new Vector3(-1.84f, 1.55f, 0.75f), trim);
            Box(s, b, "Window bar", new Vector3(0.05f, 1.2f, 0.04f), new Vector3(-1.86f, 1.55f, 0.15f), trim);
            // Venetian blinds, half open: the streetlight comes through in stripes.
            Box(s, b, "Blind rail", new Vector3(0.06f, 0.04f, 1.22f), new Vector3(-1.75f, 2.11f, 0.15f), trim);
            for (int i = 0; i < 22; i++)
                Box(s, b, "Blind slat", new Vector3(0.05f, 0.004f, 1.18f), new Vector3(-1.75f, 1.0f + i * 0.051f, 0.15f), slat, 1f, Quaternion.Euler(0f, 0f, -38f));
            Box(s, b, "Door", new Vector3(0.9f, 2.02f, 0.05f), new Vector3(0.95f, 1.01f, -1.69f), st.Mat(VhWakingStyle.WoodPale, woodTex));
            Box(s, b, "Door handle", new Vector3(0.1f, 0.025f, 0.04f), new Vector3(0.6f, 1.0f, -1.65f), st.Mat(VhWakingStyle.Metal));

            // Outside: the street below, buildings across it with a few lit windows, the streetlight.
            var outside = st.Mat(VhWakingStyle.Outside, null, 0f);
            Box(s, b, "Building", new Vector3(1f, 7f, 4f), new Vector3(-9f, -1.5f, -3f), outside, 1f, null, false);
            Box(s, b, "Building", new Vector3(1f, 9f, 3f), new Vector3(-10f, -1f, 1.2f), outside, 1f, null, false);
            Box(s, b, "Building", new Vector3(1f, 6f, 4f), new Vector3(-8.5f, -2f, 5f), outside, 1f, null, false);
            var lit = st.Unique(Color.black, null, "VH lit window");
            VhWakingStyle.SetEmission(lit, new Color(0.55f, 0.45f, 0.30f));
            Vector3[] windows = { new Vector3(-8.45f, 1.4f, -2.2f), new Vector3(-8.45f, 0.2f, -3.6f), new Vector3(-9.45f, 2.6f, 0.6f), new Vector3(-9.45f, 1.0f, 1.8f), new Vector3(-7.95f, 0.6f, 4.2f) };
            foreach (var w in windows) VhMeshKit.Show(b, "Lit window", s.meshes.Quad(0.5f, 0.7f), lit, w, Quaternion.Euler(0f, -90f, 0f), false);
            var metalDark = st.Mat(VhWakingStyle.MetalDark);
            VhMeshKit.Show(b, "Streetlight pole", s.meshes.Cylinder(0.06f, 0.05f, 6.2f, 10), metalDark, new Vector3(-3.6f, -5.7f, 0.3f), Quaternion.identity);
            Box(s, b, "Streetlight arm", new Vector3(0.3f, 0.05f, 0.06f), new Vector3(-3.45f, 0.5f, 0.3f), metalDark);
            var lamp = st.Unique(new Color(0.3f, 0.28f, 0.25f), null, "VH streetlight");
            VhWakingStyle.SetEmission(lamp, new Color(1f, 0.85f, 0.6f) * 2.2f);
            VhMeshKit.Show(b, "Streetlight lamp", s.meshes.RoundedBox(new Vector3(0.34f, 0.07f, 0.18f), 0.03f, 4), lamp, new Vector3(-3.3f, 0.46f, 0.3f), Quaternion.identity, false);

            var streetGo = new GameObject("Streetlight (light)");
            streetGo.transform.SetParent(b, false);
            streetGo.transform.localPosition = new Vector3(-3.25f, 0.42f, 0.32f);
            streetGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.4f, 2.6f, 0.45f) - streetGo.transform.localPosition);
            var street = streetGo.AddComponent<Light>();
            street.type = LightType.Spot;
            street.range = 9f;
            street.spotAngle = 80f;
            street.innerSpotAngle = 45f;
            street.color = new Color(1f, 0.82f, 0.6f);
            street.intensity = s.streetIntensity;
            street.shadows = LightShadows.Soft;
            street.shadowStrength = 0.92f;
            s.street = street;

            var moonGo = new GameObject("Moon (light)");
            moonGo.transform.SetParent(b, false);
            moonGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.75f, -0.5f, 0.15f));
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.62f, 0.85f);
            moon.intensity = s.moonIntensity;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.85f;
            s.moon = moon;

            // The bed: frame, headboard, mattress, pillows, and the blanket over her.
            Box(s, b, "Bed frame", new Vector3(1.68f, 0.3f, 2.14f), new Vector3(0.2f, 0.17f, 0.61f), wood);
            Box(s, b, "Headboard", new Vector3(1.74f, 1.05f, 0.07f), new Vector3(0.2f, 0.525f, 1.70f), wood);
            var sheetTex = s.boot != null ? s.boot.Art("tex_bedsheet") : null;
            VhMeshKit.Show(b, "Mattress", s.meshes.RoundedBox(new Vector3(1.58f, 0.24f, 2.04f), 0.05f, 5), st.Mat(sheetTex != null ? Color.white : VhWakingStyle.Sheet, sheetTex), new Vector3(0.2f, 0.43f, 0.61f), Quaternion.identity);
            var pillowMat = st.Mat(sheetTex != null ? new Color(1.05f, 1.05f, 1.05f) : VhWakingStyle.Pillow, sheetTex);
            var pillow = s.meshes.RoundedBox(new Vector3(0.64f, 0.12f, 0.42f), 0.055f, 6, 0.035f);
            VhMeshKit.Show(b, "Pillow (hers)", pillow, pillowMat, new Vector3(-0.22f, 0.6f, 1.36f), Quaternion.Euler(0f, 4f, 0f));
            VhMeshKit.Show(b, "Pillow", pillow, pillowMat, new Vector3(0.64f, 0.6f, 1.38f), Quaternion.Euler(0f, -3f, 0f));
            var blanketMat = st.Mat(VhWakingStyle.Blanket, knit, -1f);
            s.blanketMaterial = blanketMat;

            // The nightstand, the phone on it, the lamp (off), a framed photo.
            var nightstand = PlaceModel(s, "nightstand", b, new Vector3(-0.9f, 0f, 1.36f), 180f, 0.52f);
            float top;
            if (nightstand != null) top = TopOf(nightstand);
            else
            {
                Box(s, b, "Nightstand", new Vector3(0.46f, 0.52f, 0.4f), new Vector3(-0.9f, 0.26f, 1.36f), wood);
                Box(s, b, "Nightstand drawer", new Vector3(0.38f, 0.16f, 0.01f), new Vector3(-0.9f, 0.36f, 1.155f), st.Mat(VhWakingStyle.WoodDark));
                top = 0.52f;
            }
            // What's on it, placed on its real top (the model's bounds): the phone on the bed's side within her reach,
            // the lamp at the back by the wall, the framed photo at the front corner turned towards her pillow.
            Bounds nb = nightstand != null ? WorldBounds(nightstand) : new Bounds(new Vector3(-0.9f, 0.26f, 1.36f), new Vector3(0.46f, 0.52f, 0.4f));
            float inset = 0.06f;
            Vector3 lampAt = new Vector3(nb.min.x + inset + 0.07f, top, nb.max.z - inset - 0.07f);
            if (PlaceModel(s, "lamp", b, lampAt, 0f, 0.42f) == null)
            {
                VhMeshKit.Show(b, "Lamp base", s.meshes.Cylinder(0.06f, 0.05f, 0.03f, 16), st.Mat(VhWakingStyle.Metal), lampAt, Quaternion.identity);
                VhMeshKit.Show(b, "Lamp stem", s.meshes.Cylinder(0.012f, 0.012f, 0.24f, 8), st.Mat(VhWakingStyle.Metal), lampAt + new Vector3(0f, 0.03f, 0f), Quaternion.identity);
                VhMeshKit.Show(b, "Lamp shade", s.meshes.Cylinder(0.11f, 0.075f, 0.15f, 20), st.Mat(VhWakingStyle.Lampshade), lampAt + new Vector3(0f, 0.24f, 0f), Quaternion.identity);
            }
            // The phone lies with its bottom end 4 cm over the bed-side edge (most of it on the wood): she takes it by
            // that end, her fingers under it and her palm on its back, so it never turns over in her hand.
            Vector3 phoneAt = new Vector3(nb.max.x + PhoneOverhang - PhoneSize.z * 0.5f, top, Mathf.Clamp(1.27f, nb.min.z + inset + 0.08f, nb.max.z - inset - 0.08f));
            BuildPhone(s, b, phoneAt);
            BuildFrame(s, b, new Vector3(nb.min.x + inset + 0.05f, top, nb.min.z + inset + 0.06f));

            // Her things: scrubs over the chair, a rug, a low bookcase with books, a plant.
            var chair = PlaceModel(s, "chair", b, new Vector3(1.3f, 0f, -0.95f), -125f, 0.85f);
            Vector3 seat = new Vector3(1.3f, 0.47f, -0.95f);
            if (chair == null)
            {
                var c = Pivot(b, "Chair (stand-in)", new Vector3(1.3f, 0f, -0.95f), Quaternion.Euler(0f, -125f, 0f));
                Box(s, c, "Seat", new Vector3(0.44f, 0.05f, 0.42f), new Vector3(0f, 0.45f, 0f), wood);
                Box(s, c, "Back", new Vector3(0.44f, 0.45f, 0.04f), new Vector3(0f, 0.7f, -0.2f), wood);
                foreach (var leg in new[] { new Vector3(-0.19f, 0.22f, -0.18f), new Vector3(0.19f, 0.22f, -0.18f), new Vector3(-0.19f, 0.22f, 0.18f), new Vector3(0.19f, 0.22f, 0.18f) })
                    Box(s, c, "Leg", new Vector3(0.04f, 0.45f, 0.04f), leg, wood);
            }
            var scrubs = st.Mat(VhWakingStyle.Scrubs);
            VhMeshKit.Show(b, "Scrubs (folded)", s.meshes.RoundedBox(new Vector3(0.34f, 0.07f, 0.28f), 0.03f, 4, 0.015f), scrubs, seat + new Vector3(0f, 0.04f, 0f), Quaternion.Euler(0f, -110f, 0f));
            VhMeshKit.Show(b, "Scrubs (a leg hanging)", s.meshes.RoundedBox(new Vector3(0.15f, 0.38f, 0.03f), 0.012f, 3), scrubs, seat + new Vector3(-0.17f, -0.14f, 0.12f), Quaternion.Euler(8f, -125f, 4f));
            if (PlaceModel(s, "rug", b, new Vector3(0.25f, 0.002f, -1.05f), 0f, 1.5f, true) == null)
                Box(s, b, "Rug", new Vector3(1.5f, 0.01f, 0.9f), new Vector3(0.25f, 0.005f, -1.05f), st.Mat(VhWakingStyle.Rug));
            var shelf = PlaceModel(s, "shelf", b, new Vector3(1.6f, 0f, 0.4f), -90f, 0.78f);
            float shelfTop = 0.78f;
            if (shelf != null) shelfTop = TopOf(shelf);
            else Box(s, b, "Bookcase", new Vector3(0.32f, 0.78f, 0.8f), new Vector3(1.62f, 0.39f, 0.4f), wood);
            if (PlaceModel(s, "books", b, new Vector3(1.6f, shelfTop, 0.3f), -90f, 0.2f) == null)
                for (int i = 0; i < 4; i++)
                    Box(s, b, "Book", new Vector3(0.16f, 0.22f, 0.035f), new Vector3(1.6f, shelfTop + 0.11f, 0.2f + i * 0.04f), st.Mat(i % 2 == 0 ? VhWakingStyle.BlanketDark : VhWakingStyle.Rug));
            if (PlaceModel(s, "plant", b, new Vector3(1.45f, 0f, 1.38f), 20f, 0.6f) == null)
            {
                VhMeshKit.Show(b, "Pot", s.meshes.Cylinder(0.11f, 0.14f, 0.22f, 16), st.Mat(VhWakingStyle.Rug), new Vector3(1.45f, 0f, 1.38f), Quaternion.identity);
                VhMeshKit.Show(b, "Plant", s.meshes.RoundedBox(new Vector3(0.32f, 0.3f, 0.3f), 0.14f, 5, 0.03f), st.Mat(VhWakingStyle.Plant), new Vector3(1.45f, 0.38f, 1.38f), Quaternion.identity);
            }

            BuildNadia(s, b);
            // The blanket last: over her real body when the model is there (so nothing of her pokes through it).
            BuildBlanket(s, b, blanketMat, Vector3.zero);
        }

        /// <summary>
        /// Shapes the blanket again over Nadia where she is now (the opening editor moved her), under its parent (the bed):
        /// <see cref="VhStage.blanketOrigin"/> is where that parent stands in the bedroom when nothing is moved.
        /// </summary>
        public static void ReshapeBlanket(VhStage s)
        {
            s.blanketDirty = false;
            if (s.blanket == null) return;
            var parent = s.blanket.transform.parent;
            var mat = s.blanketMaterial != null ? s.blanketMaterial : s.blanket.sharedMaterial;
            var old = s.blanket.gameObject;
            old.SetActive(false);
            VhUtil.Kill(old);
            string pose = s.Pose;
            BuildBlanket(s, parent, mat, s.blanketOrigin);
            if (s.nadia != null) s.nadia.SetMode(pose == "overhead" ? VhNadiaRig.Mode.Asleep : VhNadiaRig.Mode.FirstPerson);
        }

        static float SMax(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (a - b) / k);
            return Mathf.Lerp(b, a, h) + k * h * (1f - h);
        }

        /// <summary>An ellipsoid bump's height at (x, z): 0 outside it.</summary>
        static float Bump(float x, float z, float cx, float cz, float rx, float rz, float topY)
        {
            float dx = (x - cx) / rx, dz = (z - cz) / rz;
            float d2 = dx * dx + dz * dz;
            if (d2 >= 1f) return 0f;
            // From well below the blanket's own height (0.565), so where a bump begins there's no step: the blanket only
            // rises where the bump is above it (the old base of 0.55 left a 6 mm ridge round every bump: the "artifact").
            return 0.45f + (topY - 0.45f) * Mathf.Sqrt(1f - d2);
        }

        /// <summary>The blanket's height at a bedroom point: over the mattress, hanging down its sides and foot, and over Nadia curled on her side.</summary>
        public static float BlanketHeight(float wx, float wz)
        {
            float h = BedDrape(wx, wz);
            float body = 0f;
            // A woman lying on her side: the upper shoulder and hip stand about 0.35 m above the mattress.
            body = Mathf.Max(body, Bump(wx, wz, -0.36f, 0.86f, 0.21f, 0.24f, 0.92f));
            body = Mathf.Max(body, Bump(wx, wz, -0.35f, 0.52f, 0.19f, 0.27f, 0.85f));
            body = Mathf.Max(body, Bump(wx, wz, -0.31f, 0.15f, 0.22f, 0.25f, 0.91f));
            body = Mathf.Max(body, Bump(wx, wz, -0.18f, -0.2f, 0.19f, 0.24f, 0.78f));
            body = Mathf.Max(body, Bump(wx, wz, -0.1f, -0.46f, 0.16f, 0.2f, 0.69f));
            return SMax(h, body, 0.05f);
        }

        static void BuildBlanket(VhStage s, Transform b, Material mat, Vector3 origin)
        {
            Vector3 center = new Vector3(0.2f, 0f, 0.26f);
            const float W = 1.76f, D = 1.72f;
            const int N = 70;
            var body = BakeBody(s, b, center, W, D, N, origin);
            Func<float, float, float> height = body == null
                ? (x, z) => BlanketHeight(x + center.x, z + center.z)
                : (Func<float, float, float>)((x, z) => DrapeOver(body, x, z, W, D, N, center));
            var mesh = s.meshes.Heightfield(W, D, N, N, height, 1.6f);
            // Breathing: her shoulder and ribs rise a little under the blanket.
            VhMeshKit.AddBlendShape(mesh, "breath", v =>
            {
                float wx = v.x + center.x, wz = v.z + center.z;
                float k = 1f - ((wx + 0.35f) / 0.26f) * ((wx + 0.35f) / 0.26f) - ((wz - 0.66f) / 0.42f) * ((wz - 0.66f) / 0.42f);
                return Vector3.up * (0.014f * Mathf.Clamp01(k));
            });
            var go = new GameObject("Blanket");
            go.transform.SetParent(b, false);
            go.transform.localPosition = center - origin;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = ShadowCastingMode.On;
            s.blanket = smr;
        }

        /// <summary>The blanket over her real body: the bed's own drape, raised over the highest point of her model (in both
        /// poses she takes, first person and asleep) plus a little thickness, smoothed so it reads as cloth.</summary>
        static float DrapeOver(float[] body, float x, float z, float w, float d, int n, Vector3 center)
        {
            float bed = BedDrape(x + center.x, z + center.z);
            float fx = (x + w * 0.5f) / w * n, fz = (z + d * 0.5f) / d * n;
            int ix = Mathf.Clamp(Mathf.FloorToInt(fx), 0, n - 1), iz = Mathf.Clamp(Mathf.FloorToInt(fz), 0, n - 1);
            float tx = Mathf.Clamp01(fx - ix), tz = Mathf.Clamp01(fz - iz);
            int row = n + 1;
            float h = Mathf.Lerp(Mathf.Lerp(body[iz * row + ix], body[iz * row + ix + 1], tx), Mathf.Lerp(body[(iz + 1) * row + ix], body[(iz + 1) * row + ix + 1], tx), tz);
            return SMax(bed, h, 0.04f);
        }

        /// <summary>The bed under the blanket: over the mattress, hanging down its sides and foot, a few soft folds.</summary>
        static float BedDrape(float wx, float wz)
        {
            float outX = Mathf.Max(0f, Mathf.Max(-0.585f - wx, wx - 0.985f));
            float outZ = Mathf.Max(0f, -0.41f - wz);
            float drop = Mathf.Sqrt(outX * outX + outZ * outZ);
            float h = Mathf.Max(0.565f - drop * 3.2f, 0.3f);
            return h + (Mathf.Sin(wx * 9f + wz * 3f) * 0.006f + Mathf.Sin(wz * 13f + 1f) * 0.004f) * Mathf.Clamp01(1f - drop * 6f);
        }

        /// <summary>
        /// The highest point of Nadia's model over each point of the blanket's grid (both her poses: first person and asleep),
        /// her head and neck left out (they stay above the blanket's edge), widened and blurred a little, plus 3 cm of
        /// blanket. Null without the model (the stand-in's analytic shape is used).
        /// </summary>
        static float[] BakeBody(VhStage s, Transform b, Vector3 center, float w, float d, int n, Vector3 origin)
        {
            var model = s.nadia as VhModelNadia;
            if (model == null || model.model == null) return null;
            int row = n + 1;
            var h = new float[row * row];
            for (int i = 0; i < h.Length; i++) h[i] = 0f;
            var baked = new Mesh();
            var verts = new List<Vector3>();
            // Grid in the bedroom's own space (the heightfield's: centred on the blanket's centre).
            float x0 = center.x - w * 0.5f, z0 = center.z - d * 0.5f;
            const float headEdge = 1.08f;
            foreach (var mode in new[] { VhNadiaRig.Mode.Asleep, VhNadiaRig.Mode.FirstPerson })
            {
                model.SetMode(mode);
                model.SetHand(Vector3.zero, Quaternion.identity, 0f);
                foreach (var smr in model.model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.BakeMesh(baked, true);
                    baked.GetVertices(verts);
                    var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    for (int i = 0; i < verts.Count; i++)
                    {
                        Vector3 p = b.InverseTransformPoint(m.MultiplyPoint3x4(verts[i])) + origin;
                        if (p.z > headEdge) continue;
                        int ix = Mathf.RoundToInt((p.x - x0) / w * n), iz = Mathf.RoundToInt((p.z - z0) / d * n);
                        if (ix < 0 || iz < 0 || ix > n || iz > n) continue;
                        float y = p.y;
                        if (y > h[iz * row + ix]) h[iz * row + ix] = y;
                    }
                }
            }
            model.SetMode(VhNadiaRig.Mode.FirstPerson);
            VhUtil.Kill(baked);
            // Widen (the cloth spans over her), then soften twice, then add the blanket's thickness.
            var tmp = new float[h.Length];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int z = 0; z <= n; z++)
                    for (int x = 0; x <= n; x++)
                    {
                        float m = 0f;
                        for (int dz = -2; dz <= 2; dz++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int xx = x + dx, zz = z + dz;
                                if (xx < 0 || zz < 0 || xx > n || zz > n) continue;
                                float v = h[zz * row + xx];
                                if (pass == 0) { if (v > m) m = v; }
                                else m += v / 25f;
                            }
                        tmp[z * row + x] = m;
                    }
                Array.Copy(tmp, h, h.Length);
            }
            for (int i = 0; i < h.Length; i++) h[i] = h[i] > 0.05f ? h[i] + 0.03f : 0f;
            return h;
        }

        /// <summary>The phone's size (width, thickness, length).</summary>
        public static readonly Vector3 PhoneSize = new Vector3(0.071f, 0.0082f, 0.147f);

        /// <summary>How far the phone's bottom end reaches past the nightstand's edge, towards the bed.</summary>
        public const float PhoneOverhang = 0.04f;

        /// <summary>The marker of where the phone rests (the opening editor's "phone").</summary>
        public const string PhoneRestName = "Phone (where it rests)";

        static void BuildPhone(VhStage s, Transform b, Vector3 rest)
        {
            var phone = new GameObject("Phone").transform;
            phone.SetParent(b, false);
            Vector3 topDir = new Vector3(-1f, 0f, 0.12f).normalized;
            const float bump = 0.0014f; // she lies on her camera bump
            s.phoneRestPos = b.TransformPoint(rest + new Vector3(0f, bump, 0f));
            s.phoneRestRot = Quaternion.LookRotation(topDir, Vector3.up);
            // Where it rests (the opening editor moves this; the phone itself is the arm's while she holds it).
            var restMark = new GameObject(PhoneRestName).transform;
            restMark.SetParent(b, false);
            restMark.SetPositionAndRotation(s.phoneRestPos, s.phoneRestRot);
            phone.SetPositionAndRotation(s.phoneRestPos, s.phoneRestRot);
            s.phoneSize = PhoneSize;
            BuildDevice(s, phone, PhoneSize.x, PhoneSize.z, PhoneSize.y, 0.0095f, 0.0028f, 0.0036f, out var screenMat, out var screen);
            s.phoneScreenMat = screenMat;
            s.phoneScreen = screen;
            // The back: a camera bump with two lenses and a flash, and the buttons on its edges.
            var st = s.style;
            var frameMat = st.Mat(new Color(0.24f, 0.25f, 0.28f), null, 0.5f);
            VhMeshKit.Show(phone, "Camera bump", s.meshes.RoundedSlab(0.026f, 0.03f, bump, 0.0065f, 0.0005f, 4), frameMat,
                new Vector3(-PhoneSize.x * 0.5f + 0.018f, 0f, PhoneSize.z * 0.5f - 0.02f), Quaternion.Euler(180f, 0f, 0f));
            var lens = st.Mat(new Color(0.05f, 0.06f, 0.08f), null, 0f);
            foreach (var z in new[] { 0.0085f, -0.0045f })
                VhMeshKit.Show(phone, "Lens", s.meshes.Disc(0.0042f, 16), lens, new Vector3(-PhoneSize.x * 0.5f + 0.0145f, -bump - 0.0002f, PhoneSize.z * 0.5f - 0.02f + z), Quaternion.Euler(180f, 0f, 0f), false);
            VhMeshKit.Show(phone, "Flash", s.meshes.Disc(0.0018f, 10), st.Mat(new Color(0.85f, 0.82f, 0.7f), null, 0f), new Vector3(-PhoneSize.x * 0.5f + 0.0235f, -bump - 0.0002f, PhoneSize.z * 0.5f - 0.02f), Quaternion.Euler(180f, 0f, 0f), false);
            Box(s, phone, "Power button", new Vector3(0.0012f, 0.0034f, 0.016f), new Vector3(PhoneSize.x * 0.5f + 0.0003f, PhoneSize.y * 0.5f, 0.028f), frameMat, 1f, null, false);
            Box(s, phone, "Volume up", new Vector3(0.0012f, 0.0034f, 0.011f), new Vector3(-PhoneSize.x * 0.5f - 0.0003f, PhoneSize.y * 0.5f, 0.036f), frameMat, 1f, null, false);
            Box(s, phone, "Volume down", new Vector3(0.0012f, 0.0034f, 0.011f), new Vector3(-PhoneSize.x * 0.5f - 0.0003f, PhoneSize.y * 0.5f, 0.021f), frameMat, 1f, null, false);
            var glowGo = new GameObject("Screen glow (light)");
            glowGo.transform.SetParent(phone, false);
            glowGo.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 0.9f;
            glow.color = new Color(0.72f, 0.8f, 1f);
            glow.intensity = s.phoneGlowIntensity;
            glow.shadows = LightShadows.None;
            s.phoneGlow = glow;
            s.phone = phone;
        }

        /// <summary>
        /// A phone or a tablet, lying on its back at the parent's origin (+Y the screen, +Z its top): a rounded aluminium-dark
        /// frame with softened edges, a black glass front, and the screen set into the glass inside a thin bezel (rounded
        /// corners, its own emissive material: the UI's texture or a picture).
        /// </summary>
        static void BuildDevice(VhStage s, Transform parent, float width, float length, float thickness, float corner, float bezel, float bezelChin,
            out Material screenMat, out MeshRenderer screen, float y0 = 0f)
        {
            var st = s.style;
            VhMeshKit.Show(parent, "Body", s.meshes.RoundedSlab(width, length, thickness, corner, thickness * 0.3f, 6), st.Mat(new Color(0.20f, 0.21f, 0.24f)), new Vector3(0f, y0, 0f), Quaternion.identity);
            VhMeshKit.Show(parent, "Glass", s.meshes.RoundedRect(width - 0.0016f, length - 0.0016f, Mathf.Max(0.001f, corner - 0.0008f), 6), st.Mat(new Color(0.025f, 0.028f, 0.034f), null, 0f),
                new Vector3(0f, y0 + thickness + 0.0002f, 0f), Quaternion.identity, false);
            screenMat = ScreenMat(s, "VH " + parent.name + " screen");
            float sw = width - 2f * bezel, sl = length - bezel - bezelChin;
            screen = VhMeshKit.Show(parent, "Screen", s.meshes.RoundedRect(sw, sl, Mathf.Max(0.001f, corner - bezel), 6), screenMat,
                new Vector3(0f, y0 + thickness + 0.0005f, (bezelChin - bezel) * 0.5f), Quaternion.identity, false);
        }

        /// <summary>A framed photo standing on the nightstand: the frame leaning back on its strut, turned towards her pillow.</summary>
        static void BuildFrame(VhStage s, Transform b, Vector3 foot)
        {
            var st = s.style;
            Vector3 toPillow = b.TransformPoint(Eyes) - b.TransformPoint(foot);
            toPillow.y = 0f;
            // Local +X (the photo's side) towards her: LookRotation's right is up x forward, so forward = toPillow x up.
            Quaternion facing = Quaternion.LookRotation(Vector3.Cross(toPillow.normalized, Vector3.up), Vector3.up);
            const float lean = 12f, h = 0.17f, wdt = 0.135f, depth = 0.016f;
            var frame = Pivot(b, "Framed photo", foot, facing * Quaternion.Euler(0f, 0f, lean));
            var wood = st.Mat(VhWakingStyle.WoodDark);
            // The frame stands on its bottom edge: lift it by the lean so the front lower edge sits on the wood.
            float lift = depth * 0.5f * Mathf.Sin(lean * Mathf.Deg2Rad);
            Box(s, frame, "Frame", new Vector3(depth, h, wdt), new Vector3(0f, h * 0.5f + lift, 0f), wood);
            var photo = Art(s, "photo_frame", () => s.boot != null && s.boot.Art("phone_wallpaper") != null ? s.boot.Art("phone_wallpaper") : s.textures.FramedPhoto());
            VhMeshKit.Show(frame, "Photo", s.meshes.Quad(0.11f, 0.145f), Crop(st.Mat(Color.white, photo, 0f), photo, 0.11f / 0.145f),
                new Vector3(depth * 0.5f + 0.0006f, h * 0.5f + lift, 0f), Quaternion.Euler(0f, -90f, 0f), false);
            // The strut from the back of the frame down to the wood behind it.
            Vector3 a = frame.TransformPoint(new Vector3(-depth * 0.5f, h * 0.55f, 0f));
            Vector3 back = -(frame.rotation * Vector3.right);
            back.y = 0f;
            back.Normalize();
            Vector3 c = new Vector3(a.x, b.TransformPoint(foot).y, a.z) + back * 0.055f;
            var strut = new GameObject("Strut").transform;
            strut.SetParent(frame, true);
            strut.SetPositionAndRotation((a + c) * 0.5f, Quaternion.LookRotation(c - a, back));
            VhMeshKit.Show(strut, "Strut", s.meshes.Box(new Vector3(0.028f, 0.004f, Vector3.Distance(a, c)), 1f), wood, Vector3.zero, Quaternion.identity);
        }

        static void BuildNadia(VhStage s, Transform b)
        {
            s.shoulder = b.TransformPoint(Shoulder);
            if (s.boot != null && s.boot.nadiaModel != null && BuildModelNadia(s, b)) return;
            BuildStandInNadia(s, b);
        }

        static bool BuildModelNadia(VhStage s, Transform b)
        {
            var go = UnityEngine.Object.Instantiate(s.boot.nadiaModel, b);
            go.name = "Nadia (model)";
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var mat = s.style.Mat(Color.white, s.boot.nadiaTexture, 0.7f);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                    // Outside play mode (the lighting captures, the EditMode tests) nothing skins her between posing and
                    // rendering: re-skin on every render so a still shows the pose (her arm on the phone), not a stale one.
                    if (!Application.isPlaying) smr.forceMatrixRecalculationPerRender = true;
                }
            }
            var rig = go.AddComponent<VhModelNadia>();
            if (!rig.Bind(go.transform))
            {
                Debug.Log("JAMKIT_VH Nadia's model has no Mixamo arm bones: the stand-in arm is used.");
                VhUtil.Kill(go);
                return false;
            }
            float h = rig.Height();
            if (h > 0.2f && h < 1.3f) go.transform.localScale *= 1.62f / h;
            rig.eyes = b.TransformPoint(Eyes);
            rig.facingFirstPerson = Vector3.left;
            rig.facingAsleep = Vector3.right;
            rig.bodyUp = Vector3.forward;
            rig.pole = s.shoulder + new Vector3(0.05f, -0.3f, -0.25f);
            rig.SetMode(VhNadiaRig.Mode.FirstPerson);
            s.shoulder = rig.rArm.position;
            rig.pole = s.shoulder + new Vector3(0.05f, -0.3f, -0.25f);
            s.nadia = rig;
            return true;
        }

        static void BuildStandInNadia(VhStage s, Transform b)
        {
            var go = new GameObject("Nadia (stand-in)");
            go.transform.SetParent(b, false);
            var rig = go.AddComponent<VhStandInNadia>();
            var st = s.style;
            var mk = s.meshes;
            Material sleeve = st.Mat(VhWakingStyle.Thermal), skin = st.Mat(VhWakingStyle.Skin), hair = st.Mat(VhWakingStyle.Hair);

            rig.upperArm = Pivot(go.transform, "Upper arm", Vector3.zero, Quaternion.identity);
            VhMeshKit.Show(rig.upperArm, "Sleeve", mk.Cylinder(0.048f, 0.043f, 1f, 14), sleeve, Vector3.zero, Quaternion.identity);
            rig.foreArm = Pivot(go.transform, "Forearm", Vector3.zero, Quaternion.identity);
            VhMeshKit.Show(rig.foreArm, "Sleeve", mk.Cylinder(0.043f, 0.037f, 0.8f, 14), sleeve, Vector3.zero, Quaternion.identity);
            VhMeshKit.Show(rig.foreArm, "Wrist", mk.Cylinder(0.031f, 0.028f, 0.24f, 12), skin, new Vector3(0f, 0.78f, 0f), Quaternion.identity);
            rig.hand = Pivot(go.transform, "Hand", Vector3.zero, Quaternion.identity);
            VhMeshKit.Show(rig.hand, "Palm", mk.RoundedBox(new Vector3(0.078f, 0.028f, 0.088f), 0.012f, 4), skin, new Vector3(0f, 0f, 0.045f), Quaternion.identity);
            VhMeshKit.Show(rig.hand, "Fingers", mk.RoundedBox(new Vector3(0.074f, 0.022f, 0.075f), 0.01f, 4), skin, new Vector3(0f, -0.005f, 0.115f), Quaternion.Euler(14f, 0f, 0f));
            rig.thumb = Pivot(rig.hand, "Thumb", new Vector3(-0.04f, -0.006f, 0.022f), Quaternion.Euler(0f, -35f, 0f));
            VhMeshKit.Show(rig.thumb, "Thumb", mk.RoundedBox(new Vector3(0.024f, 0.022f, 0.066f), 0.01f, 3), skin, new Vector3(0f, 0f, 0.032f), Quaternion.identity);

            // Her hair on the pillow (the overhead shot): she's turned away, the bun towards the nightstand.
            Vector3 head = b.TransformPoint(Eyes + new Vector3(0.08f, -0.04f, 0.03f));
            rig.hair = Pivot(go.transform, "Hair", go.transform.InverseTransformPoint(head), Quaternion.identity);
            VhMeshKit.Show(rig.hair, "Hair", mk.RoundedBox(new Vector3(0.18f, 0.16f, 0.21f), 0.075f, 6, 0.01f), hair, Vector3.zero, Quaternion.identity);
            VhMeshKit.Show(rig.hair, "Bun", mk.RoundedBox(new Vector3(0.085f, 0.075f, 0.085f), 0.035f, 4), hair, new Vector3(-0.1f, 0.02f, -0.02f), Quaternion.identity);
            VhMeshKit.Show(rig.hair, "Neck", mk.Cylinder(0.05f, 0.05f, 0.1f, 10), skin, new Vector3(0.0f, -0.05f, -0.1f), Quaternion.Euler(90f, 0f, 0f));
            var strands = new List<Vector3>();
            for (int k = 0; k < 4; k++)
            {
                strands.Clear();
                float a = -0.6f + k * 0.35f;
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    strands.Add(new Vector3(-0.05f - t * 0.14f * Mathf.Cos(a), 0.05f - t * 0.11f, 0.06f + t * 0.16f * Mathf.Sin(a + 1.2f)));
                }
                VhMeshKit.Show(rig.hair, "Strand", mk.Tube(strands, 0.007f, 5, 0.002f), hair, Vector3.zero, Quaternion.identity, false);
            }

            rig.shoulder = s.shoulder;
            rig.restWrist = b.TransformPoint(new Vector3(-0.48f, 0.7f, 0.86f));
            rig.upperLength = 0.3f;
            rig.lowerLength = 0.28f;
            rig.restPalm = Quaternion.LookRotation(Vector3.left, Vector3.up);
            rig.pole = s.shoulder + new Vector3(0.05f, -0.3f, -0.25f);
            rig.SetMode(VhNadiaRig.Mode.FirstPerson);
            rig.SetHand(rig.restWrist, rig.restPalm, 0f);
            s.nadia = rig;
        }

        // ---------- The title table ----------

        static VhTitlePiece Piece(VhStage s, Transform parent, string id, VhTitleAction action, Vector3 pos, Quaternion rot, string group = "")
        {
            var t = Pivot(parent, "Piece: " + id, pos, rot);
            var p = t.gameObject.AddComponent<VhTitlePiece>();
            p.id = id;
            p.action = action;
            p.group = group;
            s.table.pieces.Add(p);
            return p;
        }

        static void FinishPiece(VhTitlePiece p, bool colliders)
        {
            p.renderers = p.GetComponentsInChildren<Renderer>(true);
            if (!colliders) return;
            foreach (var r in p.renderers)
                if (r.GetComponent<Collider>() == null && r.GetComponent<MeshFilter>() != null) r.gameObject.AddComponent<BoxCollider>();
        }

        static void BuildTable(VhStage s)
        {
            var t = new GameObject("Title table").transform;
            t.SetParent(s.transform, false);
            t.localPosition = VhStage.TableOrigin;
            s.tableRoot = t;
            s.table = s.gameObject.AddComponent<VhTitleTable>();
            s.table.cam = s.cam;
            var st = s.style;
            var tx = s.textures;
            var woodTex = Art(s, "tex_table_wood", () => tx.Wood(VhWakingStyle.WoodPale, 7));
            var plaster = Art(s, "tex_bedroom_wall", () => tx.Plaster());

            // The room (the floor and the wall behind) and the table: two pieces, the last two to come up.
            var room = Piece(s, t, "room", VhTitleAction.None, Vector3.zero, Quaternion.identity);
            Box(s, room.transform, "Floor", new Vector3(8f, 0.02f, 8f), new Vector3(0f, -0.01f, 0f), st.Mat(VhWakingStyle.Floor * 0.8f), 1f, null, false);
            Box(s, room.transform, "Wall", new Vector3(8f, 3f, 0.1f), new Vector3(0f, 1.5f, 1.9f), st.Mat(VhWakingStyle.Wall * 0.85f, plaster), 0.6f, null, false);
            FinishPiece(room, false);
            var tablePiece = Piece(s, t, "table", VhTitleAction.None, Vector3.zero, Quaternion.identity);
            VhMeshKit.Show(tablePiece.transform, "Table top", s.meshes.RoundedBox(new Vector3(1.5f, 0.045f, 1.1f), 0.015f, 3), st.Mat(VhWakingStyle.WoodPale, woodTex), new Vector3(0f, 0.7275f, 0.15f), Quaternion.identity);
            FinishPiece(tablePiece, false);

            // Laid out to the creator's sketch (art/review/2026-10-12/title-screen/creator-sketch.png), seen from straight
            // above (see TableCamera): the tablet large and turned, upper left, running off the left edge; the quit piece
            // upper right (the mug's handle cut by the right edge); the notepad at the bottom, cut by it; the badge bottom
            // right with its lanyard. The frame shows the table from x -0.31 to 0.31 m and z -0.175 to 0.175 m.

            // The tablet (Play), lying flat, screen up, turned 11.5 degrees (its right side further away).
            const float tabW = 0.46f, tabH = 0.30f;
            var tablet = Piece(s, t, "tablet", VhTitleAction.Play, new Vector3(-0.119f, 0.75f, 0.063f), Quaternion.Euler(0f, -11.5f, 0f));
            tablet.lift = 0.006f;
            tablet.tilt = 0.8f;
            var device = Pivot(tablet.transform, "Tablet", Vector3.zero, Quaternion.identity);
            BuildDevice(s, device, tabW, tabH, 0.008f, 0.018f, 0.01f, 0.015f, out var tabletScreen, out _, 0f);
            s.tabletScreenMat = tabletScreen;
            VhMeshKit.Show(device, "Front camera", s.meshes.Disc(0.0018f, 10), st.Mat(new Color(0.08f, 0.09f, 0.11f), null, 0f), new Vector3(0f, 0.0087f, tabH * 0.5f - 0.005f), Quaternion.identity, false);
            FinishPiece(tablet, true);

            // The quit piece, upper right: one setting picks it (QuitPiece); on the Despair route it's the pill bottle.
            var quit = BuildQuitPiece(s, t, QuitVariant());
            if (!VhTitleTable.QuitAvailable) quit.action = VhTitleAction.None;

            // The notepad (Credits: written at the top of its page) at the bottom, cut by the bottom edge, the pen on it.
            var pad = Piece(s, t, "notepad", VhTitleAction.Credits, new Vector3(-0.0125f, 0.75f, -0.205f), Quaternion.Euler(0f, 12f, 0f));
            VhMeshKit.Show(pad.transform, "Pad", s.meshes.RoundedBox(new Vector3(0.15f, 0.014f, 0.21f), 0.003f, 3), st.Mat(VhWakingStyle.Paper), new Vector3(0f, 0.007f, 0f), Quaternion.identity);
            var pageArt = s.boot != null ? s.boot.Art("notepad_page") : null;
            if (pageArt == null && !s.missingArt.Contains("notepad_page")) s.missingArt.Add("notepad_page");
            var page = tx.NotepadCredits(pageArt);
            VhMeshKit.Show(pad.transform, "Page", s.meshes.Quad(0.144f, 0.2f), Crop(st.Mat(Color.white, page, 0f), page, 0.144f / 0.2f), new Vector3(0f, 0.0143f, -0.003f), Quaternion.Euler(90f, 0f, 0f), false);
            VhMeshKit.Show(pad.transform, "Binding", s.meshes.RoundedBox(new Vector3(0.152f, 0.012f, 0.02f), 0.004f, 3), st.Mat(new Color(0.22f, 0.24f, 0.28f)), new Vector3(0f, 0.013f, 0.1f), Quaternion.identity);
            FinishPiece(pad, true);
            // The pen along the pad's right side, clear of "Credits".
            var pen = Piece(s, t, "pen", VhTitleAction.Credits, new Vector3(0.062f, 0.769f, -0.158f), Quaternion.Euler(0f, 100f, 90f), "notepad");
            var navy = st.Mat(new Color(0.18f, 0.21f, 0.32f));
            VhMeshKit.Show(pen.transform, "Barrel", s.meshes.Cylinder(0.0045f, 0.0045f, 0.12f, 12), navy, new Vector3(0f, -0.06f, 0f), Quaternion.identity);
            VhMeshKit.Show(pen.transform, "Tip", s.meshes.Cylinder(0.0045f, 0.0008f, 0.018f, 12), st.Mat(VhWakingStyle.Metal), new Vector3(0f, 0.06f, 0f), Quaternion.identity);
            Box(s, pen.transform, "Clip", new Vector3(0.002f, 0.04f, 0.0025f), new Vector3(0.005f, -0.04f, 0f), st.Mat(VhWakingStyle.Metal));
            pen.lift = 0.012f;
            FinishPiece(pen, true);

            // Nadia's ID badge on its lanyard (Settings: a gear and the word on the card), bottom right, cut by the bottom
            // edge; a little larger than life so "Settings" reads from above. The lanyard arches up from the clip and curls left.
            var badge = Piece(s, t, "badge", VhTitleAction.Settings, new Vector3(0.225f, 0.75f, -0.115f), Quaternion.Euler(0f, -6f, 0f));
            badge.transform.localScale = Vector3.one * 1.4f;
            VhMeshKit.Show(badge.transform, "Card", s.meshes.RoundedBox(new Vector3(0.056f, 0.002f, 0.088f), 0.0015f, 2), st.Mat(new Color(0.9f, 0.9f, 0.88f)), new Vector3(0f, 0.001f, 0f), Quaternion.identity);
            var photo = s.boot != null ? s.boot.Art("badge_photo") : null;
            var faceArt = s.boot != null ? s.boot.Art("badge_face") : null;
            if (faceArt == null && !s.missingArt.Contains("badge_face")) s.missingArt.Add("badge_face");
            var face = tx.BadgeSettings(faceArt, photo);
            // The art shows the card standing in a 16:9 frame: crop to the card (about 90% of the height).
            bool drawn = faceArt == null || !faceArt.isReadable;
            VhMeshKit.Show(badge.transform, "Face", s.meshes.Quad(0.053f, 0.085f), Crop(st.Mat(Color.white, face, 0f), face, 0.053f / 0.085f, drawn ? 1f : 1.1f), new Vector3(0f, 0.00215f, 0f), Quaternion.Euler(90f, 0f, 0f), false);
            Box(s, badge.transform, "Clip", new Vector3(0.016f, 0.004f, 0.012f), new Vector3(0f, 0.003f, 0.05f), st.Mat(VhWakingStyle.Metal));
            // The lanyard: from the clip, a smooth loop lying to the card's left (towards the middle of the table).
            var lanyard = new List<Vector3>();
            Vector3 loopCentre = new Vector3(-0.04f, 0.003f, 0.072f);
            float start = Mathf.Atan2(0.056f - loopCentre.z, 0f - loopCentre.x);
            for (int i = 0; i <= 36; i++)
            {
                float ang = start + i / 36f * Mathf.PI * 1.55f;
                lanyard.Add(loopCentre + new Vector3(Mathf.Cos(ang) * 0.04f, 0f, Mathf.Sin(ang) * 0.03f));
            }
            VhMeshKit.Show(badge.transform, "Lanyard", s.meshes.Tube(lanyard, 0.0036f, 10), st.Mat(VhWakingStyle.Lanyard, null, 0.6f), Vector3.zero, Quaternion.identity);
            FinishPiece(badge, true);

            var keyGo = new GameObject(TableLampName);
            keyGo.transform.SetParent(t, false);
            keyGo.transform.rotation = Quaternion.Euler(48f, 40f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.95f, 0.88f);
            key.intensity = 1.1f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.7f;
            s.tableKey = key;
        }

        // ---------- Camera poses ----------

        /// <summary>The title's camera (table space): straight down over the table, 1 m above it with a narrow lens (the
        /// sketch is nearly flat: from 0.37 m the mug's rim swelled out of the frame), the far side of the table at the top;
        /// the frame shows the table from x -0.31 to 0.31 m and z -0.17 to 0.17 m.</summary>
        static readonly Vector3 TableCamera = new Vector3(0f, 1.75f, 0f), TableLook = new Vector3(0f, 0.75f, 0f);
        const float TableFov = 19.6f;

        /// <summary>The light over the title table (the editor's "title-lamp"; the lighting places it).</summary>
        public const string TableLampName = "Lamp over the table (light)";

        /// <summary>
        /// The title's quit piece on the normal route (the creator hasn't picked its thematic object yet: change this one
        /// setting when they do, and add the object to <see cref="BuildQuitPiece"/>). "mug": the coffee mug with "Quit" in
        /// its milk. On the Despair route it's always the pill bottle labelled "Give Up".
        /// </summary>
        public const string QuitPiece = "mug";

        /// <summary>Tests and the title capture: force the quit piece's variant ("normal", "despair"); null = the save decides.</summary>
        public static string QuitVariantOverride;

        /// <summary>"despair" when the save has the Despair route (she went on into the rain without the umbrella), else "normal".</summary>
        public static string QuitVariant()
        {
            if (!string.IsNullOrEmpty(QuitVariantOverride)) return QuitVariantOverride;
            bool despair = GameRoot.Exists && GameRoot.Save != null && GameRoot.Save.Data != null && GameRoot.Save.Data.HasFlag(JamKit.DreamEscape.DreamRain.DespairFlag);
            return despair ? "despair" : "normal";
        }

        /// <summary>
        /// The quit piece (piece id "mug": the quit slot, whatever object fills it; the opening's cues and the editor's
        /// "title-mug" use it), upper right: on the Despair route a pill bottle on its side labelled "Give Up", else
        /// <see cref="QuitPiece"/>.
        /// </summary>
        static VhTitlePiece BuildQuitPiece(VhStage s, Transform t, string variant)
        {
            var st = s.style;
            if (variant == "despair")
            {
                // An amber pharmacy bottle lying on its side, its white cap to the right, its label up.
                var bottle = Piece(s, t, "mug", VhTitleAction.Quit, new Vector3(0.235f, 0.75f, 0.07f), Quaternion.Euler(0f, -18f, 0f));
                const float r = 0.019f, len = 0.072f;
                var amber = st.Mat(new Color(0.78f, 0.42f, 0.12f), null, 0.85f);
                VhMeshKit.Show(bottle.transform, "Bottle", s.meshes.Cylinder(r, r, len, 28, true, true), amber, new Vector3(-len * 0.5f, r, 0f), Quaternion.Euler(0f, 0f, -90f));
                var cap = st.Mat(new Color(0.93f, 0.93f, 0.92f));
                VhMeshKit.Show(bottle.transform, "Cap", s.meshes.Cylinder(r * 1.12f, r * 1.12f, 0.02f, 28, true, true), cap, new Vector3(len * 0.5f, r, 0f), Quaternion.Euler(0f, 0f, -90f));
                var label = s.textures.PillLabel();
                VhMeshKit.Show(bottle.transform, "Label", s.meshes.Quad(0.056f, 0.028f), st.Mat(Color.white, label, 0f), new Vector3(-0.002f, r * 2f + 0.0006f, 0f), Quaternion.Euler(90f, 0f, 0f), false);
                FinishPiece(bottle, true);
                return bottle;
            }
            switch (QuitPiece)
            {
                default:
                    return BuildMug(s, t, new Vector3(0.215f, 0.75f, 0.0665f), 0f);
            }
        }

        /// <summary>The mug (Quit): half drunk, "Quit" in the milk, read straight from above whatever its turn.</summary>
        static VhTitlePiece BuildMug(VhStage s, Transform t, Vector3 at, float yaw)
        {
            var st = s.style;
            var tx = s.textures;
            var mug = Piece(s, t, "mug", VhTitleAction.Quit, at, Quaternion.Euler(0f, yaw, 0f));
            var mugMat = st.Mat(VhWakingStyle.Mug);
            VhMeshKit.Show(mug.transform, "Mug", s.meshes.Cylinder(0.041f, 0.044f, 0.096f, 32, false, true), mugMat, Vector3.zero, Quaternion.identity);
            VhMeshKit.Show(mug.transform, "Inside", s.meshes.Cylinder(0.038f, 0.0405f, 0.09f, 32, false, false, true), st.Mat(VhWakingStyle.Mug * 0.92f, null, 0f), new Vector3(0f, 0.006f, 0f), Quaternion.identity, false);
            var ring = new List<Vector3>();
            for (int i = 0; i <= 40; i++)
            {
                float ang = i / 40f * Mathf.PI * 2f;
                ring.Add(new Vector3(Mathf.Cos(ang) * 0.0422f, 0.096f, Mathf.Sin(ang) * 0.0422f));
            }
            VhMeshKit.Show(mug.transform, "Rim", s.meshes.Tube(ring, 0.0022f, 6), mugMat, Vector3.zero, Quaternion.identity, false);
            var handle = new List<Vector3>();
            for (int i = 0; i <= 14; i++)
            {
                float ang = Mathf.Lerp(-75f, 75f, i / 14f) * Mathf.Deg2Rad;
                handle.Add(new Vector3(0.04f + Mathf.Cos(ang) * 0.027f, 0.05f + Mathf.Sin(ang) * 0.03f, 0f));
            }
            VhMeshKit.Show(mug.transform, "Handle", s.meshes.Tube(handle, 0.0065f, 8), mugMat, Vector3.zero, Quaternion.identity);
            var coffee = Art(s, "coffee_quit", () => tx.CoffeeQuit(s.boot != null ? s.boot.Art("coffee_swirl") : null));
            // Counter-turn the coffee so "Quit" reads straight to the camera.
            VhMeshKit.Show(mug.transform, "Coffee", s.meshes.Disc(0.0395f, 40), Crop(st.Mat(Color.white, coffee, 0f), coffee, 1f, 1.1f), new Vector3(0f, 0.081f, 0f), Quaternion.Euler(0f, -yaw, 0f), false);
            FinishPiece(mug, true);
            return mug;
        }

        static void Poses(VhStage s)
        {
            var b = s.bedroom;
            Vector3 eyes = b.TransformPoint(Eyes);
            s.AddPose("pillow", eyes + new Vector3(0f, 0.02f, 0f), b.TransformPoint(new Vector3(-0.7f, 2.6f, 0.7f)), Vector3.forward, 0f, 62f);
            s.AddPose("nightstand", eyes + new Vector3(-0.02f, -0.02f, 0f), b.TransformPoint(new Vector3(-0.86f, 0.6f, 1.26f)), Vector3.up, -16f, 62f);
            s.AddPose("away", eyes + new Vector3(0.04f, -0.03f, -0.02f), b.TransformPoint(new Vector3(1.2f, 0.7f, 0.9f)), Vector3.up, 24f, 62f);
            s.AddPose("overhead", b.TransformPoint(new Vector3(-0.52f, 1.62f, 0.98f)), b.TransformPoint(new Vector3(-0.6f, 0.55f, 1.24f)), Vector3.forward, 0f, 50f);
            var t = s.tableRoot;
            s.AddPose("table", t.TransformPoint(TableCamera), t.TransformPoint(TableLook), t.forward, 0f, TableFov);
        }

        // ---------- Sound sources ----------

        static AudioSource Source(VhStage s, string name, AudioClip clip, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(s.transform, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.ignoreListenerPause = true;
            a.spatialBlend = 0f;
            a.loop = loop;
            a.clip = clip;
            a.mute = GameRoot.Exists && GameRoot.Audio.Muted;
            return a;
        }

        static void Sounds(VhStage s)
        {
            s.ring = Source(s, "Phone ring", ProceduralAudio.Get("vh_phone_ring"), true);
            s.vibrate = Source(s, "Phone buzz", ProceduralAudio.Get("vh_vibrate"), true);
            var boot = s.boot;
            s.voiceClean = Source(s, "Voicemail (held close)", boot != null ? boot.voicemail : null, false);
            s.voicePhone = Source(s, "Voicemail (phone speaker)", boot != null ? boot.voicemail : null, false);
            var sp = boot != null ? boot.speaker : new VhPhoneSpeaker();
            var hp = s.voicePhone.gameObject.AddComponent<AudioHighPassFilter>();
            hp.cutoffFrequency = sp.highPass;
            hp.highpassResonanceQ = 1.4f;
            var lp = s.voicePhone.gameObject.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = sp.lowPass;
            lp.lowpassResonanceQ = 1.2f;
            var dist = s.voicePhone.gameObject.AddComponent<AudioDistortionFilter>();
            dist.distortionLevel = sp.distortion;
            s.news = Source(s, "News (tablet)", boot != null ? boot.newsAnchor : null, false);
            var breath = Source(s, "Breathing", ProceduralAudio.Get("vh_breath"), true);
            s.SetBreathSource(breath);
        }

        // ---------- Screens ----------

        static void Screens(VhStage s)
        {
            var boot = s.boot;
            var style = boot != null ? boot.style : null;
            s.phonePanel = new VhScreenPanel(s.transform, "Phone screen", VhPhoneScreen.Width, VhPhoneScreen.Height, style);
            var wallpaper = Art(s, "phone_wallpaper", () => s.textures.Wallpaper());
            s.phoneUi = new VhPhoneScreen(s.phonePanel.Root, wallpaper, boot != null ? boot.ClipLength("vo_elias_voicemail") : 0f);
            SetScreenPicture(s.phoneScreenMat, s.phonePanel.Texture != null ? s.phonePanel.Texture : wallpaper, 1f);

            var frames = new List<Texture2D>();
            if (boot != null) boot.ArtList("news_", frames);
            if (frames.Count == 0)
            {
                if (!s.missingArt.Contains("news_*")) s.missingArt.Add("news_*");
                frames.Add(s.textures.NewsStill(1));
                frames.Add(s.textures.NewsStill(2));
                frames.Add(s.textures.NewsStill(3));
            }
            s.tabletPanel = new VhScreenPanel(s.transform, "Tablet screen", VhTabletScreen.Width, VhTabletScreen.Height, style);
            string headline = SliceContent.Headline(boot != null ? boot.headline : "");
            s.tabletUi = new VhTabletScreen(s.tabletPanel.Root, frames, s.textures.Blob(), headline, boot != null ? boot.headlineSpeed : 90f);
            SetScreenPicture(s.tabletScreenMat, s.tabletPanel.Texture != null ? s.tabletPanel.Texture : frames[0], 1f);
        }
    }
}
