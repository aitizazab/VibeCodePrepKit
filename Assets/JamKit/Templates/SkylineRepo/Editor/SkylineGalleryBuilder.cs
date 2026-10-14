using JamKit.Skyline;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// Builds the enemy gallery (Task 15): four segments along a 60 x 24 m floor, in the order drone, floor-care unit,
    /// spotlight, gondola. The drone hovers over a column (x -14), the floor-care unit sits on a painted square (x -7), the
    /// spotlight stands on a column (x 0), and the gondola rides a rail along a short wall (x 6 to 14, at wall-run height,
    /// 0.8 m off the wall, as the facade's rails). Each bot has a hidden <see cref="SkylineDummy"/> in front of it (the
    /// gondola's on the wall, ready to wall-run past a mine), a close-up camera pose the main camera cuts to, and a still
    /// camera; plus a wide pose and a wide still: five <see cref="SkylineStillCamera"/>s. Every bot faces the camera side (-Z).
    /// The bots themselves are spawned at run time by the <see cref="SkylineGalleryDirector"/> through
    /// <see cref="SkylineBot.Spawn"/>, as the arena will.
    /// </summary>
    public static class SkylineGalleryBuilder
    {
        const float DroneX = -14f, FloorX = -7f, SpotX = 0f;
        const float RailStartX = 6f, RailEndX = 14f, WallZ = 2f;

        public static void Build(string path)
        {
            var scene = SkylineScenes.NewScene();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.52f);

            var room = new GameObject("Gallery").transform;
            Block(room, "Floor", new Vector3(2f, -0.25f, -2f), new Vector3(60f, 0.5f, 24f), new Color(0.36f, 0.37f, 0.4f));
            Block(room, "Back Wall", new Vector3(2f, 3f, 9.2f), new Vector3(60f, 6f, 0.4f), new Color(0.3f, 0.32f, 0.38f));

            // the drone's column, the floor-care unit's square, the spotlight's column
            Block(room, "Drone Column", new Vector3(DroneX, 0.75f, 0f), new Vector3(0.5f, 1.5f, 0.5f), new Color(0.82f, 0.8f, 0.74f));
            Block(room, "Floor-care Square", new Vector3(FloorX, 0.005f, 0f), new Vector3(2.4f, 0.01f, 2.4f), new Color(0.75f, 0.6f, 0.2f), false);
            Block(room, "Spotlight Column", new Vector3(SpotX, 0.96f, 0f), new Vector3(0.6f, 1.92f, 0.6f), new Color(0.82f, 0.8f, 0.74f));
            // the gondola's wall: a shop front, its face at z = WallZ
            Block(room, "Facade Wall", new Vector3(12f, 2.25f, WallZ + 0.2f), new Vector3(15f, 4.5f, 0.4f), new Color(0.62f, 0.66f, 0.72f));
            Block(room, "Facade Glass", new Vector3(12f, 2.4f, WallZ - 0.01f), new Vector3(13f, 2.6f, 0.02f), new Color(0.45f, 0.58f, 0.68f), false);

            var markers = new GameObject("Spawns").transform;
            var director = new GameObject("Gallery Director").AddComponent<SkylineGalleryDirector>();

            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            camGo.AddComponent<AudioListener>();
            director.mainCamera = cam;

            var wide = Pose(markers, "Pose Wide", new Vector3(2.5f, 6f, -19f), new Vector3(2.5f, 1.5f, 0f));
            director.widePose = wide;

            float dummyY = GalleriaArena.RailY - 1.25f;     // the gondola's dummy: its chest at the rail's height
            director.slots = new[]
            {
                Slot(markers, "drone", SkylineEnemies.DroneId, new Vector3(DroneX, 2.4f, 0f), null,
                    Dummy(room, "Dummy drone", new Vector3(DroneX + 2.5f, 0f, -3.5f), new Vector3(DroneX, 0f, 0f)),
                    Pose(markers, "Pose drone", new Vector3(DroneX + 1.6f, 2.4f, -7.5f), new Vector3(DroneX + 0.6f, 1.7f, -1f)), 12f),
                Slot(markers, "floor-care", SkylineEnemies.FloorCareId, new Vector3(FloorX, 0f, 0f), null,
                    Dummy(room, "Dummy floor-care", new Vector3(FloorX + 2.5f, 0f, -3f), new Vector3(FloorX, 0f, 0f)),
                    Pose(markers, "Pose floor-care", new Vector3(FloorX + 1.5f, 2.1f, -7.5f), new Vector3(FloorX + 0.8f, 0.4f, -1.5f)), 12f),
                Slot(markers, "spotlight", SkylineEnemies.SpotlightId, new Vector3(SpotX, 2.8f, 0f), null,
                    Dummy(room, "Dummy spotlight", new Vector3(SpotX + 2.5f, 0f, -4f), new Vector3(SpotX, 0f, 0f)),
                    Pose(markers, "Pose spotlight", new Vector3(SpotX + 1.5f, 2.5f, -8f), new Vector3(SpotX + 0.6f, 1.8f, -1.5f)), 12f),
                Slot(markers, "gondola", SkylineEnemies.GondolaId, new Vector3(RailStartX, GalleriaArena.RailY, WallZ - 0.8f),
                    Marker(markers, "Gondola Rail End", new Vector3(RailEndX, GalleriaArena.RailY, WallZ - 0.8f), 0f),
                    Dummy(room, "Dummy gondola", new Vector3(18f, dummyY, WallZ - 0.3f), new Vector3(0f, dummyY, WallZ - 0.3f)),
                    Pose(markers, "Pose gondola", new Vector3(12f, 2.8f, -7.5f), new Vector3(11.5f, 2.2f, 1.5f)), 14f),
            };
            // the gondola's dummy wall-runs along the wall from x 18 to x 12.5, its chest at the rail's height
            director.slots[3].runFrom = Marker(markers, "Wall-run From", new Vector3(18f, dummyY, WallZ - 0.3f), -90f);
            director.slots[3].runTo = Marker(markers, "Wall-run To", new Vector3(12.5f, dummyY, WallZ - 0.3f), -90f);

            for (int i = 0; i < director.slots.Length; i++)
            {
                var pose = director.slots[i].cameraPose;
                var still = Still("Still " + director.slots[i].label, pose.position, pose.rotation, director.slots[i].label);
                DisplayBot(still, director.slots[i], pose);
            }
            Still("Still wide", wide.position, wide.rotation, "wide");
            cam.transform.SetPositionAndRotation(wide.position, wide.rotation);

            SkylineInkSetup.ApplyToScene(scene);   // comic ink (docs/SKYLINE-STYLE.md)
            EditorSceneManager.SaveScene(scene, path);
        }

        static SkylineGalleryDirector.Slot Slot(Transform markers, string label, string enemyId, Vector3 at, Transform railEnd, SkylineDummy dummy, Transform pose, float seconds) =>
            new SkylineGalleryDirector.Slot
            {
                label = label, enemyId = enemyId, spawn = Marker(markers, "Spawn " + label, at, 180f), railEnd = railEnd, dummy = dummy,
                cameraPose = pose, seconds = seconds,
            };

        /// <summary>Fix round 1 (the gallery's stills caught dead bots, a wreck behind a pillar): each bot's still gets a display bot
        /// (<see cref="SkylineDisplayBot"/>), alive and idle, 4.5 m down the camera's line of sight and facing the camera, made only
        /// while that still renders; every other bot and wreck is hidden for the shot. A floor-care unit stands on the floor.</summary>
        static void DisplayBot(SkylineStillCamera still, SkylineGalleryDirector.Slot slot, Transform pose)
        {
            var at = pose.position + pose.forward * 4.5f;
            if (slot.enemyId == SkylineEnemies.FloorCareId) at.y = 0f;
            var display = still.gameObject.AddComponent<SkylineDisplayBot>();
            display.enemyId = slot.enemyId;
            display.position = at;
            display.yaw = Mathf.Atan2(pose.position.x - at.x, pose.position.z - at.z) * Mathf.Rad2Deg;
        }

        static Transform Marker(Transform parent, string name, Vector3 at, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static Transform Pose(Transform parent, string name, Vector3 at, Vector3 lookAt)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(at, Quaternion.LookRotation(lookAt - at));
            return t;
        }

        static SkylineStillCamera Still(string name, Vector3 at, Quaternion rot, string shot)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(at, rot);
            var c = go.AddComponent<Camera>();
            c.fieldOfView = 55f;
            c.enabled = false;
            var still = go.AddComponent<SkylineStillCamera>();
            still.shotName = shot;
            return still;
        }

        /// <summary>A beige mannequin (its root at its feet, a capsule collider for the bots' bolts), facing <paramref name="face"/>.</summary>
        static SkylineDummy Dummy(Transform parent, string name, Vector3 feet, Vector3 face)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Vector3 d = face - feet;
            d.y = 0f;
            root.transform.SetPositionAndRotation(feet, d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d) : Quaternion.identity);
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.3f;
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var beige = new Color(0.86f, 0.76f, 0.6f);
            Part(body, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.05f, 0f), new Vector3(0.5f, 0.55f, 0.32f), beige);
            Part(body, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.67f, 0f), new Vector3(0.26f, 0.3f, 0.26f), beige);
            Part(body, PrimitiveType.Capsule, "Legs", new Vector3(0f, 0.42f, 0f), new Vector3(0.36f, 0.42f, 0.24f), beige * 0.85f);
            Part(body, PrimitiveType.Cube, "Visor", new Vector3(0f, 1.7f, 0.12f), new Vector3(0.2f, 0.06f, 0.04f), new Color(0.15f, 0.15f, 0.18f));
            var dummy = root.AddComponent<SkylineDummy>();
            dummy.body = body;
            return dummy;
        }

        static void Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Tint(go, color);
        }

        static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 size, Color color, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            Tint(go, color);
            return go;
        }

        static void Tint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = new Material(r.sharedMaterial) { color = color };   // saved inside the scene
        }
    }
}
