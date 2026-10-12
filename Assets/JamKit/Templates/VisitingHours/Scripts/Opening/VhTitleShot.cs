using System.IO;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// A screenshot of the title from a build, for checking the table against the creator's sketch:
    /// <c>VisitingHours.exe -title-shot "C:/…/title-normal.png" [-title-variant normal|despair]</c> skips the opening, waits
    /// for the title table (lit and still), saves the picture and quits. "despair" shows the Despair route's quit piece (the
    /// pill bottle) without touching the save. Lives on the GameRoot (added by <see cref="VhBoot"/>); does nothing without the flag.
    /// </summary>
    public sealed class VhTitleShot : MonoBehaviour
    {
        public const string ShotFlag = "-title-shot", VariantFlag = "-title-variant";

        string _path;
        float _settle = -1f;
        int _phase;

        /// <summary>The flags: the picture's path (null without <see cref="ShotFlag"/>) and the quit piece's variant (null = the save's).</summary>
        public static bool Parse(string[] args, out string path, out string variant)
        {
            path = variant = null;
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == ShotFlag)
                    path = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1].Trim('"') : "title-shot.png";
                else if (args[i] == VariantFlag && i + 1 < args.Length) variant = args[i + 1].Trim('"').ToLowerInvariant();
            }
            return path != null;
        }

        void Awake()
        {
            if (!Parse(System.Environment.GetCommandLineArgs(), out _path, out var variant))
            {
                enabled = false;
                return;
            }
            if (!Path.IsPathRooted(_path)) _path = Path.Combine(Application.persistentDataPath, _path);
            VhBoot.SkipOpening = true;
            if (variant == "normal" || variant == "despair") VhStageBuilder.QuitVariantOverride = variant;
            Debug.Log("JAMKIT_VH title shot: " + _path + " (" + (variant ?? "the save's route") + ")");
        }

        void Update()
        {
            if (!GameRoot.Exists) return;
            var stage = VhBoot.Stage;
            switch (_phase)
            {
                case 0:
                    // The title, its table up and lit.
                    if (GameRoot.Flow.Current != GameState.Title || GameRoot.IsLoadingScene || stage == null || !stage.Interactive) return;
                    _settle = 1.5f;
                    _phase = 1;
                    return;
                case 1:
                    // No hover lift in the picture; let the fades and the lights settle.
                    if (stage != null && stage.table != null) stage.table.interactive = false;
                    _settle -= Time.unscaledDeltaTime;
                    if (_settle > 0f) return;
                    Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
                    if (File.Exists(_path)) File.Delete(_path);
                    ScreenCapture.CaptureScreenshot(_path);
                    _settle = 8f;
                    _phase = 2;
                    return;
                case 2:
                    _settle -= Time.unscaledDeltaTime;
                    if (!File.Exists(_path) && _settle > 0f) return;
                    Debug.Log(File.Exists(_path) ? "JAMKIT_VH_TITLE_SHOT_OK " + _path : "JAMKIT_VH_TITLE_SHOT_FAIL " + _path);
                    _phase = 3;
                    Application.Quit();
                    return;
            }
        }
    }
}
