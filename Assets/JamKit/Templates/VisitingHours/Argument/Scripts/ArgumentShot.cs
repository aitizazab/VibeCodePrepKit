using System.Globalization;
using System.IO;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Argument
{
    /// <summary>
    /// Frames of the battle sprite's boil from a build, for a review page (like the title's -title-shot):
    /// <c>VisitingHours.exe -argument-shot "C:/…/boil" [-argument-expression angry] [-argument-boil strong] [-argument-react]
    /// [-argument-frames 12] [-argument-fps 12]</c>
    /// jumps to the argument prototype (Story, so nothing interrupts), waits until the debate is up,
    /// holds Lena on the expression with the boil preset (its name and values shown under her), saves the frames as
    /// <c>boil_&lt;preset&gt;_&lt;expression&gt;_NN.png</c> in the folder, then quits. Does nothing without the flag.
    /// </summary>
    public sealed class ArgumentShot : MonoBehaviour
    {
        public const string ShotFlag = "-argument-shot", ExpressionFlag = "-argument-expression", BoilFlag = "-argument-boil",
            ReactFlag = "-argument-react", FramesFlag = "-argument-frames", FpsFlag = "-argument-fps";

        /// <summary>What the flags asked for.</summary>
        public struct Request
        {
            public string folder;
            public string expression;
            public string preset;
            public bool react;
            public int frames;
            public float fps;
        }

        Request _request;
        int _phase, _taken;
        float _wait, _next;
        string _first;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Parse(System.Environment.GetCommandLineArgs(), out var request)) return;
            var go = new GameObject("[Argument shot]");
            DontDestroyOnLoad(go);
            go.AddComponent<ArgumentShot>()._request = request;
        }

        /// <summary>The flags (false without <see cref="ShotFlag"/>). Defaults: the antagonistic pose, the current
        /// preset, no reacting, 12 frames at 12 fps (one second).</summary>
        public static bool Parse(string[] args, out Request request)
        {
            request = new Request { expression = ArgumentArt.Opening, preset = null, frames = 12, fps = 12f };
            if (args == null) return false;
            bool found = false;
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1].Trim('"') : null;
                switch (args[i])
                {
                    case ShotFlag: found = true; request.folder = next ?? "argument-boil"; break;
                    case ExpressionFlag: if (next != null) request.expression = next.ToLowerInvariant(); break;
                    case BoilFlag: if (next != null) request.preset = next; break;
                    case ReactFlag: request.react = true; break;
                    case FramesFlag: if (next != null && int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) request.frames = Mathf.Clamp(n, 1, 240); break;
                    case FpsFlag: if (next != null && float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) request.fps = Mathf.Clamp(f, 1f, 60f); break;
                }
            }
            return found;
        }

        /// <summary>The file for frame <paramref name="index"/>: boil_gentle_angry_03.png.</summary>
        public static string FrameName(string preset, string expression, int index) =>
            "boil_" + Slug(preset) + "_" + Slug(expression) + "_" + index.ToString("00", CultureInfo.InvariantCulture) + ".png";

        /// <summary>The frames' preset part: the preset in effect ("mood-" and her mood's preset when following it).</summary>
        static string ShotName => ArgumentBoil.Reacting ? "mood-" + ArgumentBoil.For(ArgumentHost.ExpressionOverride ?? ArgumentArt.Opening).name : ArgumentBoil.Preset.name;

        static string Slug(string s)
        {
            var b = new System.Text.StringBuilder();
            foreach (char c in (s ?? "").ToLowerInvariant()) b.Append(char.IsLetterOrDigit(c) ? c : '-');
            return b.ToString().Trim('-');
        }

        void Start()
        {
            if (!Path.IsPathRooted(_request.folder)) _request.folder = Path.Combine(Application.persistentDataPath, _request.folder);
            ArgumentBoil.SetReacting(true);
            if (_request.preset != null && !ArgumentBoil.Use(_request.preset))
                Debug.LogWarning("JAMKIT_ARGUMENT_SHOT unknown boil preset \"" + _request.preset + "\"; following her mood");
            if (_request.react) ArgumentBoil.SetReacting(true);
            Debug.Log("JAMKIT_ARGUMENT_SHOT " + _request.folder + " (" + _request.expression + ", " + ArgumentBoil.Mode
                + ", " + _request.frames + " frames at " + _request.fps + " fps)");
            _wait = 90f;
            _next = 1f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _wait -= dt;
            if (_wait <= 0f && _phase < 3)
            {
                Debug.Log("JAMKIT_ARGUMENT_SHOT_FAIL timed out (phase " + _phase + ")");
                _phase = 4;
                Application.Quit();
                return;
            }
            switch (_phase)
            {
                case 0:
                    // Into the prototype's Story battle (retrying until the shell is ready).
                    _next -= dt;
                    if (_next > 0f || !GameRoot.Exists || GameRoot.IsLoadingScene) return;
                    var r = PresenterMode.Jump(ArgumentPrototype.Scene, ArgumentPrototype.Story);
                    if (r == JumpResult.Pending || r == JumpResult.Jumped) _phase = 1;
                    else _next = 1f;
                    return;
                case 1:
                    // The battle settled on its premise list (the intro and the opening pressure phase over).
                    var host = ArgumentHost.Current;
                    if (host == null || !ArgumentHost.Running || host.State != BattleState.Debate || host.Stage == null || host.Stage.Busy || host.Engine.InPressure) return;
                    ArgumentHost.OverrideExpression(_request.expression);
                    host.Stage.ShowBoilLabel(true);
                    Directory.CreateDirectory(_request.folder);
                    _next = 0.8f;
                    _phase = 2;
                    return;
                case 2:
                    _next -= dt;
                    if (_next > 0f) return;
                    string path = Path.Combine(_request.folder, FrameName(ShotName, _request.expression, _taken));
                    if (_taken == 0) _first = path;
                    if (File.Exists(path)) File.Delete(path);
                    ScreenCapture.CaptureScreenshot(path);
                    _taken++;
                    _next = 1f / _request.fps;
                    if (_taken < _request.frames) return;
                    _wait = 10f;
                    _phase = 3;
                    return;
                case 3:
                    string last = Path.Combine(_request.folder, FrameName(ShotName, _request.expression, _request.frames - 1));
                    if (!File.Exists(last) && _wait > 0f) return;
                    Debug.Log(File.Exists(last) ? "JAMKIT_ARGUMENT_SHOT_OK " + _request.folder + " " + _taken + " frames from " + _first : "JAMKIT_ARGUMENT_SHOT_FAIL no frames in " + _request.folder);
                    _phase = 4;
                    ArgumentHost.OverrideExpression(null);
                    Application.Quit();
                    return;
            }
        }
    }
}
