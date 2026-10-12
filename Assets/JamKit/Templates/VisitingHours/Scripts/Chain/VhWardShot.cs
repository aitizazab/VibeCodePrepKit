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
    /// Stills of the waking days for the judge (docs/redesign/day.md, "Captures"), from a built player only (never a windowed
    /// editor):
    /// <c>VisitingHours.exe -ward-shot "C:/…/folder" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0</c>. It
    /// skips the opening, starts the Visiting Hours scene and takes, at 1920x1080 and at her eye height:
    /// <list type="number">
    /// <item>the framed stills in <see cref="Framings"/> (every area of Ward 4 West on Day 1 and Day 2, and the booth), UI hidden;</item>
    /// <item>the bedside, frame by frame (sitting, her hand coming in, the topics, his fingers, the second topic, his heart on
    /// the monitor, "E  Stand up", rising, standing), the dialogue shown;</item>
    /// <item>one still per examinable on each day, from where she stands to look at it (the prompt shown).</item>
    /// </list>
    /// It writes <c>shots.txt</c> (one line per still: file, what) and quits. Lives on the GameRoot (added by
    /// <see cref="VhBoot"/>) and does nothing without the flag. The recorded run of a whole day is <see cref="VhDayRun"/>.
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
            new Framing("01-lobby-arriving-from-the-lifts", SliceArea.Hospital, 32f, 3.4f, 0f, 2f),
            new Framing("02-corridor-the-sun-from-the-west-window", SliceArea.Hospital, 24f, 12.5f, -90f, 2f),
            new Framing("03-corridor-looking-east", SliceArea.Hospital, 9f, 12.4f, 90f, 2f),
            new Framing("04-station-rafael-at-the-counter", SliceArea.Hospital, 21.3f, 13.2f, 180f, 8f),
            new Framing("05-station-inside-the-round-board", SliceArea.Hospital, 24.2f, 8.6f, -140f, 6f),
            new Framing("06-room14-from-the-door", SliceArea.Hospital, 20.4f, 15.3f, 35f, 12f),
            new Framing("07-room14-from-the-window-corner", SliceArea.Hospital, 26.2f, 16.6f, -38f, 12f),
            new Framing("08-room12-mrs-okafor", SliceArea.Hospital, 8.0f, 15.6f, 28f, 10f),
            new Framing("09-staff-room-lockers-and-kitchen", SliceArea.Hospital, 14.2f, 9.9f, -125f, 8f),
            new Framing("10-quiet-room-candles-and-book", SliceArea.Hospital, 5.2f, 9.7f, -118f, 8f),
            new Framing("11-family-room-june-and-the-tv", SliceArea.Hospital, 38.4f, 3.4f, 40f, 4f),
            new Framing("12-lobby-the-directory", SliceArea.Hospital, 30.8f, 7.6f, -100f, 0f),
            new Framing("13-day2-room16-mr-brandt", SliceArea.HospitalDay2, 34.3f, 15.5f, 38f, 10f),
            new Framing("14-day2-family-room-the-woman-from-lebaget", SliceArea.HospitalDay2, 35.6f, 5.4f, 78f, 2f),
            new Framing("15-day2-staff-room-rafael-on-his-break", SliceArea.HospitalDay2, 14.2f, 9.9f, -150f, 10f),
            new Framing("16-day2-room14-the-new-bag", SliceArea.HospitalDay2, 20.2f, 16.2f, 20f, 12f),
            new Framing("17-booth-mags", SliceArea.Booth, -0.8f, 1.0f, 62f, 6f),
            new Framing("18-booth-the-chair", SliceArea.Booth, 0.9f, 1.2f, -35f, 10f),
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
            // A player flag only: never a windowed editor (one took focus from the creator, 2026-10-12).
            if (Application.isEditor)
            {
                Debug.LogWarning("JAMKIT_VH ward shots: " + ShotFlag + " is a player flag (run the built VisitingHours.exe); ignored in the editor");
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
                yield return Real(0.5f);
                yield return Shoot(f.name, f.area + " from (" + f.at.x + ", " + f.at.y + ") yaw " + f.yaw + " pitch " + f.pitch);
            }

            // The bedside, frame by frame (Day 1: she found Walt's card, the creek photo, June, the police report).
            HideUi(false);
            yield return Bedside(game);

            // One still per examinable on each day, from her approach, looking at it (the HUD shown: its prompt names the thing).
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

        /// <summary>The bedside on Day 1, its frames.</summary>
        IEnumerator Bedside(VisitingHoursGame game)
        {
            yield return Enter(game, SliceArea.Hospital);
            game.DebugCompleteRound(SliceContent.FactEffects, DayContent.FactLockerPhoto, DayContent.FactJune, SliceContent.FactPoliceReport);
            game.StandAt("sit1");
            yield return Real(0.3f);
            VisitingHoursGame.AutoRise = false;
            game.StartBedside();
            yield return Real(VhBedside.SitSeconds * 0.5f);
            yield return Shoot("bed-01-sitting-down", "the bedside: she sits (the view lowering to the chair)");
            yield return Until(() => game.Bedside != null && game.Bedside.Current == VhBedside.Phase.Reaching, 5f);
            yield return Real(VhBedside.ReachSeconds * 0.55f);
            yield return Shoot("bed-02-her-hand-coming-in", "the bedside: her hand comes into frame to take his");
            yield return Until(() => GameRoot.Dialogue.ChoiceCount > 0, 12f, true);
            yield return Real(0.6f);
            yield return Shoot("bed-03-what-to-tell-him", "the bedside: the topics her finds opened (choices)");
            Choose("Walt");
            yield return Until(() => GameRoot.Dialogue.IsActive && (GameRoot.Dialogue.CurrentLine ?? "").Contains("ledger"), 12f, true);
            yield return Real(1.4f);
            yield return Shoot("bed-04-his-fingers-move", "the bedside: \"...he'd write it in that ledger\": his fingers move against hers");
            yield return Until(() => GameRoot.Dialogue.ChoiceCount > 0, 15f, true);
            yield return Real(0.6f);
            yield return Shoot("bed-05-one-more-thing", "the bedside: one more thing to tell him (two topics a day)");
            Choose("creek");
            yield return Until(() => GameRoot.Dialogue.IsActive && (GameRoot.Dialogue.CurrentLine ?? "").Contains("jumped it"), 12f, true);
            yield return Real(4f);
            float rate = game.BuiltArea.patientMonitor != null ? game.BuiltArea.patientMonitor.Rate : 0f;
            yield return Shoot("bed-06-his-heart-climbs", "the bedside: the creek; his heart up on the monitor (" + rate.ToString("0") + " bpm)");
            yield return Until(() => game.Bedside != null && game.Bedside.Current == VhBedside.Phase.Ready && !GameRoot.Dialogue.IsActive, 20f, true);
            yield return Real(0.8f);
            yield return Shoot("bed-07-e-stand-up", "the bedside: \"E  Stand up\"");
            game.RequestRise();
            yield return Until(() => game.Bedside != null && game.Bedside.Current == VhBedside.Phase.Rising, 5f);
            yield return Real(0.6f);
            yield return Shoot("bed-08-rising", "the bedside: she lets go and stands");
            yield return Until(() => game.Bedside == null, 6f);
            yield return Real(0.5f);
            yield return Shoot("bed-09-standing-again", "the bedside: standing, the room in focus again; told " + string.Join(", ", game.ToldToday));
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
        }

        static void Choose(string words)
        {
            var d = GameRoot.Dialogue;
            for (int i = 0; i < d.ChoiceCount; i++)
                if (d.Choices[i].Contains(words)) { d.Choose(i); return; }
            if (d.ChoiceCount > 0) d.Choose(0);
        }

        IEnumerator Enter(VisitingHoursGame game, SliceArea area)
        {
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            game.DebugJumpTo(area);
            yield return Real(0.6f);
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
            yield return Real(0.25f);
            string path = Path.Combine(_folder, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float end = Time.realtimeSinceStartup + 6f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < end) yield return null;
            if (File.Exists(path)) { _taken++; _lines.Add(name + ".png\t" + what); }
            else { _failed++; Debug.LogWarning("JAMKIT_VH ward shot missing: " + path); }
        }

        /// <summary>The framed stills are of the rooms alone: every UI document hidden, shown again for the rest.</summary>
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

        /// <summary>Waits for <paramref name="condition"/>; <paramref name="read"/>: meanwhile reads the dialogue through at a
        /// reader's pace (a line finished typing, a moment, the next), never choosing.</summary>
        static IEnumerator Until(System.Func<bool> condition, float timeout, bool read = false)
        {
            float end = Time.realtimeSinceStartup + timeout;
            float shown = -1f;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                if (read)
                {
                    var d = GameRoot.Dialogue;
                    if (d.IsActive && d.ChoiceCount == 0 && !d.IsTyping)
                    {
                        if (shown < 0f) shown = Time.realtimeSinceStartup;
                        if (Time.realtimeSinceStartup - shown > 0.9f) { d.Advance(); shown = -1f; }
                    }
                    else shown = -1f;
                }
                yield return null;
            }
        }

        static IEnumerator Real(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
