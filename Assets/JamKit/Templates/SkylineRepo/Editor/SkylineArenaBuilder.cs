using System.Collections.Generic;
using JamKit.Movement;
using JamKit.Skyline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// Builds the arena scene (docs/SKYLINE-PLAN-M1.md Task 16) from <see cref="GalleriaArena"/>'s numbers only: plain
    /// stand-in architecture (URP Lit colours, glass and a few glowing strips; Task 21 dresses it), every collider an
    /// axis-aligned box.
    /// <list type="bullet">
    /// <item><b>The warm-up hall</b> (x -15..15, z -48..-20, walls 8 m, a glass roof): runnable walls; the player at
    /// <see cref="GalleriaArena.PlayerStart"/> facing +Z; the practice bot's gantry (the bot itself is spawned by the
    /// director); the kiosk (3 x 3 m up to <see cref="GalleriaArena.BoostLedgeTop"/>, not runnable, a glass counter band
    /// round it) with the <see cref="SkylineScrapCache"/> on top; a 6 x 5 m doorway into the main hall at z -20.</item>
    /// <item><b>The main hall</b> (x -25..25, z -20..20, walls 14 m, a glass skylight roof at 14 m): runnable walls all
    /// round (the side walls by the gondolas' rails are a wall-run route; Task 21's dressing draws their shopfronts and
    /// curtain wall inside their volumes); the mezzanine deck exactly as <see cref="GalleriaArena.MezzanineDeck"/> (solid,
    /// so it blocks sight above and below except past its open edge at z 12) with a glass balustrade and two plinths under
    /// the spotlights' bases (both dressing, no colliders); the cover exactly as <see cref="GalleriaArena.HallCover"/> (14 boxes, solid, not
    /// runnable), a pillar's top split where it meets the deck so no two solids overlap; the three encounter zones and
    /// their spawn markers; the entry lane's floor marker (shown while the next fight waits).</item>
    /// <item>The HUD, a 2D voice source, the <see cref="SkylineArenaDirector"/>, the <see cref="SkylineArenaDemo"/> and eight
    /// <see cref="SkylineStillCamera"/>s. The sun at 50 degrees with a procedural sky is only the fallback now.</item>
    /// <item><b>Task 21</b> replaces the sun and sky with the afternoon look (<see cref="SkylineLook.ApplyAfternoon"/>; the plain sun
    /// stays as the fallback if the look fails) and dresses the grey box (<see cref="Dress"/>, SkylineArenaDress.cs): the
    /// gameplay pieces keep their positions and colliders; the dressing re-skins them and adds visual-only geometry.</item>
    /// </list>
    /// </summary>
    public static partial class SkylineArenaBuilder
    {
        const float WarmupHeight = 8f, HallHeight = 14f, WallThick = 0.4f, RoofThick = 0.2f;
        const float DoorHalfWidth = 3f, DoorHeight = 5f;
        /// <summary>The spotlight body's base sits 0.88 m under its centre (SkylineBotBodies' spotlight mount).</summary>
        const float SpotlightBaseBelow = 0.88f;
        const string PanelSettingsPath = "Assets/JamKit/UI/JamKitPanelSettings.asset";

        static readonly string[] CoverNames =
        {
            "Pillar West", "Pillar Middle", "Pillar East",
            "Food Stall Back SW", "Food Stall Back NW", "Food Stall Back SE", "Food Stall Back NE",
            "Info Booth Screen West", "Info Booth Screen East", "Info Booth Screen Back",
            "Kiosk West", "Kiosk East",
            "Planter West", "Planter East",
        };

        static Material _template;

        public static void Build(string path)
        {
            var scene = SkylineScenes.NewScene();
            SkylineLookLibrary.BeginBuild();
            bool look = false;
            try
            {
                SkylineLook.ApplyAfternoon(scene);
                look = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("SKYLINE_LOOK afternoon failed, the plain sun is used: " + e);
                Light();
            }

            var mWall = Lit("SK_Wall", new Color(0.72f, 0.72f, 0.7f));
            var mWarmWall = Lit("SK_Wall_Warmup", new Color(0.62f, 0.64f, 0.68f));
            var mFloor = Lit("SK_Floor_Hall", new Color(0.78f, 0.76f, 0.72f));
            var mWarmFloor = Lit("SK_Floor_Warmup", new Color(0.42f, 0.43f, 0.46f));
            var mDeck = Lit("SK_Mezzanine", new Color(0.86f, 0.85f, 0.82f));
            var mPillar = Lit("SK_Pillar", new Color(0.9f, 0.9f, 0.88f));
            var mStall = Lit("SK_FoodStall", new Color(0.66f, 0.42f, 0.3f));
            var mScreen = Lit("SK_AdScreen", new Color(0.2f, 0.22f, 0.26f));
            var mKiosk = Lit("SK_Kiosk", new Color(0.82f, 0.78f, 0.7f));
            var mPlanter = Lit("SK_Planter", new Color(0.38f, 0.5f, 0.32f));
            var mGantry = Lit("SK_Gantry", new Color(0.86f, 0.66f, 0.22f));
            var mRoof = Glass("SK_Glass_Skylight", new Color(0.75f, 0.85f, 0.95f, 0.12f));
            var mCounter = Glass("SK_Glass_Counter", new Color(0.8f, 0.9f, 0.95f, 0.3f));
            var mRail = Glass("SK_Glass_Balustrade", new Color(0.7f, 0.8f, 0.88f, 0.25f));
            var mSign = Emissive("SK_Emissive_Sign", new Color(1.6f, 1.1f, 0.5f));
            var mExit = Emissive("SK_Emissive_Doorway", new Color(0.5f, 1.4f, 1.1f));
            var mCache = Emissive("SK_Emissive_ScrapCache", new Color(1.8f, 1.2f, 0.3f));

            var arena = new GameObject("Arena").transform;
            var warm = Group(arena, "Warm-up Hall");
            var hall = Group(arena, "Main Hall");

            // ---------------------------------------------------------- the warm-up hall
            float wx0 = GalleriaArena.WarmupMinX, wx1 = GalleriaArena.WarmupMaxX, wz0 = GalleriaArena.WarmupMinZ, wz1 = GalleriaArena.WarmupMaxZ;
            float hz0 = GalleriaArena.HallMinZ;           // the warm-up's north wall is the hall's south wall, its slab z -20.4..-20
            Box(warm, "Floor", V(wx0, -0.5f, wz0), V(wx1, 0f, wz1), mWarmFloor, true, null);
            Box(warm, "Wall West", V(wx0 - WallThick, 0f, wz0 - WallThick), V(wx0, WarmupHeight, hz0 - WallThick), mWarmWall, true, true);
            Box(warm, "Wall East", V(wx1, 0f, wz0 - WallThick), V(wx1 + WallThick, WarmupHeight, hz0 - WallThick), mWarmWall, true, true);
            Box(warm, "Wall South", V(wx0, 0f, wz0 - WallThick), V(wx1, WarmupHeight, wz0), mWarmWall, true, true);
            NoShadow(Box(warm, "Roof (glass)", V(wx0 - WallThick, WarmupHeight, wz0 - WallThick), V(wx1 + WallThick, WarmupHeight + RoofThick, hz0 - WallThick), mRoof, true, null));

            // the practice bot's gantry (a training rig): two posts and a crossbar over where the bot hovers
            var pb = U(GalleriaArena.PracticeBot);
            var rig = Group(warm, "Practice Rig");
            Box(rig, "Post West", V(pb.x - 2.1f, 0f, pb.z - 0.1f), V(pb.x - 1.9f, 2.8f, pb.z + 0.1f), mGantry, true, null);
            Box(rig, "Post East", V(pb.x + 1.9f, 0f, pb.z - 0.1f), V(pb.x + 2.1f, 2.8f, pb.z + 0.1f), mGantry, true, null);
            Box(rig, "Crossbar", V(pb.x - 2.1f, 2.8f, pb.z - 0.1f), V(pb.x + 2.1f, 3f, pb.z + 0.1f), mGantry, true, null);
            Box(rig, "Floor Mark", V(pb.x - 1.5f, 0f, pb.z - 1.5f), V(pb.x + 1.5f, 0.01f, pb.z + 1.5f), mGantry, false, null, true);

            // the kiosk: the boost ledge, glass counters round it, the scrap cache on top
            var top = U(GalleriaArena.BoostLedgeTop);
            float half = GalleriaArena.BoostLedgeSize * 0.5f;
            var kiosk = Box(warm, "Boost Kiosk", V(top.x - half, 0f, top.z - half), V(top.x + half, top.y, top.z + half), mKiosk, true, false);
            float g = 0.03f;
            Box(kiosk.transform, "Counter Glass South", V(top.x - half, 0.9f, top.z - half - g), V(top.x + half, 2.4f, top.z - half), mCounter, false, null, true);
            Box(kiosk.transform, "Counter Glass North", V(top.x - half, 0.9f, top.z + half), V(top.x + half, 2.4f, top.z + half + g), mCounter, false, null, true);
            Box(kiosk.transform, "Counter Glass West", V(top.x - half - g, 0.9f, top.z - half), V(top.x - half, 2.4f, top.z + half), mCounter, false, null, true);
            Box(kiosk.transform, "Counter Glass East", V(top.x + half, 0.9f, top.z - half), V(top.x + half + g, 2.4f, top.z + half), mCounter, false, null, true);
            var cacheGo = new GameObject("Scrap Cache");
            cacheGo.transform.SetParent(warm, false);
            cacheGo.transform.position = top;
            var cache = cacheGo.AddComponent<SkylineScrapCache>();
            cache.visual = Box(cacheGo.transform, "Crate", V(top.x - 0.35f, top.y, top.z - 0.25f), V(top.x + 0.35f, top.y + 0.4f, top.z + 0.25f), mCache, false, null, true);

            // ---------------------------------------------------------- the main hall
            float x0 = GalleriaArena.HallMinX, x1 = GalleriaArena.HallMaxX, z0 = GalleriaArena.HallMinZ, z1 = GalleriaArena.HallMaxZ;
            Box(hall, "Floor", V(x0, -0.5f, z0), V(x1, 0f, z1), mFloor, true, null);
            Box(hall, "Wall West", V(x0 - WallThick, 0f, z0 - WallThick), V(x0, HallHeight, z1 + WallThick), mWall, true, true);
            Box(hall, "Wall East", V(x1, 0f, z0 - WallThick), V(x1 + WallThick, HallHeight, z1 + WallThick), mWall, true, true);
            Box(hall, "Wall North", V(x0, 0f, z1), V(x1, HallHeight, z1 + WallThick), mWall, true, true);
            Box(hall, "Wall South West", V(x0, 0f, z0 - WallThick), V(-DoorHalfWidth, HallHeight, z0), mWall, true, true);
            Box(hall, "Wall South East", V(DoorHalfWidth, 0f, z0 - WallThick), V(x1, HallHeight, z0), mWall, true, true);
            Box(hall, "Doorway Lintel", V(-DoorHalfWidth, DoorHeight, z0 - WallThick), V(DoorHalfWidth, HallHeight, z0), mWall, true, true);
            Box(hall, "Doorway Sign", V(-DoorHalfWidth, DoorHeight + 0.2f, z0 - WallThick - 0.03f), V(DoorHalfWidth, DoorHeight + 0.5f, z0 - WallThick), mExit, false, null, true);
            NoShadow(Box(hall, "Skylight Roof (glass)", V(x0 - WallThick, HallHeight, z0 - WallThick), V(x1 + WallThick, HallHeight + RoofThick, z1 + WallThick), mRoof, true, null));

            // the mezzanine deck, exactly the data's slab; a glass balustrade at its open edge (no collider, so it changes no
            // sightline) and a plinth under each spotlight's base
            var deck = GalleriaArena.MezzanineDeck;
            Box(hall, "Mezzanine Deck", U(deck.min), U(deck.max), mDeck, true, false);
            Box(hall, "Mezzanine Balustrade (glass)", V(x0, deck.max.Y, deck.min.Z), V(x1, deck.max.Y + 1.05f, deck.min.Z + 0.05f), mRail, false, null, true);
            foreach (var s in GalleriaArena.Skylight().spawns)
            {
                if (s.enemy != SkylineEnemies.SpotlightId) continue;
                float baseY = s.position.Y - SpotlightBaseBelow;
                if (baseY <= deck.max.Y + 0.01f) continue;
                // dressing only: no collider, so FightSim's sightlines (HallCover + the deck) are exactly the arena's
                Box(hall, "Spotlight Plinth " + (s.position.X < 0f ? "West" : "East"), V(s.position.X - 0.5f, deck.max.Y, deck.min.Z),
                    V(s.position.X + 0.5f, baseY, s.position.Z + 0.4f), mDeck, false, null, true);
            }

            // the entry lane where every fight after the food court starts: a plain lit floor marker, shown by the director
            // while the next fight is armed
            var entryMarker = Box(hall, "Entry Marker", V(GalleriaArena.EntryZoneMinX, 0f, GalleriaArena.EntryZoneMinZ),
                V(GalleriaArena.EntryZoneMaxX, 0.01f, GalleriaArena.EntryZoneMaxZ), Lit("SK_EntryMarker", new Color(0.95f, 0.72f, 0.2f)), false, null, true);
            NoShadow(entryMarker);

            // the cover: exactly HallCover()'s boxes; where a box reaches into the deck's slab, the part inside is left out
            // (the deck is solid there anyway), so the union is the data's and no two solids overlap
            var coverRoot = Group(hall, "Cover");
            var cover = GalleriaArena.HallCover();
            for (int i = 0; i < cover.Length; i++)
            {
                string name = cover.Length == CoverNames.Length ? CoverNames[i] : "Cover " + (i + 1);
                var mat = name.StartsWith("Pillar") ? mPillar : name.StartsWith("Food") ? mStall : name.StartsWith("Info") ? mScreen
                    : name.StartsWith("Planter") ? mPlanter : mKiosk;
                foreach (var part in OutsideDeck(cover[i], deck))
                    Box(coverRoot, part.Value == 0 ? name : name + " Cap", U(part.Key.min), U(part.Key.max), mat, true, false);
            }

            // ---------------------------------------------------------- the player, the HUD, the director, the demo
            var player = SkylinePlayerBuilder.Build(null, U(GalleriaArena.PlayerStart), 0f);
            cache.player = player;

            var hudGo = new GameObject("HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel != null) doc.panelSettings = panel;
            else Debug.LogWarning("SkylineArenaBuilder: no " + PanelSettingsPath + "; the HUD makes a bare panel at run time");
            var hud = hudGo.AddComponent<SkylineHud>();
            hud.panelSettings = panel;
            hud.player = player;

            var voice = new GameObject("Voice").AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.volume = 1f;

            var zones = Zones();
            var director = new GameObject("Arena Director").AddComponent<SkylineArenaDirector>();
            director.player = player;
            director.zones = zones;
            director.cache = cache;
            director.voice = voice;
            director.botsRoot = new GameObject("Bots").transform;
            director.entryMarker = entryMarker;
            // the cracks' and sparks' glow copies this scene material (its URP Unlit variant is in the build), not a shader
            // looked up by name at run time
            director.glowTemplate = mSign;

            var demo = new GameObject("Arena Demo").AddComponent<SkylineArenaDemo>();
            demo.player = player;
            demo.director = director;
            demo.cache = cache;

            Stills();

            // ---------------------------------------------------------- the look's dressing (Task 21)
            try { Dress(arena, warm, hall, entryMarker); }
            catch (System.Exception e) { Debug.LogError("SKYLINE_LOOK dressing failed: " + e); }
            SkylineLook.SetupCameras(scene);
            if (look) SkylineLook.BakeReflections(scene, path, arena);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, path);
        }

        // ------------------------------------------------------------------ pieces

        static void Light()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);     // 50 degrees above the horizon
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.47f, 0.52f);
            var sky = Shader.Find("Skybox/Procedural");
            if (sky != null) RenderSettings.skybox = new Material(sky) { name = "SK_Sky_Procedural" };
            RenderSettings.sun = sun;
        }

        /// <summary>The cover box less any part inside the deck's slab: the part below the slab, and the part south of its open
        /// edge at the slab's height (the deck spans the hall's width and runs to the north wall). Value 0 the main part, 1 a
        /// cap.</summary>
        static List<KeyValuePair<CoverBox, int>> OutsideDeck(CoverBox b, CoverBox deck)
        {
            var parts = new List<KeyValuePair<CoverBox, int>>();
            bool overlaps = b.max.Y > deck.min.Y && b.min.Y < deck.max.Y && b.max.Z > deck.min.Z && b.min.Z < deck.max.Z
                && b.max.X > deck.min.X && b.min.X < deck.max.X;
            if (!overlaps)
            {
                parts.Add(new KeyValuePair<CoverBox, int>(b, 0));
                return parts;
            }
            parts.Add(new KeyValuePair<CoverBox, int>(new CoverBox { min = b.min, max = new NVec3(b.max.X, deck.min.Y, b.max.Z) }, 0));
            if (b.min.Z < deck.min.Z)
                parts.Add(new KeyValuePair<CoverBox, int>(new CoverBox
                {
                    min = new NVec3(b.min.X, deck.min.Y, b.min.Z), max = new NVec3(b.max.X, b.max.Y, deck.min.Z),
                }, 1));
            return parts;
        }

        /// <summary>The three encounter zones (no colliders: the director tests the player's feet), each with its spawn markers.
        /// The food court: the hall south of z 5, so it starts as the player comes through the doorway. The skylight and the
        /// facade: the entry lane round the anchor (x -6..6, z -20..-14), each armed once the fight before is cleared, so
        /// every fight starts from <see cref="GalleriaArena.HallEntry"/>, where its balance (dodge ratio, clean line,
        /// bearings) was measured.</summary>
        static SkylineEncounterZone[] Zones()
        {
            var root = new GameObject("Encounter Zones").transform;
            float x0 = GalleriaArena.HallMinX, x1 = GalleriaArena.HallMaxX, z0 = GalleriaArena.HallMinZ, z1 = GalleriaArena.HallMaxZ;
            var defs = GalleriaArena.Encounters();
            var entry = Bounds(GalleriaArena.EntryZoneMinX, GalleriaArena.EntryZoneMaxX, GalleriaArena.EntryZoneMinZ, GalleriaArena.EntryZoneMaxZ);
            var bounds = new[] { Bounds(x0, x1, z0, 5f), entry, entry };
            var zones = new SkylineEncounterZone[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var go = new GameObject("Zone " + defs[i].id);
                go.transform.SetParent(root, false);
                var b = i < bounds.Length ? bounds[i] : Bounds(x0, x1, z0, z1);
                go.transform.position = b.center;
                var z = go.AddComponent<SkylineEncounterZone>();
                z.encounterIndex = i;
                z.encounterId = defs[i].id;
                z.size = b.size;
                foreach (var s in defs[i].spawns)
                {
                    var m = new GameObject("Spawn " + s.enemy + " g" + (i * 100 + s.group)).transform;
                    m.SetParent(go.transform, false);
                    m.position = U(s.position);
                    if (s.railEnd != s.position)
                    {
                        var e = new GameObject("Rail End").transform;
                        e.SetParent(m, false);
                        e.position = U(s.railEnd);
                    }
                }
                zones[i] = z;
            }
            return zones;
        }

        static Bounds Bounds(float x0, float x1, float z0, float z1)
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(x0, -1f, z0), new Vector3(x1, HallHeight, z1));
            return b;
        }

        static void Stills()
        {
            var root = new GameObject("Stills").transform;
            var pb = U(GalleriaArena.PracticeBot);
            var top = U(GalleriaArena.BoostLedgeTop);
            Still(root, "01-warmup-start", V(0f, 2.2f, -47.6f), V(0f, 1.6f, -30f));
            Still(root, "02-practice-bot", V(pb.x + 4f, 2.2f, pb.z - 5f), pb);
            Still(root, "03-kiosk-ledge", V(top.x + 5f, 3.6f, top.z - 7f), V(top.x, top.y - 0.5f, top.z));
            Still(root, "04-doorway", V(0f, 2.4f, -31f), V(0f, 2.4f, -20f));
            Still(root, "05-food-court", V(0f, 3f, -18.5f), V(0f, 2.5f, 2f));
            Still(root, "06-mezzanine", V(0f, 3.2f, 0f), V(0f, 5.5f, 16f));
            Still(root, "07-hall-wide", V(-23.5f, 9f, -18.5f), V(4f, 1.5f, 6f));
            Still(root, "08-skylight", V(0f, 2f, -4f), V(0f, 14f, 8f));
        }

        static void Still(Transform parent, string shot, Vector3 at, Vector3 lookAt)
        {
            var go = new GameObject("Still " + shot);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(lookAt - at));
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 70f;
            go.AddComponent<SkylineStillCamera>().shotName = shot;
        }

        // ------------------------------------------------------------------ helpers

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        /// <summary>An axis-aligned box from <paramref name="min"/> to <paramref name="max"/> (world). With
        /// <paramref name="collider"/> false its collider is removed (and, with <paramref name="markNoCollider"/>, it carries
        /// <see cref="SkylineNoCollider"/> for the lint); with a collider, <paramref name="runnable"/> adds a MovementSurface
        /// on the collider's own object (the engine reads it there) saying whether it can be wall-run.</summary>
        static GameObject Box(Transform parent, string name, Vector3 min, Vector3 max, Material m, bool collider, bool? runnable, bool markNoCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation((min + max) * 0.5f, Quaternion.identity);
            Vector3 size = max - min;
            Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
            go.transform.localScale = new Vector3(size.x / ps.x, size.y / ps.y, size.z / ps.z);
            if (!collider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
                if (markNoCollider) go.AddComponent<SkylineNoCollider>();
            }
            else if (runnable.HasValue) go.AddComponent<MovementSurface>().runnable = runnable.Value;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static GameObject NoShadow(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        static Material Template()
        {
            if (_template != null) return _template;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _template = go.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(go);
            return _template;
        }

        /// <summary>A plain lit colour (saved inside the scene). Its name starts with SK_; the lint flags it untextured until
        /// Task 21's look.</summary>
        static Material Lit(string name, Color c) => new Material(Template()) { name = name, color = c };

        /// <summary>URP Lit, transparent (alpha blended). Named SK_Glass..., which the lint accepts untextured.</summary>
        static Material Glass(string name, Color c)
        {
            var m = Lit(name, c);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            return m;
        }

        /// <summary>A glowing colour (URP Unlit, HDR so bloom catches it). Named SK_Emissive..., which the lint accepts.</summary>
        static Material Emissive(string name, Color hdr)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = sh != null ? new Material(sh) : new Material(Template());
            m.name = name;
            m.color = hdr;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
            return m;
        }
    }
}
