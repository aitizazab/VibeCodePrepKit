using System.Collections;
using System.Collections.Generic;
using System.IO;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Stills of the waking days for review (the creator: "the rooms should look real"), from a build:
    /// <c>VibeJamKit.exe -ward-shot "C:/…/folder" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0</c>. It skips the
    /// opening, starts the Visiting Hours scene, and takes the UI-free stills listed in <see cref="Framings"/>: the station,
    /// the corridor both ways, room 14 on Day 1 and on Day 2, and the booth with Mags. Then it takes one first-person still
    /// per examinable on each day, from the approach the smoke run uses and looking at the thing. It writes
    /// <c>shots.txt</c> (one line per still: file, place, where from) and quits. Lives on the GameRoot (added by
    /// <see cref="VhBoot"/>) and does nothing without the flag.
    /// </summary>
    public sealed class VhWardShot : MonoBehaviour
    {
        public const string ShotFlag = "-ward-shot";

        /// <summary>A framed still: the place, where Nadia stands (x, z), her yaw and pitch (degrees, pitch + is down).</summary>
        public struct Framing
        {
            public string name;
            public SliceArea area;
            public Vector2 at;
            public float yaw, pitch;

            public Framing(string name, SliceArea area, float x, float z, float yaw, float pitch)
            {
                this.name = name;
                this.area = area;
                at = new Vector2(x, z);
                this.yaw = yaw;
                this.pitch = pitch;
            }
        }

        public static readonly Framing[] Framings =
        {
            new Framing("01-corridor-from-the-lifts", SliceArea.Hospital, 0f, 0.7f, 0f, 2f),
            new Framing("02-corridor-from-the-stairwell", SliceArea.Hospital, 0.3f, 25.2f, 180f, 2f),
            new Framing("03-station", SliceArea.Hospital, -0.6f, 4.4f, -65f, 10f),
            new Framing("04-station-close", SliceArea.Hospital, -2.3f, 6.9f, -110f, 22f),
            new Framing("05-room14-day1-from-the-door", SliceArea.Hospital, 2.7f, 18.6f, 40f, 12f),
            new Framing("06-room14-day1-from-the-window", SliceArea.Hospital, 8.3f, 20.4f, -75f, 14f),
            new Framing("07-room14-day1-the-bed", SliceArea.Hospital, 6.0f, 19.4f, 0f, 24f),
            new Framing("08-room14-day2-from-the-door", SliceArea.HospitalDay2, 2.7f, 18.6f, 40f, 12f),
            new Framing("09-room14-day2-from-the-window", SliceArea.HospitalDay2, 8.3f, 20.4f, -75f, 14f),
            new Framing("10-corridor-day2-the-visitor", SliceArea.HospitalDay2, -0.6f, 12.5f, 20f, 4f),
            new Framing("11-booth-mags", SliceArea.Booth, -0.8f, 1.0f, 62f, 6f),
            new Framing("12-booth-the-chair", SliceArea.Booth, 0.9f, 1.2f, -35f, 10f),
            new Framing("13-booth-the-door", SliceArea.Booth, 0.4f, 4.6f, 200f, 4f),
        };

        string _folder;
        readonly List<string> _lines = new List<string>();
        int _taken, _failed;

        /// <summary>The flag: the folder the stills go to (null without <see cref="ShotFlag"/>).</summary>
        public static bool Parse(string[] args, out string folder)
        {
            folder = null;
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == ShotFlag)
                    folder = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1].Trim('"') : "ward-shots";
            return folder != null;
        }

        void Awake()
        {
            if (!Parse(System.Environment.GetCommandLineArgs(), out _folder))
            {
                enabled = false;
                return;
            }
            if (!Path.IsPathRooted(_folder)) _folder = Path.Combine(Application.persistentDataPath, _folder);
            VhBoot.SkipOpening = true;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            Debug.Log("JAMKIT_VH ward shots: " + _folder);
        }

        IEnumerator Start()
        {
            if (!enabled) yield break;
            // The title, then straight into the Visiting Hours scene (Day 1 from the start).
            yield return Until(() => GameRoot.Exists && GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene, 60f);
            yield return Real(1f);
            GameRoot.Save.Data.seenHowToPlay = true;
            VisitingHoursGame.FastTransitions = true;
            string scene = VhBoot.Config != null ? VhBoot.Config.sceneName : "VisitingHours";
            if (!GameRoot.Instance.StartRunAt(scene)) { Finish("couldn't start " + scene); yield break; }
            yield return Until(() => !GameRoot.IsLoadingScene && VisitingHoursGame.Current != null && VisitingHoursGame.Current.CurrentMode == VisitingHoursGame.Mode.Explore, 60f);
            var game = VisitingHoursGame.Current;
            if (game == null) { Finish("the Visiting Hours scene didn't start"); yield break; }
            Directory.CreateDirectory(_folder);

            SliceArea built = (SliceArea)(-1);
            foreach (var f in Framings)
            {
                if (built != f.area) { yield return Enter(game, f.area); built = f.area; }
                Place(game, new Vector3(f.at.x, 0.05f, f.at.y), f.yaw, f.pitch);
                HideUi(true);
                yield return Shoot(f.name, f.area + " from (" + f.at.x + ", " + f.at.y + ") yaw " + f.yaw + " pitch " + f.pitch);
            }

            // One still per examinable on each day, from its approach, looking at it (the HUD shown: its prompt names the thing).
            HideUi(false);
            foreach (var area in new[] { SliceArea.Hospital, SliceArea.HospitalDay2, SliceArea.Booth })
            {
                yield return Enter(game, area);
                var ids = new List<string>();
                foreach (var s in game.BuiltArea.spots) ids.Add(s.def.id);
                foreach (var id in ids)
                {
                    game.StandAt(id);
                    yield return null;
                    var spot = game.BuiltArea.Spot(id);
                    var eye = game.player.EyePosition;
                    var to = spot.position - eye;
                    float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                    Place(game, game.player.transform.position, game.player.Yaw, Mathf.Clamp(pitch, -40f, 60f));
                    string day = area == SliceArea.HospitalDay2 ? "day2" : area == SliceArea.Booth ? "booth" : "day1";
                    yield return Shoot("spot-" + day + "-" + id, area + ": " + spot.def.label + " (prompt: " + (game.Target != null ? game.Target.def.id : "none") + ")");
                }
            }

            File.WriteAllLines(Path.Combine(_folder, "shots.txt"), _lines);
            Finish(null);
        }

        IEnumerator Enter(VisitingHoursGame game, SliceArea area)
        {
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            game.DebugJumpTo(area);
            yield return Real(0.4f);
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
        }

        static void Place(VisitingHoursGame game, Vector3 at, float yaw, float pitch)
        {
            var p = game.player;
            p.Teleport(at);
            p.Look(0f, pitch - p.Pitch);
            p.FaceYaw(yaw);
        }

        IEnumerator Shoot(string name, string what)
        {
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            yield return Real(0.6f);
            string path = Path.Combine(_folder, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float end = Time.realtimeSinceStartup + 6f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < end) yield return null;
            if (File.Exists(path)) { _taken++; _lines.Add(name + ".png\t" + what); }
            else { _failed++; Debug.LogWarning("JAMKIT_VH ward shot missing: " + path); }
        }

        /// <summary>The framed stills are of the rooms alone: every UI document hidden (the HUD, the controls row, the
        /// prompt), shown again for the per-thing stills.</summary>
        static void HideUi(bool hide)
        {
            foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if (doc != null && doc.rootVisualElement != null) doc.rootVisualElement.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void Finish(string problem)
        {
            if (problem != null) Debug.Log("JAMKIT_VH_WARD_SHOT_FAIL " + problem);
            else Debug.Log((_failed == 0 ? "JAMKIT_VH_WARD_SHOT_OK " : "JAMKIT_VH_WARD_SHOT_FAIL ") + _taken + " stills, " + _failed + " missing, in " + _folder);
            Application.Quit();
        }

        static IEnumerator Until(System.Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Real(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
