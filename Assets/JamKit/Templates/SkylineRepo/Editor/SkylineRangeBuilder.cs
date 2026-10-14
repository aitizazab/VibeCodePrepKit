using JamKit.Skyline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>Builds the firing range (Task 14): a 12 x 30 m room (floor, side walls, back wall, and the wall behind the
    /// player), the player rig at the origin facing +Z, three tipping plates at 8, 12 and 18 m, the HUD, a 2D source for the
    /// marker beep, the range director (which runs the scripted showcase only under SkylineRunArgs.Demo) and a still camera
    /// for the judges.</summary>
    public static class SkylineRangeBuilder
    {
        const float Width = 12f, Length = 30f, Back = 6f, WallHeight = 5f, WallThick = 0.4f;
        static readonly float[] PlateDistances = { 8f, 12f, 18f };
        static readonly float[] PlateSides = { 0f, -1.6f, 1.6f };
        const string PanelSettingsPath = "Assets/JamKit/UI/JamKitPanelSettings.asset";

        public static void Build(string path)
        {
            var scene = SkylineScenes.NewScene();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);

            // The room runs from z = -Back (behind the player) to z = Length - Back (the back wall behind the plates).
            var room = new GameObject("Range").transform;
            float zMin = -Back, zMax = Length - Back, zMid = (zMin + zMax) * 0.5f;
            var wall = new Color(0.55f, 0.56f, 0.6f);
            Block(room, "Floor", new Vector3(0f, -0.25f, zMid), new Vector3(Width, 0.5f, Length), new Color(0.36f, 0.37f, 0.4f));
            Block(room, "Wall Left", new Vector3(-Width * 0.5f - WallThick * 0.5f, WallHeight * 0.5f, zMid), new Vector3(WallThick, WallHeight, Length), wall);
            Block(room, "Wall Right", new Vector3(Width * 0.5f + WallThick * 0.5f, WallHeight * 0.5f, zMid), new Vector3(WallThick, WallHeight, Length), wall);
            Block(room, "Back Wall", new Vector3(0f, WallHeight * 0.5f, zMax + WallThick * 0.5f), new Vector3(Width + WallThick * 2f, WallHeight, WallThick), new Color(0.3f, 0.32f, 0.38f));
            Block(room, "Rear Wall", new Vector3(0f, WallHeight * 0.5f, zMin - WallThick * 0.5f), new Vector3(Width + WallThick * 2f, WallHeight, WallThick), wall);
            for (int i = 0; i < PlateDistances.Length; i++)       // distance lines on the floor
                Block(room, "Line " + PlateDistances[i] + " m", new Vector3(0f, 0.005f, PlateDistances[i] - 0.6f), new Vector3(Width, 0.01f, 0.06f), new Color(0.9f, 0.75f, 0.2f), false);

            var player = SkylinePlayerBuilder.Build(null, Vector3.zero, 0f);

            var plates = new SkylineTargetPlate[PlateDistances.Length];
            for (int i = 0; i < plates.Length; i++)
                plates[i] = Plate(room, "plate-" + (i + 1), new Vector3(PlateSides[i], 0f, PlateDistances[i]));

            var hudGo = new GameObject("HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel != null) doc.panelSettings = panel;
            else Debug.LogWarning("SkylineRangeBuilder: no " + PanelSettingsPath + "; the HUD makes a bare panel at run time");
            var hud = hudGo.AddComponent<SkylineHud>();
            hud.panelSettings = panel;
            hud.player = player;

            var beep = new GameObject("Marker Beep").AddComponent<AudioSource>();
            beep.playOnAwake = false;
            beep.spatialBlend = 0f;
            beep.volume = 1f;

            var director = new GameObject("Range Director").AddComponent<SkylineRangeDirector>();
            director.player = player;
            director.plates = plates;
            director.beepSource = beep;

            var still = new GameObject("Still Range");
            still.transform.SetPositionAndRotation(new Vector3(4.5f, 2.6f, -3.5f), Quaternion.LookRotation(new Vector3(-0.3f, -0.12f, 1f)));
            var stillCam = still.AddComponent<Camera>();
            stillCam.enabled = false;
            still.AddComponent<SkylineStillCamera>().shotName = "range";

            SkylineInkSetup.ApplyToScene(scene);   // comic ink (docs/SKYLINE-STYLE.md)
            EditorSceneManager.SaveScene(scene, path);
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

        /// <summary>A stand with a hinged post and board (a Body hit zone) and a bullseye on its face (a WeakPoint hit zone).</summary>
        static SkylineTargetPlate Plate(Transform parent, string id, Vector3 at)
        {
            var root = new GameObject(id).transform;
            root.SetParent(parent, false);
            root.localPosition = at;
            var plate = root.gameObject.AddComponent<SkylineTargetPlate>();
            plate.id = id;
            plate.centerOffset = new Vector3(0f, 1.25f, 0f);
            plate.radius = 0.45f;

            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Stand";
            stand.transform.SetParent(root, false);
            stand.transform.localPosition = new Vector3(0f, 0.04f, 0.25f);
            stand.transform.localScale = new Vector3(1f, 0.08f, 0.6f);
            Tint(stand, new Color(0.2f, 0.2f, 0.22f));

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root, false);
            hinge.localPosition = new Vector3(0f, 0.08f, 0f);
            plate.hinge = hinge;

            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Post";
            post.transform.SetParent(hinge, false);
            post.transform.localPosition = new Vector3(0f, 0.4f, 0.03f);
            post.transform.localScale = new Vector3(0.08f, 0.8f, 0.04f);
            Tint(post, new Color(0.25f, 0.25f, 0.28f));
            Object.DestroyImmediate(post.GetComponent<Collider>());

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(hinge, false);
            board.transform.localPosition = new Vector3(0f, 1.17f, 0f);
            board.transform.localScale = new Vector3(0.9f, 0.9f, 0.05f);
            Tint(board, new Color(0.92f, 0.9f, 0.84f));
            var bz = board.AddComponent<SkylineHitZone>();
            bz.owner = plate;
            bz.zone = HitZone.Body;

            var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eye.name = "Bullseye";
            eye.transform.SetParent(hinge, false);
            eye.transform.localPosition = new Vector3(0f, 1.17f, -0.035f);
            eye.transform.localScale = new Vector3(0.22f, 0.22f, 0.02f);
            Tint(eye, new Color(0.85f, 0.15f, 0.12f));
            var ez = eye.AddComponent<SkylineHitZone>();
            ez.owner = plate;
            ez.zone = HitZone.WeakPoint;
            return plate;
        }
    }
}
