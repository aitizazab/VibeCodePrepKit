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

        /// <summary>Seconds stage B records each scene (its director's script fits inside; the gallery: a 1 s lead-in and four segments of 12, 12, 12 and 14 s, about 52 s, or about 60 s if the drone's and gondola's fallback waits both run).</summary>
        public static int RecordSeconds(string scene) => scene == Arena ? 60 : scene == FiringRange ? 45 : 62;

        public static string[] BuildAll()
        {
            Directory.CreateDirectory(Folder);
            SkylineArenaBuilder.Build(Arena);
            SkylineRangeBuilder.Build(FiringRange);
            SkylineGalleryBuilder.Build(Gallery);
            AssetDatabase.Refresh();
            return (string[])All.Clone();
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
