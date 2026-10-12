using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Night 2 for the judge (docs/redesign/house.md "Captures"): <see cref="Stills"/> shoots every room at her eye height
    /// (1.5 m), the hide-and-seek's moments (Theo counting at the table, Theo searching seen from a hiding place, a memory
    /// glowing), each beat of the escape, and Chapel Row in the rain with Lena at the tower's window, 1920x1080 through URP,
    /// into out/house-captures/&lt;time&gt;/ with shots.txt. <see cref="RecordFromCommandLine"/> plays the night by itself
    /// (the hide-and-seek, then the escape) and writes a frame every 0.5 s of game time (DreamHouseGame's recorder).
    /// tools/house-capture.ps1 runs both.
    /// </summary>
    public static class DreamHouseCapture
    {
        public const int Width = 1920, Height = 1080;
        const float Eye = DreamHouseBuilder.NadiaEye;

        [MenuItem("JamKit/Dream House/Capture Stills")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.RevealInFinder(Stills(null));
        }

        /// <summary>-executeMethod JamKit.DreamEscape.EditorTools.DreamHouseCapture.StillsFromCommandLine</summary>
        public static void StillsFromCommandLine()
        {
            int code = 0;
            try { Debug.Log("JAMKIT_HOUSE_CAPTURE_OK " + Stills(null)); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        /// <summary>-executeMethod JamKit.DreamEscape.EditorTools.DreamHouseCapture.RecordFromCommandLine -houseRecord &lt;folder&gt;
        /// [-houseRecordRun all|hideandseek|escape]: opens the scene and plays it; the game records and quits.</summary>
        public static void RecordFromCommandLine()
        {
            if (!File.Exists(DreamHouseBuilder.ScenePath))
            {
                Debug.LogError("JAMKIT_HOUSE_RUN missing the scene: rebuild it first (DreamHouseBuilder.RebuildFromCommandLine)");
                EditorApplication.Exit(1);
                return;
            }
            if (!DreamHouseGame.RecordArgs(Environment.GetCommandLineArgs(), out var dir, out var run))
            {
                Debug.LogError("JAMKIT_HOUSE_RUN pass -houseRecord <folder>");
                EditorApplication.Exit(1);
                return;
            }
            EditorSceneManager.OpenScene(DreamHouseBuilder.ScenePath, OpenSceneMode.Single);
            Debug.Log("JAMKIT_HOUSE_RUN playing (" + run + ") into " + dir);
            EditorApplication.EnterPlaymode();
        }

        struct Shot
        {
            public string name, what, show;
            public Vector3 eye, look;
            public Shot(string name, Vector3 eye, Vector3 look, string what, string show = null) { this.name = name; this.eye = eye; this.look = look; this.what = what; this.show = show; }
        }

        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>The stills (into <paramref name="dir"/>, or a new out/house-captures/&lt;time&gt;).</summary>
        public static string Stills(string dir)
        {
            dir = dir ?? Path.Combine(Directory.GetCurrentDirectory(), "out", "house-captures", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            if (!File.Exists(DreamHouseBuilder.ScenePath)) { Debug.LogError("JAMKIT_HOUSE_CAPTURE missing scene: " + DreamHouseBuilder.ScenePath); return dir; }
            var h = DreamHouse.Build();
            var e = HouseEscape.Build();
            const float G = DreamHouse.Ground, Up = DreamHouse.Upper, B = DreamHouse.Basement, A = DreamHouse.Attic;
            var arr = U(h.arrival);
            var shots = new List<Shot>
            {
                new Shot("house-01-rowan-street", arr + Vector3.up * Eye, new Vector3(0f, 3.5f, 3f), "Night 2 starts on Rowan Street: the house, its garden, the gate"),
                new Shot("house-02-the-porch", new Vector3(-2.6f, Eye, -5f), new Vector3(0f, 1.8f, 0f), "The front path and the porch; the front door open"),
                new Shot("house-03-front-hall", new Vector3(0.2f, G + Eye, 0.5f), new Vector3(-0.6f, G + 1.8f, 6f), "The front hall from the door: the stairs, the photo wall, the back hall beyond"),
                new Shot("house-04-living-room", new Vector3(-2.3f, G + Eye, 0.8f), new Vector3(-6.8f, G + 1.0f, 3.4f), "The living room: the fire, the sofa, the window seat"),
                new Shot("house-05-dining-table", new Vector3(6.9f, G + Eye, 0.6f), new Vector3(4.4f, G + 0.8f, 2.9f), "The table set for four, the pancakes; the fourth chair is hers"),
                new Shot("house-06-place-card-seated", new Vector3(5.9f, G + 1.05f, 2.7f), new Vector3(5.1f, G + 0.78f, 2.95f), "Sitting at the fourth chair: her place card (the first memory)"),
                new Shot("house-07-kitchen", new Vector3(2.4f, G + Eye, 5.9f), new Vector3(5.6f, G + 1.2f, 10.6f), "The kitchen: the fridge, the counters, the window over the sink"),
                new Shot("house-08-fridge-calendar", new Vector3(2.3f, G + Eye, 8.6f), new Vector3(2.3f, G + 1.35f, 10.08f), "The fridge calendar, glowing: a memory"),
                new Shot("house-09-den", new Vector3(-2.4f, G + Eye, 6.8f), new Vector3(-6.2f, G + 0.9f, 10.2f), "Elias's den: his desk under the window, the couch and the TV"),
                new Shot("house-10-back-hall", new Vector3(0.9f, G + Eye, 10.3f), new Vector3(-1.1f, G - 0.6f, 6.6f), "The back hall: the basement stairs, the coats"),
                new Shot("house-11-upper-hall", new Vector3(0.6f, Up + Eye, 10.2f), new Vector3(0f, Up + 1.3f, 1.0f), "The upper hall, the stairwell's balustrade, the doors"),
                new Shot("house-12-theos-room", new Vector3(-2.4f, Up + Eye, 1.0f), new Vector3(-5.6f, Up + 0.7f, 4.0f), "Theo's room: his bed under the stars wallpaper, the toy chest, the wardrobe"),
                new Shot("house-13-the-bad-dream", new Vector3(-3.9f, Up + Eye, 1.75f), new Vector3(-4.05f, Up + 1.2f, 0.16f), "Over his desk: THE BAD DREAM pinned to the wall (the redrawn drawing), glowing: a memory"),
                new Shot("house-14-height-marks", new Vector3(-2.7f, Up + Eye, 2.4f), new Vector3(-1.79f, Up + 1.25f, 1.96f), "Theo's height marks on his door frame, stopping at ten"),
                new Shot("house-15-their-room", new Vector3(2.3f, Up + Eye, 5.9f), new Vector3(6.6f, Up + 0.7f, 3.6f), "Elias and Lena's room"),
                new Shot("house-16-bathroom", new Vector3(-2.0f, Up + Eye, 7.2f), new Vector3(-3.9f, Up + 0.9f, 5.6f), "The bathroom: the shower curtain (a hiding place)"),
                new Shot("house-17-guest-room", new Vector3(-2.2f, Up + Eye, 9.9f), new Vector3(-5.5f, Up + 0.8f, 9.6f), "The guest room, made up for her"),
                new Shot("house-18-attic", new Vector3(4.6f, A + Eye, 7.6f), new Vector3(-1.5f, A + 1.0f, 5.5f), "The attic from the top of its stairs: the family's stored life"),
                new Shot("house-19-observatory", new Vector3(-3.6f, A + Eye, 4.2f), new Vector3(-5.25f, A + 2.6f, 8.0f), "Lena's observatory: the telescope aimed up through the round window at the sky, her dais under it"),
                new Shot("house-20-rec-room", new Vector3(-0.6f, B + Eye, 3.8f), new Vector3(-5.2f, B + 0.6f, 3.0f), "The basement's rec room: the ping-pong table, the old couch"),
                new Shot("house-21-workshop", new Vector3(3.66f, B + Eye, 2.4f), new Vector3(3.66f, B + 0.95f, 0.45f), "Elias's workshop: his bench and his radio (a memory)"),
                new Shot("house-22-garage", new Vector3(8.0f, G + Eye, 6.8f), new Vector3(10.0f, G + 0.8f, 2.2f), "The garage: the car under its cover"),
                new Shot("house-23-back-yard", new Vector3(0.2f, DreamHouse.Yard + Eye, 12.4f), new Vector3(-6.5f, 2.2f, 18f), "The back yard: the sheets on the line, the oak and Theo's treehouse"),
                new Shot("house-24-treehouse", new Vector3(-6.2f, DreamHouse.Yard + Eye, 14.6f), new Vector3(-10.5f, 2.6f, 17f), "Theo's treehouse on the oak and its ladder"),
                new Shot("house-25-the-house-from-the-garden", new Vector3(7.5f, Eye, -5.6f), new Vector3(0f, 4.2f, 4f), "The house from the front garden"),
                // The hide-and-seek's moments.
                new Shot("seek-01-theo-counting", new Vector3(6.8f, G + Eye, 4.6f), new Vector3(4.55f, G + 1.0f, 1.8f), "Theo at the table, counting (he stands beside his chair: a counting pose with his face in his arms isn't drawn yet)", "theo:3.75,0,1.72,0"),
                new Shot("seek-02-theo-in-the-kitchen-from-the-pantry", new Vector3(6.55f, G + 1.0f, 10.2f), new Vector3(4.6f, G + 1.0f, 7.4f), "Hiding in the pantry: Theo searching the kitchen", "theo:5.6,0,8.7,20:seek;nolena"),
                new Shot("seek-03-theo-upstairs-from-the-wardrobe", new Vector3(-2.8f, Up + 1.0f, 4.6f), new Vector3(-3.6f, Up + 1.0f, 2.2f), "In Theo's wardrobe: Theo looking round his room", "theo:-3.8,3.2,2.2,160:seek"),
                new Shot("seek-04-memory-glowing-in-the-dark", new Vector3(2.3f, B + Eye, 2.9f), new Vector3(3.66f, B + 0.95f, 0.45f), "Elias's radio glowing on his workbench in the basement: a memory"),
                // Chapel Row: out of the dream's door in the rain; Lena at the tower's window.
                new Shot("out-01-chapel-row-door", U(h.exitDoor) + new Vector3(0f, Eye, 3f), U(h.exitDoor) + new Vector3(0f, 1.2f, 20f), "Out on Chapel Row: the path up the hill in the rain", "rain"),
                new Shot("out-02-lena-at-the-window", U(h.exitDoor) + new Vector3(0f, Eye, 2f), U(h.lenaWindow) + new Vector3(0f, 1.6f, 0f), "Looking back through the dream's door: Lena in the tower's round window, watching her go", "lena"),
            };
            // The escape: each room from its start, the corridor from its start and near its door.
            var e0 = U(HouseEscape.Origin);
            for (int i = 0; i < e.rooms.Count; i++)
            {
                var r = e.rooms[i];
                var at = e0 + new Vector3(0f, r.floorTop, r.s0 + 4f);
                string id = r.name.ToLowerInvariant().Replace(" ", "-").Replace(",", "").Replace("'", "");
                shots.Add(new Shot($"escape-{i + 1:00}-{id}", at + new Vector3(0f, Eye + 0.2f, -2f), at + new Vector3(0f, 1.2f, 16f), EscapeCaption(r.name), r.open ? "nightsky" : null));
            }
            var c = e.corridor;
            float cf = e.rooms[3].floorTop - (c.triggerS - 6f - c.startS) * c.slope;
            shots.Add(new Shot("escape-04b-the-corridor-near-its-door", e0 + new Vector3(0f, cf + Eye, c.triggerS - 6f), e0 + new Vector3(0f, cf + 1.0f, c.doorS), "The corridor's last two bays, the same as each other, and the lit red front door at its end (it stretches as she reaches it)"));
            var lh = e.rooms[e.rooms.Count - 1];
            shots.Add(new Shot("escape-08b-the-umbrella-by-the-door", e0 + new Vector3(0.2f, lh.floorTop + Eye, e.frontDoorS - 5f), e0 + new Vector3(1.9f, lh.floorTop + 0.6f, e.frontDoorS - 1f), "The last hall: the umbrella's place by the open red front door, warm light beyond it"));
            Scene(shots, dir);
            return dir;
        }

        static string EscapeCaption(string room)
        {
            switch (room)
            {
                case "The dining room tips": return "The escape: the dining room on its side (sage paper underfoot, the chairs and cabinet standing out of the old floor on the right, the pendants out of the left wall, the table sliding away)";
                case "The hall turns over": return "The escape: the oxblood hall turned over (photos underfoot and overhead, the console and coat stand on the wall, the doorframes ahead)";
                case "The stairs flatten": return "The escape: the stairs flattening into a runnered slide between banisters";
                case "Theo's room, upside down": return "The escape: Theo's room upside down (glow stars underfoot, his bed, desk and bookcase hanging overhead)";
                case "The kitchen, come round again": return "The escape: the kitchen on its other side (counters, fridge and stove on the left wall, the fridge on its side across the room ahead)";
                case "The attic, open to the stars": return "The escape: the attic's boards floating under a starry night, the family's things drifting";
                case "The last hall": return "The escape: the last hall the right way up (runner, console, photos, the open front door glowing)";
                case "The corridor": return "The escape: the corridor (butter trellis, walnut wainscot, the same threshold, door, photo and sconces every bay)";
            }
            return "The escape: " + room;
        }

        static void Scene(List<Shot> shots, string dir)
        {
            EditorSceneManager.OpenScene(DreamHouseBuilder.ScenePath, OpenSceneMode.Single);
            var game = UnityEngine.Object.FindAnyObjectByType<DreamHouseGame>();
            var main = Camera.main;
            var hide = new List<Renderer>();
            if (main != null) hide.AddRange(main.transform.root.GetComponentsInChildren<Renderer>(true));
            var go = new GameObject("Capture camera");
            var cam = go.AddComponent<Camera>();
            if (main != null) { cam.CopyFrom(main); main.enabled = false; }
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 4000f;   // Look's backdrop ring
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            var rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 4 });
            var log = new System.Text.StringBuilder();
            try
            {
                foreach (var r in hide) if (r != null) r.enabled = false;
                Render(cam, rt, null, dir);
                foreach (var s in shots)
                {
                    Apply(game, s.show, true);
                    go.transform.position = s.eye;
                    go.transform.rotation = Quaternion.LookRotation(s.look - s.eye, Vector3.up);
                    FaceCutOuts(s.eye);
                    Render(cam, rt, s.name, dir);
                    Apply(game, s.show, false);
                    log.AppendLine(s.name + ".png\t" + s.what);
                }
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(go);
            }
            File.WriteAllText(Path.Combine(dir, "shots.txt"), log.ToString());
        }

        /// <summary>A shot's setup: Theo placed (theo:x,y,z,yaw:count|seek), the night sky, the rain, Lena at the window.</summary>
        static void Apply(DreamHouseGame game, string show, bool on)
        {
            if (game == null || string.IsNullOrEmpty(show)) return;
            if (show.Contains(";"))
            {
                foreach (var one in show.Split(';')) Apply(game, one, on);
                return;
            }
            if (show == "nolena" && game.lena != null) { game.lena.gameObject.SetActive(!on); return; }
            if (show.StartsWith("theo:") && game.theo != null && on)
            {
                var parts = show.Substring(5).Split(':');
                var n = parts[0].Split(',');
                var p = new Vector3(float.Parse(n[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(n[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(n[2], System.Globalization.CultureInfo.InvariantCulture));
                float yaw = float.Parse(n[3], System.Globalization.CultureInfo.InvariantCulture);
                game.theo.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
                if (game.theo.figure != null) game.theo.figure.localPosition = new Vector3(0f, parts.Length > 1 && parts[1] == "count" ? -0.35f : 0f, 0f);
            }
            if (show == "nightsky" && game.nightSky != null) game.nightSky.SetActive(on);
            if (show == "lena" && game.lenaInWindow != null) game.lenaInWindow.SetActive(on);
            if (show == "rain" && game.umbrella != null && game.umbrella.rainFx != null)
            {
                if (on) { game.umbrella.rainFx.Simulate(2f, true, true); } else game.umbrella.rainFx.Clear(true);
            }
        }

        static void FaceCutOuts(Vector3 eye)
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<DreamCutoutView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var d = eye - c.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) c.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
            }
        }

        static void Render(Camera cam, RenderTexture rt, string name, string dir)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            if (name == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("JAMKIT_HOUSE_CAPTURE " + path);
        }
    }
}
