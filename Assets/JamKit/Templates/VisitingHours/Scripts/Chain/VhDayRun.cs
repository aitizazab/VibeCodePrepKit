using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// A recorded autopilot run of a waking day for the judge (docs/redesign/day.md, "Captures"):
    /// <c>VisitingHours.exe -day-run "C:/…/folder" [-day-run-day 2] -screen-width 1920 -screen-height 1080 -screen-fullscreen 0</c>.
    /// It plays the day the way a player would, at real speed: walks the ward on the real controller (collisions on; the
    /// sticks are synthetic) along the corridor and through the doorways, turns to each thing, presses E, reads every line
    /// at a reader's pace, makes the round's choices (the right ones, with one slip on Day 2 to show the catch), sits with
    /// Elias and tells him two things, stands, takes the stairs and plays the booth through to the dive. A frame every 0.5
    /// s (JPG) goes to the folder with <c>run.txt</c> (what happened when) and it quits. Lives on the GameRoot (added by
    /// <see cref="VhBoot"/>); does nothing without the flag.
    /// </summary>
    public sealed class VhDayRun : MonoBehaviour
    {
        public const string Flag = "-day-run", DayFlag = "-day-run-day";
        const float FrameEvery = 0.5f, WalkTurnDegrees = 220f;

        static readonly string[] Day1 = { "rafael", "round_board", "wristband", "vitals", "chart", "effects", "photo", "bouquet", "okafor", "police_report", "locker", "rota", "june", "tv", "remembrance_book", "candles", "west_window", "sit1", "stairwell" };
        static readonly string[] Day2 = { "rafael2", "round_board", "brandt_band", "brandt", "okafor_chart", "okafor", "round2", "vitals2", "visitor", "remembrance_book", "sit", "stairwell2" };

        string _folder;
        int _day = 1, _frames;
        float _nextFrame, _start;
        bool _recording;
        readonly StringBuilder _log = new StringBuilder();
        SyntheticInput _pad;
        VisitingHoursGame _game;

        public static bool Parse(string[] args, out string folder, out int day)
        {
            folder = null;
            day = 1;
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == Flag) folder = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1].Trim('"') : "day-run";
                if (args[i] == DayFlag && i + 1 < args.Length && int.TryParse(args[i + 1], out int d)) day = d == 2 ? 2 : 1;
            }
            return folder != null;
        }

        void Awake()
        {
            if (!Parse(System.Environment.GetCommandLineArgs(), out _folder, out _day))
            {
                enabled = false;
                return;
            }
            // A player flag only: never a windowed editor (one took focus from the creator, 2026-10-12).
            if (Application.isEditor)
            {
                Debug.LogWarning("JAMKIT_VH day run: " + Flag + " is a player flag (run the built VisitingHours.exe); ignored in the editor");
                enabled = false;
                return;
            }
            if (!Path.IsPathRooted(_folder)) _folder = Path.Combine(Application.persistentDataPath, _folder);
            VhBoot.SkipOpening = true;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            Debug.Log("JAMKIT_VH day run (day " + _day + "): " + _folder);
        }

        void Say(string what)
        {
            string line = (Time.realtimeSinceStartup - _start).ToString("000.0") + "s  frame " + _frames.ToString("0000") + "  " + what;
            _log.AppendLine(line);
            Debug.Log("JAMKIT_VH_DAY_RUN " + line);
        }

        IEnumerator Start()
        {
            if (!enabled) yield break;
            yield return Until(() => GameRoot.Exists && GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene, 60f);
            yield return Real(1f);
            GameRoot.Save.Data.seenHowToPlay = true;
            Directory.CreateDirectory(_folder);
            _start = Time.realtimeSinceStartup;
            _recording = true;
            if (_day == 2)
            {
                if (VhChainRunner.Instance != null) VhChainRunner.Instance.JumpTo(ChainSegment.Day2);
            }
            else
            {
                string scene = VhBoot.Config != null ? VhBoot.Config.sceneName : "VisitingHours";
                GameRoot.Instance.StartRunAt(scene);
            }
            yield return Until(() => !GameRoot.IsLoadingScene && VisitingHoursGame.Current != null && VisitingHoursGame.Current.CurrentMode == VisitingHoursGame.Mode.Explore, 60f);
            _game = VisitingHoursGame.Current;
            if (_game == null) { Finish("the Visiting Hours scene didn't start"); yield break; }
            _pad = new SyntheticInput();
            GameRoot.Input.Synthetic = _pad;
            Say("day " + _day + " starts in " + _game.Area);
            yield return Read(30f);

            foreach (var id in _day == 2 ? Day2 : Day1)
            {
                var spot = _game.BuiltArea != null ? _game.BuiltArea.Spot(id) : null;
                if (spot == null) { Say("no " + id + " here; skipped"); continue; }
                yield return WalkTo(VisitingHoursGame.Approach(id, spot.position));
                yield return TurnTo(spot.position);
                yield return Real(0.4f);
                Say("E on " + spot.def.label + (_game.Target != null && _game.Target.def.id == id ? "" : " (the target was " + (_game.Target != null ? _game.Target.def.id : "nothing") + ")"));
                if (_game.Target != null && _game.Target.def.id == id) _pad.Tap(GameRoot.Input.Interact);
                else _game.Examine(id);
                yield return Real(0.3f);
                if (_game.CurrentMode == VisitingHoursGame.Mode.Bedside) yield return Bedside();
                else yield return Read(60f);
                if (id.StartsWith("stairwell")) break;
            }

            // The booth: the creator's scene with Mags, the journal (closed after a look), then the dive.
            yield return Until(() => _game == null || _game.Area == SliceArea.Booth || _game.Area == SliceArea.BoothDay2, 20f);
            if (_game != null && (_game.Area == SliceArea.Booth || _game.Area == SliceArea.BoothDay2))
            {
                Say("the booth, 11 PM");
                float end = Time.realtimeSinceStartup + 120f;
                while (Time.realtimeSinceStartup < end && _game != null && _game.CurrentMode != VisitingHoursGame.Mode.Transition)
                {
                    if (_game.JournalScreen != null && _game.JournalScreen.IsOpen)
                    {
                        Say("the journal, open");
                        yield return Real(3f);
                        _game.JournalScreen.Close();
                    }
                    yield return ReadStep();
                }
                Say("the dive");
                yield return Real(3f);
            }
            Finish(null);
        }

        // ---------- Walking the ward ----------

        IEnumerator WalkTo(Vector3 target)
        {
            var path = Route(_game.player.transform.position, target);
            foreach (var p in path)
            {
                float stuckClock = 0f;
                var last = _game.player.transform.position;
                while (true)
                {
                    var me = _game.player.transform.position;
                    var to = new Vector3(p.x - me.x, 0f, p.z - me.z);
                    if (to.magnitude < 0.3f) break;
                    float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    float yaw = Mathf.MoveTowardsAngle(_game.player.Yaw, want, WalkTurnDegrees * Time.deltaTime);
                    _game.player.Look(0f, Mathf.MoveTowards(_game.player.Pitch, 4f, 30f * Time.deltaTime) - _game.player.Pitch);
                    _game.player.FaceYaw(yaw);
                    _pad.move = Mathf.Abs(Mathf.DeltaAngle(yaw, want)) < 50f ? new Vector2(0f, 1f) : new Vector2(0f, 0.25f);
                    stuckClock += Time.deltaTime;
                    if (stuckClock > 1.5f)
                    {
                        if ((me - last).magnitude < 0.08f)
                        {
                            Say("stuck near (" + me.x.ToString("0.0") + ", " + me.z.ToString("0.0") + "); stepped to the next point");
                            _game.player.Teleport(new Vector3(p.x, me.y, p.z));
                        }
                        stuckClock = 0f;
                        last = me;
                    }
                    yield return null;
                }
            }
            _pad.move = Vector2.zero;
        }

        IEnumerator TurnTo(Vector3 at)
        {
            float end = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < end)
            {
                var eye = _game.player.EyePosition;
                var to = at - eye;
                float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -40f, 60f);
                float y = Mathf.MoveTowardsAngle(_game.player.Yaw, yaw, 160f * Time.deltaTime);
                float p = Mathf.MoveTowards(_game.player.Pitch, pitch, 90f * Time.deltaTime);
                _game.player.Look(0f, p - _game.player.Pitch);
                _game.player.FaceYaw(y);
                if (Mathf.Abs(Mathf.DeltaAngle(y, yaw)) < 1f && Mathf.Abs(p - pitch) < 1f) break;
                yield return null;
            }
        }

        /// <summary>The way from one point of the floor to another: out of the room she's in through its door, along the
        /// corridor, in through the next one's.</summary>
        static List<Vector3> Route(Vector3 from, Vector3 to)
        {
            var path = new List<Vector3>();
            int a = Zone(from), b = Zone(to);
            if (a != b)
            {
                var outOf = Doors(a);
                for (int i = outOf.Length - 1; i >= 0; i--) path.Add(outOf[i]);
                foreach (var p in Doors(b)) path.Add(p);
            }
            path.Add(to);
            return path;
        }

        /// <summary>0 the corridor; 11-16 the rooms on the north side; 21 quiet room, 22 staff room, 23 station, 24 lobby, 25 family room.</summary>
        static int Zone(Vector3 p)
        {
            if (p.z >= 11f && p.z <= 14f) return 0;
            if (p.z > 14f)
                return p.x < 6.5f ? 11 : p.x < 13f ? 12 : p.x < 19f ? 13 : p.x < 27f ? 14 : p.x < 33f ? 15 : 16;
            return p.x < 6f ? 21 : p.x < 15f ? 22 : p.x < 27f ? 23 : p.x < 37f ? 24 : 25;
        }

        /// <summary>From the corridor in to a zone (the corridor's own point first).</summary>
        static Vector3[] Doors(int zone)
        {
            switch (zone)
            {
                case 12: return new[] { new Vector3(7.7f, 0f, 12.5f), new Vector3(7.7f, 0f, 15.3f) };
                case 14: return new[] { new Vector3(20.4f, 0f, 12.5f), new Vector3(20.4f, 0f, 15.4f) };
                case 16: return new[] { new Vector3(34.3f, 0f, 12.5f), new Vector3(34.3f, 0f, 15.3f) };
                case 21: return new[] { new Vector3(4.1f, 0f, 12.5f), new Vector3(4.1f, 0f, 9.8f) };
                case 22: return new[] { new Vector3(13.7f, 0f, 12.5f), new Vector3(13.7f, 0f, 9.8f) };
                case 23: return new[] { new Vector3(25.9f, 0f, 12.5f), new Vector3(25.9f, 0f, 9.4f) };
                case 24: return new[] { new Vector3(32f, 0f, 12.5f), new Vector3(32f, 0f, 9.5f) };
                case 25: return new[] { new Vector3(32f, 0f, 12.5f), new Vector3(32f, 0f, 9.5f), new Vector3(35.8f, 0f, 5.4f), new Vector3(38.2f, 0f, 5.4f) };
                default: return new Vector3[0];
            }
        }

        // ---------- Reading and choosing ----------

        /// <summary>Reads the dialogue running now to its end (and anything it starts right after).</summary>
        IEnumerator Read(float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            yield return Real(0.2f);
            float idle = 0f;
            while (Time.realtimeSinceStartup < end)
            {
                if (GameRoot.Dialogue.IsActive) idle = 0f;
                else
                {
                    idle += Time.unscaledDeltaTime;
                    if (idle > 0.8f) yield break;
                }
                yield return ReadStep();
            }
        }

        float _lineShown = -1f;

        /// <summary>One step of reading: a typed line gets a reader's pause, then the next; a choice gets a moment, then one.</summary>
        IEnumerator ReadStep()
        {
            var d = GameRoot.Dialogue;
            if (d.IsActive)
            {
                if (d.ChoiceCount > 0)
                {
                    yield return Real(1.4f);
                    if (d.ChoiceCount > 0)
                    {
                        int pick = PickChoice();
                        Say("chose \"" + d.Choices[pick] + "\"");
                        d.Choose(pick);
                    }
                    _lineShown = -1f;
                }
                else if (!d.IsTyping)
                {
                    if (_lineShown < 0f)
                    {
                        _lineShown = Time.realtimeSinceStartup;
                        Say((string.IsNullOrEmpty(d.Speaker) ? "" : d.Speaker + ": ") + (d.CurrentLine ?? ""));
                    }
                    float pause = 1.1f + Mathf.Min(2.2f, (d.CurrentLine ?? "").Length * 0.018f);
                    if (Time.realtimeSinceStartup - _lineShown > pause)
                    {
                        d.Advance();
                        _lineShown = -1f;
                    }
                }
            }
            yield return null;
        }

        /// <summary>The run's choices: the round's right answers (Day 2's band check slips, to show the catch); the stairs down;
        /// at the bedside, the first topic each time.</summary>
        int PickChoice()
        {
            var d = GameRoot.Dialogue;
            for (int i = 0; i < d.ChoiceCount; i++)
            {
                string c = d.Choices[i];
                if (c.StartsWith("Go down")) return i;
                if (_day == 2 && c.StartsWith("Close enough")) return i;
                if (c.StartsWith("They don't match") && _day != 2) return i;
                if (c.StartsWith("Call the doctor") || c.StartsWith("Hold it") || c.StartsWith("Sodium chloride") || c.StartsWith("Chart them") || c.StartsWith("They match") || c.StartsWith("Two 500 mg")) return i;
            }
            return 0;
        }

        IEnumerator Bedside()
        {
            Say("the bedside: she sits");
            VisitingHoursGame.AutoRise = false;
            float end = Time.realtimeSinceStartup + 120f;
            while (_game.Bedside != null && Time.realtimeSinceStartup < end)
            {
                if (_game.Bedside.Current == VhBedside.Phase.Ready && !GameRoot.Dialogue.IsActive)
                {
                    Say("\"E  Stand up\"");
                    yield return Real(1.6f);
                    _pad.Tap(GameRoot.Input.Interact);
                    yield return Real(0.2f);
                    _game.RequestRise();
                    Say("she stands");
                    yield return Until(() => _game.Bedside == null, 6f);
                    break;
                }
                yield return ReadStep();
            }
            Say("told him: " + string.Join(", ", _game.ToldToday) + "; the dream will change: " + string.Join(", ", Chain.DreamChanges(GameRoot.Save.Data)));
        }

        // ---------- Frames ----------

        void LateUpdate()
        {
            if (!_recording) return;
            if (Time.realtimeSinceStartup < _nextFrame) return;
            _nextFrame = Time.realtimeSinceStartup + FrameEvery;
            StartCoroutine(Frame());
        }

        IEnumerator Frame()
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null) yield break;
            _frames++;
            File.WriteAllBytes(Path.Combine(_folder, "frame-" + _frames.ToString("0000") + ".jpg"), tex.EncodeToJPG(85));
            Destroy(tex);
        }

        void Finish(string problem)
        {
            _recording = false;
            if (GameRoot.Exists && GameRoot.Input.Synthetic == _pad) GameRoot.Input.Synthetic = null;
            if (problem != null) Say("FAIL " + problem);
            Say((problem == null ? "done: " : "stopped: ") + _frames + " frames");
            File.WriteAllText(Path.Combine(_folder, "run.txt"), _log.ToString());
            Debug.Log((problem == null ? "JAMKIT_VH_DAY_RUN_OK " : "JAMKIT_VH_DAY_RUN_FAIL ") + _frames + " frames in " + _folder);
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
