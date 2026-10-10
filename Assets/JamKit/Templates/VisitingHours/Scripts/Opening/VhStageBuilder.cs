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
            VhLighting.Apply(s);
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
            BuildBlanket(s, b, st.Mat(VhWakingStyle.Blanket, knit, -1f));

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
            if (PlaceModel(s, "lamp", b, new Vector3(-1.02f, top, 1.5f), 0f, 0.42f) == null)
            {
                VhMeshKit.Show(b, "Lamp base", s.meshes.Cylinder(0.06f, 0.05f, 0.03f, 16), st.Mat(VhWakingStyle.Metal), new Vector3(-1.02f, top, 1.5f), Quaternion.identity);
                VhMeshKit.Show(b, "Lamp stem", s.meshes.Cylinder(0.012f, 0.012f, 0.24f, 8), st.Mat(VhWakingStyle.Metal), new Vector3(-1.02f, top + 0.03f, 1.5f), Quaternion.identity);
                VhMeshKit.Show(b, "Lamp shade", s.meshes.Cylinder(0.11f, 0.075f, 0.15f, 20), st.Mat(VhWakingStyle.Lampshade), new Vector3(-1.02f, top + 0.24f, 1.5f), Quaternion.identity);
            }
            BuildPhone(s, b, new Vector3(-0.74f, top, 1.27f));
            var frame = Pivot(b, "Framed photo", new Vector3(-1.03f, top, 1.15f), Quaternion.Euler(0f, 0f, 9f));
            Box(s, frame, "Frame", new Vector3(0.016f, 0.17f, 0.135f), new Vector3(0f, 0.085f, 0f), st.Mat(VhWakingStyle.WoodDark));
            var photo = Art(s, "photo_frame", () => s.boot != null && s.boot.Art("phone_wallpaper") != null ? s.boot.Art("phone_wallpaper") : s.textures.FramedPhoto());
            VhMeshKit.Show(frame, "Photo", s.meshes.Quad(0.11f, 0.145f), Crop(st.Mat(Color.white, photo, 0f), photo, 0.11f / 0.145f), new Vector3(0.0085f, 0.085f, 0f), Quaternion.Euler(0f, -90f, 0f), false);

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
            return 0.55f + (topY - 0.55f) * Mathf.Sqrt(1f - d2);
        }

        /// <summary>The blanket's height at a bedroom point: over the mattress, hanging down its sides and foot, and over Nadia curled on her side.</summary>
        public static float BlanketHeight(float wx, float wz)
        {
            float outX = Mathf.Max(0f, Mathf.Max(-0.585f - wx, wx - 0.985f));
            float outZ = Mathf.Max(0f, -0.41f - wz);
            float drop = Mathf.Sqrt(outX * outX + outZ * outZ);
            float h = Mathf.Max(0.565f - drop * 3.2f, 0.3f);
            h += (Mathf.Sin(wx * 9f + wz * 3f) * 0.006f + Mathf.Sin(wz * 13f + 1f) * 0.004f) * Mathf.Clamp01(1f - drop * 6f);
            float body = 0f;
            // A woman lying on her side: the upper shoulder and hip stand about 0.35 m above the mattress.
            body = Mathf.Max(body, Bump(wx, wz, -0.36f, 0.86f, 0.21f, 0.24f, 0.92f));
            body = Mathf.Max(body, Bump(wx, wz, -0.35f, 0.52f, 0.19f, 0.27f, 0.85f));
            body = Mathf.Max(body, Bump(wx, wz, -0.31f, 0.15f, 0.22f, 0.25f, 0.91f));
            body = Mathf.Max(body, Bump(wx, wz, -0.18f, -0.2f, 0.19f, 0.24f, 0.78f));
            body = Mathf.Max(body, Bump(wx, wz, -0.1f, -0.46f, 0.16f, 0.2f, 0.69f));
            return SMax(h, body, 0.05f);
        }

        static void BuildBlanket(VhStage s, Transform b, Material mat)
        {
            Vector3 center = new Vector3(0.2f, 0f, 0.26f);
            var mesh = s.meshes.Heightfield(1.76f, 1.72f, 70, 70, (x, z) => BlanketHeight(x + center.x, z + center.z), 1.6f);
            // Breathing: her shoulder and ribs rise a little under the blanket.
            VhMeshKit.AddBlendShape(mesh, "breath", v =>
            {
                float wx = v.x + center.x, wz = v.z + center.z;
                float k = 1f - ((wx + 0.35f) / 0.26f) * ((wx + 0.35f) / 0.26f) - ((wz - 0.66f) / 0.42f) * ((wz - 0.66f) / 0.42f);
                return Vector3.up * (0.014f * Mathf.Clamp01(k));
            });
            var go = new GameObject("Blanket");
            go.transform.SetParent(b, false);
            go.transform.localPosition = center;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = ShadowCastingMode.On;
            s.blanket = smr;
        }

        static void BuildPhone(VhStage s, Transform b, Vector3 rest)
        {
            var phone = new GameObject("Phone").transform;
            phone.SetParent(b, false);
            Vector3 topDir = new Vector3(-1f, 0f, 0.12f).normalized;
            s.phoneRestPos = b.TransformPoint(rest);
            s.phoneRestRot = Quaternion.LookRotation(topDir, Vector3.up);
            phone.SetPositionAndRotation(s.phoneRestPos, s.phoneRestRot);
            VhMeshKit.Show(phone, "Body", s.meshes.RoundedBox(new Vector3(0.072f, 0.009f, 0.148f), 0.0055f, 4), s.style.Mat(VhWakingStyle.PhoneBody), new Vector3(0f, 0.0045f, 0f), Quaternion.identity);
            s.phoneScreenMat = ScreenMat(s, "VH phone screen");
            s.phoneScreen = VhMeshKit.Show(phone, "Screen", s.meshes.Quad(0.066f, 0.141f), s.phoneScreenMat, new Vector3(0f, 0.0093f, 0f), Quaternion.Euler(90f, 0f, 0f), false);
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
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
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

            // The table, the floor under it and the wall behind (they come up with the table).
            var tablePiece = Piece(s, t, "table", VhTitleAction.None, Vector3.zero, Quaternion.identity);
            VhMeshKit.Show(tablePiece.transform, "Table top", s.meshes.RoundedBox(new Vector3(1.5f, 0.045f, 1.1f), 0.015f, 3), st.Mat(VhWakingStyle.WoodPale, woodTex), new Vector3(0f, 0.7275f, 0.15f), Quaternion.identity);
            Box(s, tablePiece.transform, "Floor", new Vector3(8f, 0.02f, 8f), new Vector3(0f, -0.01f, 0f), st.Mat(VhWakingStyle.Floor * 0.8f), 1f, null, false);
            Box(s, tablePiece.transform, "Wall", new Vector3(8f, 3f, 0.1f), new Vector3(0f, 1.5f, 1.9f), st.Mat(VhWakingStyle.Wall * 0.85f, plaster), 0.6f, null, false);
            FinishPiece(tablePiece, false);

            // Where the camera sits for the title (the tablet faces it).
            Vector3 camPos = t.TransformPoint(new Vector3(0f, 1.27f, -0.5f));

            // The tablet (Play), propped on its folio, screen to the camera.
            Vector3 tabletFoot = new Vector3(-0.07f, 0.75f, 0.16f);
            Vector3 footWorld = t.TransformPoint(tabletFoot);
            Vector3 toCam = (camPos - (footWorld + Vector3.up * 0.08f)).normalized;
            Vector3 screenUp = (Vector3.up - toCam * Vector3.Dot(Vector3.up, toCam)).normalized;
            var tablet = Piece(s, t, "tablet", VhTitleAction.Play, tabletFoot, Quaternion.LookRotation(screenUp, toCam));
            tablet.lift = 0.008f;
            tablet.tilt = 1.5f;
            VhMeshKit.Show(tablet.transform, "Body", s.meshes.RoundedBox(new Vector3(0.27f, 0.009f, 0.185f), 0.008f, 4), st.Mat(VhWakingStyle.PhoneBody), new Vector3(0f, 0f, 0.0925f), Quaternion.identity);
            s.tabletScreenMat = ScreenMat(s, "VH tablet screen");
            VhMeshKit.Show(tablet.transform, "Screen", s.meshes.Quad(0.252f, 0.158f), s.tabletScreenMat, new Vector3(0f, 0.0047f, 0.0925f), Quaternion.Euler(90f, 0f, 0f), false);
            // The folio's flap behind it, from halfway up its back down to the table.
            Vector3 a = new Vector3(0f, -0.006f, 0.1f);
            Vector3 aWorld = tablet.transform.TransformPoint(a);
            Vector3 back = Vector3.ProjectOnPlane(-toCam, Vector3.up).normalized;
            Vector3 bWorld = new Vector3(aWorld.x, footWorld.y + 0.003f, aWorld.z) + back * 0.06f;
            Vector3 flapMid = tablet.transform.InverseTransformPoint((aWorld + bWorld) * 0.5f);
            Quaternion flapRot = Quaternion.Inverse(tablet.transform.rotation) * Quaternion.LookRotation(bWorld - aWorld, toCam);
            Box(s, tablet.transform, "Folio flap", new Vector3(0.24f, 0.005f, Vector3.Distance(aWorld, bWorld)), flapMid, st.Mat(new Color(0.24f, 0.26f, 0.30f)), 1f, flapRot);
            FinishPiece(tablet, true);

            // The mug (Quit): half drunk, "Quit" in the milk.
            var mug = Piece(s, t, "mug", VhTitleAction.Quit, new Vector3(0.27f, 0.75f, 0.0f), Quaternion.Euler(0f, -25f, 0f));
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
            VhMeshKit.Show(mug.transform, "Coffee", s.meshes.Disc(0.0395f, 40), Crop(st.Mat(Color.white, coffee, 0f), coffee, 1f, 1.1f), new Vector3(0f, 0.081f, 0f), Quaternion.Euler(0f, 25f, 0f), false);
            FinishPiece(mug, true);
            if (!VhTitleTable.QuitAvailable) mug.action = VhTitleAction.None;

            // The notepad (Credits) with the pen on it.
            var pad = Piece(s, t, "notepad", VhTitleAction.Credits, new Vector3(-0.28f, 0.75f, -0.14f), Quaternion.Euler(0f, 8f, 0f));
            VhMeshKit.Show(pad.transform, "Pad", s.meshes.RoundedBox(new Vector3(0.15f, 0.014f, 0.21f), 0.003f, 3), st.Mat(VhWakingStyle.Paper), new Vector3(0f, 0.007f, 0f), Quaternion.identity);
            var page = Art(s, "notepad_page", () => tx.NotepadPage());
            VhMeshKit.Show(pad.transform, "Page", s.meshes.Quad(0.144f, 0.2f), Crop(st.Mat(Color.white, page, 0f), page, 0.144f / 0.2f), new Vector3(0f, 0.0143f, -0.003f), Quaternion.Euler(90f, 0f, 0f), false);
            VhMeshKit.Show(pad.transform, "Binding", s.meshes.RoundedBox(new Vector3(0.152f, 0.012f, 0.02f), 0.004f, 3), st.Mat(new Color(0.22f, 0.24f, 0.28f)), new Vector3(0f, 0.013f, 0.1f), Quaternion.identity);
            FinishPiece(pad, true);
            var pen = Piece(s, t, "pen", VhTitleAction.Credits, new Vector3(-0.26f, 0.769f, -0.17f), Quaternion.Euler(0f, 32f, 90f), "notepad");
            var navy = st.Mat(new Color(0.18f, 0.21f, 0.32f));
            VhMeshKit.Show(pen.transform, "Barrel", s.meshes.Cylinder(0.0045f, 0.0045f, 0.12f, 12), navy, new Vector3(0f, -0.06f, 0f), Quaternion.identity);
            VhMeshKit.Show(pen.transform, "Tip", s.meshes.Cylinder(0.0045f, 0.0008f, 0.018f, 12), st.Mat(VhWakingStyle.Metal), new Vector3(0f, 0.06f, 0f), Quaternion.identity);
            Box(s, pen.transform, "Clip", new Vector3(0.002f, 0.04f, 0.0025f), new Vector3(0.005f, -0.04f, 0f), st.Mat(VhWakingStyle.Metal));
            pen.lift = 0.012f;
            FinishPiece(pen, true);

            // Nadia's ID badge on its lanyard (Settings).
            var badge = Piece(s, t, "badge", VhTitleAction.Settings, new Vector3(0.2f, 0.75f, -0.25f), Quaternion.Euler(0f, -14f, 0f));
            VhMeshKit.Show(badge.transform, "Card", s.meshes.RoundedBox(new Vector3(0.056f, 0.002f, 0.088f), 0.0015f, 2), st.Mat(new Color(0.9f, 0.9f, 0.88f)), new Vector3(0f, 0.001f, 0f), Quaternion.identity);
            var photo = s.boot != null ? s.boot.Art("badge_photo") : null;
            var face = Art(s, "badge_face", () => tx.BadgeFace(photo));
            // The art shows the card standing in a 16:9 frame: crop to the card (about 90% of the height).
            bool drawn = s.missingArt.Contains("badge_face");
            VhMeshKit.Show(badge.transform, "Face", s.meshes.Quad(0.053f, 0.085f), Crop(st.Mat(Color.white, face, 0f), face, 0.053f / 0.085f, drawn ? 1f : 1.1f), new Vector3(0f, 0.00215f, 0f), Quaternion.Euler(90f, 0f, 0f), false);
            Box(s, badge.transform, "Clip", new Vector3(0.016f, 0.004f, 0.012f), new Vector3(0f, 0.003f, 0.05f), st.Mat(VhWakingStyle.Metal));
            var lanyard = new List<Vector3>
            {
                new Vector3(0f, 0.003f, 0.056f), new Vector3(0.006f, 0.003f, 0.1f), new Vector3(0.04f, 0.003f, 0.145f),
                new Vector3(0.095f, 0.003f, 0.155f), new Vector3(0.135f, 0.003f, 0.11f), new Vector3(0.13f, 0.003f, 0.04f),
                new Vector3(0.095f, 0.003f, 0.0f), new Vector3(0.06f, 0.003f, -0.012f),
            };
            VhMeshKit.Show(badge.transform, "Lanyard", s.meshes.Tube(lanyard, 0.0032f, 6), st.Mat(VhWakingStyle.Lanyard, null, 0.6f), Vector3.zero, Quaternion.identity);
            FinishPiece(badge, true);

            var keyGo = new GameObject("Morning (light)");
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

        static void Poses(VhStage s)
        {
            var b = s.bedroom;
            Vector3 eyes = b.TransformPoint(Eyes);
            s.AddPose("pillow", eyes + new Vector3(0f, 0.02f, 0f), b.TransformPoint(new Vector3(-0.7f, 2.6f, 0.7f)), Vector3.forward, 0f, 62f);
            s.AddPose("nightstand", eyes + new Vector3(-0.02f, -0.02f, 0f), b.TransformPoint(new Vector3(-0.86f, 0.6f, 1.26f)), Vector3.up, -16f, 62f);
            s.AddPose("away", eyes + new Vector3(0.04f, -0.03f, -0.02f), b.TransformPoint(new Vector3(1.2f, 0.7f, 0.9f)), Vector3.up, 24f, 62f);
            s.AddPose("overhead", b.TransformPoint(new Vector3(-0.52f, 1.62f, 0.98f)), b.TransformPoint(new Vector3(-0.6f, 0.55f, 1.24f)), Vector3.forward, 0f, 50f);
            var t = s.tableRoot;
            s.AddPose("table", t.TransformPoint(new Vector3(0f, 1.27f, -0.5f)), t.TransformPoint(new Vector3(0.02f, 0.77f, 0.03f)), Vector3.up, 0f, 42f);
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
