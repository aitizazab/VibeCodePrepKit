using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Skyline's player settings for drawing and logging (judge round 2, E 9/N5):
    /// <list type="bullet">
    /// <item><b>The quality level.</b> Round 2's build logged "Disabling TAA and STP because camera is stacked" 166 times: URP
    /// only asks for STP when the active pipeline asset says STP, and only Visiting Hours' levels ("VH High", "VH Low": 0.8 and
    /// 0.6 render scale, STP) do. So the player was on one of those while the editor (and the look) runs on "PC" (the likely
    /// cause, not verified without Unity: the Standalone default is index 1, and the player drops the Standalone-excluded
    /// "Mobile" level, so index 1 lands on "VH High"). The build then drew the arena at 80% resolution with a plain stretch
    /// (STP can't run under the viewmodel's camera stack), unlike the editor, and the perf run measured that. In a player, a
    /// Skyline scene now pins the level the look was
    /// made on (<see cref="DefaultLevel"/>, "PC": full 1080p, SMAA on the camera) before the scene wakes, and logs
    /// <c>SKYLINE_QUALITY ...</c>. <c>-skyline-quality &lt;name&gt;</c> picks another level (<c>keep</c>: leave it), so a
    /// perf run can compare.</item>
    /// <item><b>Log stack traces.</b> The project captures a script stack trace for every Debug.Log in a player
    /// (PlayerSettings: ScriptOnly), and the demo, the director and the bots log a <c>SKYLINE_</c> line at nearly every beat:
    /// each one walked the managed stack mid-fight. Plain logs now carry none in a Skyline player (warnings, errors and
    /// exceptions keep theirs).</item>
    /// </list>
    /// Not in the editor: Play mode there keeps the editor's own level (changing it would change the editor's setting).
    /// </summary>
    public static class SkylineQuality
    {
        public const string DefaultLevel = "PC";
        static bool _pinned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _pinned = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor) return;
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            EnsurePinned();
        }

        /// <summary>Pins once, in a player whose active scene is a Skyline scene (the perf probe calls it before it starts its
        /// pipeline-state trace, whichever start-up hook runs first).</summary>
        public static void EnsurePinned()
        {
            if (_pinned || Application.isEditor) return;
            if (IsSkyline(SceneManager.GetActiveScene().name)) Pin();
        }

        static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_pinned && IsSkyline(scene.name)) Pin();   // a Skyline scene reached from the shell (VhLookRig moves its shadow override over to the new asset itself)
        }

        static bool IsSkyline(string sceneName) => sceneName != null && sceneName.StartsWith("Skyline", StringComparison.Ordinal);

        /// <summary>Makes the drawing level <see cref="DefaultLevel"/> (or the command line's) and drops plain logs' stack traces.</summary>
        public static void Pin()
        {
            _pinned = true;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            string want = SkylineRunArgs.Value("-skyline-quality") ?? DefaultLevel;
            string before = LevelName;
            if (!string.Equals(want, "keep", StringComparison.OrdinalIgnoreCase))
            {
                int level = Array.IndexOf(QualitySettings.names, want);
                if (level < 0) Debug.LogWarning("SKYLINE_QUALITY no level \"" + want + "\"; kept " + before);
                else if (level != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(level, true);
            }
            Debug.Log("SKYLINE_QUALITY " + Describe() + (before != LevelName ? " (was " + before + ")" : ""));
        }

        /// <summary>The active quality level's name.</summary>
        public static string LevelName
        {
            get
            {
                var names = QualitySettings.names;
                int i = QualitySettings.GetQualityLevel();
                return i >= 0 && i < names.Length ? names[i] : "?";
            }
        }

        /// <summary>The URP asset drawing now (the level's, else the project default), or null.</summary>
        public static UniversalRenderPipelineAsset Asset => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        /// <summary>"level PC, pipeline PC_RPAsset, render scale 1, upscaling Auto, shadows 80 m x4".</summary>
        public static string Describe()
        {
            var a = Asset;
            if (a == null) return "level " + LevelName + ", pipeline none";
            return "level " + LevelName + ", pipeline " + a.name + ", render scale " + a.renderScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
                   ", upscaling " + a.upscalingFilter + ", shadows " + a.shadowDistance.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m x" + a.shadowCascadeCount;
        }
    }
}
