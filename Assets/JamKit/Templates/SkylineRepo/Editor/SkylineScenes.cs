using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>Builds the pipeline's scenes from code each pass (generated, not hand-edited): the arena (M1's one arena,
    /// first in the build so the frame-time probe measures it), the firing range (both guns' handling sets, recorded with
    /// audio) and the enemy gallery (each bot idle, alert and attacking). Every scene carries the SkylineSceneGuard that
    /// keeps Visiting Hours' GameRoot out.</summary>
    public static class SkylineScenes
    {
        public const string Folder = "Assets/JamKit/Templates/SkylineRepo/Generated";
        public const string Arena = Folder + "/SkylineArena.unity";
        public const string FiringRange = Folder + "/SkylineFiringRange.unity";
        public const string Gallery = Folder + "/SkylineGallery.unity";
        public static readonly string[] All = { Arena, FiringRange, Gallery };

        /// <summary>Floor 1 built from data (docs/SKYLINE-PLAN-FLOOR1.md Task B1): the Food Court and, once B2 has built it, the
        /// hotel lobby. Deliberately NOT in <see cref="All"/> (every directory test, pass script and the frame-time probe assume
        /// the one arena scene) until the switch-over task swaps it in as <see cref="Arena"/>.</summary>
        public const string Floor1 = Folder + "/SkylineFloor1.unity";

        /// <summary>Seconds stage B records each scene (its director's script fits inside; the arena: 180 s, so the demo goes the whole
        /// route: the warm-up, the three fights and the slide into the lift (judge round 1); the gallery: a 1 s lead-in and four segments of 12, 12, 12 and 14 s, about 52 s, or about 60 s if the drone's and gondola's fallback waits both run).</summary>
        public static int RecordSeconds(string scene) => scene == Arena ? 180 : scene == FiringRange ? 45 : 62;

        public static string[] BuildAll()
        {
            Directory.CreateDirectory(Folder);
            SkylineArenaBuilder.Build(Arena);
            SkylineRangeBuilder.Build(FiringRange);
            SkylineGalleryBuilder.Build(Gallery);
            AssetDatabase.Refresh();
            return (string[])All.Clone();
        }

        /// <summary>Builds <see cref="Floor1"/> from the floor data and returns its path (not part of <see cref="BuildAll"/>).</summary>
        public static string BuildFloor1()
        {
            Directory.CreateDirectory(Folder);
            SkylineArenaBuilder.BuildFloor1(Floor1);
            AssetDatabase.Refresh();
            return Floor1;
        }

        /// <summary>The one-off editor job that builds <see cref="Floor1"/>, lints it (the four scene rules plus
        /// <c>FloorDataMatch</c>) and writes <c>floor1-build.json</c> to <c>-skylineOut</c> ({ok, scene, lint:{count, problems},
        /// error}); then exits. <c>Unity.exe -batchmode -projectPath . -executeMethod
        /// JamKit.SkylineRepo.EditorTools.SkylineScenes.BuildFloor1FromCommandLine -skylineOut out/skyline/&lt;stamp&gt;</c></summary>
        public static void BuildFloor1FromCommandLine()
        {
            string outDir = SkylinePassBatch.Arg("-skylineOut") ?? Path.Combine("out", "skyline", "manual");
            string error = null;
            var lint = new System.Collections.Generic.List<string>();
            bool built = false;
            try
            {
                BuildFloor1();
                built = true;
                lint = SkylineLint.Run(Floor1);
            }
            catch (System.Exception e)
            {
                error = e.ToString();
            }
            int dataMatch = 0;
            foreach (var p in lint) if (p.StartsWith("FloorDataMatch", System.StringComparison.Ordinal)) dataMatch++;
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"ok\":").Append(built && error == null ? "true" : "false").Append(",\"scene\":").Append(SkylinePassBatch.Quote(Floor1));
            sb.Append(",\"lint\":{\"count\":").Append(lint.Count).Append(",\"floorDataMatch\":").Append(dataMatch).Append(",\"problems\":[");
            for (int i = 0; i < lint.Count; i++) sb.Append(i > 0 ? "," : "").Append(SkylinePassBatch.Quote(lint[i]));
            sb.Append("]},\"error\":").Append(error == null ? "null" : SkylinePassBatch.Quote(error)).Append('}');
            try
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "floor1-build.json"), sb.ToString());
            }
            catch (System.Exception e) { Debug.LogError("SKYLINE_FLOOR could not write floor1-build.json: " + e.Message); }
            Debug.Log("SKYLINE_FLOOR1 " + (built && error == null ? "OK" : "FAILED") + " lint=" + lint.Count + " floorDataMatch=" + dataMatch);
            EditorApplication.Exit(built && error == null ? 0 : 1);
        }

        /// <summary>A new empty scene with the guard that keeps Visiting Hours' GameRoot out (see SkylineSceneGuard).</summary>
        public static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("SkylineSceneGuard").AddComponent<JamKit.SkylineRepo.SkylineSceneGuard>();
            return scene;
        }
    }
}
