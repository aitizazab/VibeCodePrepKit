using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Argument
{
    /// <summary>
    /// Captures of the real-time battle for review (docs/redesign/battle.md, "Captures"), from a build (like the ward's
    /// -ward-shot):
    /// <code>
    /// VisitingHours.exe -battle-shot "C:/…/battle-stills" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
    /// VisitingHours.exe -battle-record "C:/…/battle-run" [-battle-record-every 0.5] [-battle-record-battle walt_night1] -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
    /// </code>
    /// <b>-battle-shot</b> plays scripted battles at speed (the autopilot, with chosen mistakes) and takes a 1920 x 1080
    /// still at each key moment: the debate mid-flight, a wrong card and what it cost, "Because...", a break, a pressure
    /// phase, the three rings, a reply mid-line, the case file, the showdown and a cut, composure going and gone, every
    /// ending and its end card, and Walt's tutorial with Mags. It writes <c>shots.txt</c> (file, moment) and logs
    /// JAMKIT_BATTLE_SHOT_OK or _FAIL.
    /// <b>-battle-record</b> plays Lena's battle (or Walt's) start to end in real time with the human-paced autopilot and a
    /// first-time player's mistakes (<see cref="BattlePilot.FirstTime"/>), saving a frame every 0.5 s
    /// (frame_00001.png...), <c>timeline.txt</c> (each frame's time, state, heart, round, line) and <c>summary.txt</c>
    /// (the outcome, the total time and the share in play); it logs JAMKIT_BATTLE_RECORD_OK or _FAIL.
    /// Both start from the argument prototype's scene (in the Visiting Hours build) and quit when done.
    /// <b>-battle-presenter debate|sections</b> picks the presenter (default: the sections, round 2); the stills differ.
    /// </summary>
    public sealed class ArgumentBattleShot : MonoBehaviour
    {
        public const string ShotFlag = "-battle-shot", RecordFlag = "-battle-record", EveryFlag = "-battle-record-every", BattleFlag = "-battle-record-battle";

        public struct Request
        {
            public string folder;
            public bool record;
            public float every;
            public string battle;
            public ArgumentPresenter presenter;
        }

        Request _request;
        readonly List<string> _lines = new List<string>();
        int _taken, _failed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Parse(Environment.GetCommandLineArgs(), out var request)) return;
            var go = new GameObject("[Battle shot]");
            DontDestroyOnLoad(go);
            go.AddComponent<ArgumentBattleShot>()._request = request;
        }

        /// <summary>The flags (false without either).</summary>
        public static bool Parse(string[] args, out Request request)
        {
            request = new Request { every = 0.5f, battle = ArgumentBattles.Lena, presenter = ArgumentPresenter.Sections };
            if (args == null) return false;
            if (ArgumentHost.PresenterArg(args) == (int)ArgumentPresenter.Debate) request.presenter = ArgumentPresenter.Debate;
            bool found = false;
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1].Trim('"') : null;
                switch (args[i])
                {
                    case ShotFlag: found = true; request.folder = next ?? "battle-stills"; break;
                    case RecordFlag: found = true; request.record = true; request.folder = next ?? "battle-run"; break;
                    case EveryFlag: if (next != null && float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out float e)) request.every = Mathf.Clamp(e, 0.1f, 5f); break;
                    case BattleFlag: if (next != null) request.battle = next; break;
                }
            }
            return found;
        }

        IEnumerator Start()
        {
            if (!Path.IsPathRooted(_request.folder)) _request.folder = Path.Combine(Application.persistentDataPath, _request.folder);
            Directory.CreateDirectory(_request.folder);
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            Debug.Log("JAMKIT_BATTLE_SHOT " + (_request.record ? "record " : "stills ") + _request.folder);
            yield return Until(() => GameRoot.Exists && !GameRoot.IsLoadingScene && GameRoot.Flow.Current != GameState.Boot, 60f);
            yield return Real(1f);
            GameRoot.Save.Data.seenHowToPlay = true;
            // Into the prototype's scene (the shell, on black); its own battle is closed: these play their own.
            bool jumped = false;
            for (int i = 0; i < 20 && !jumped; i++)
            {
                var r = PresenterMode.Jump(ArgumentPrototype.Scene, ArgumentPrototype.Story);
                jumped = r == JumpResult.Pending || r == JumpResult.Jumped;
                if (!jumped) yield return Real(1f);
            }
            if (!jumped) { Finish("couldn't open " + ArgumentPrototype.Scene); yield break; }
            yield return Until(() => !GameRoot.IsLoadingScene && ArgumentPrototype.Current != null && ArgumentHost.Running, 30f);
            ArgumentHost.Current?.Close(false);
            yield return Real(1.5f);
            if (_request.record) yield return Record();
            else yield return Stills();
        }

        // ---------- The recorded run ----------

        IEnumerator Record()
        {
            var def = ArgumentBattles.Build(_request.battle) ?? ArgumentBattles.Build(ArgumentBattles.Lena);
            var setup = new ArgumentSetup { seed = 21 };
            if (def.id == WaltNight1.Id) setup.evidence.AddRange(new[] { WaltNight1.WaltDead, WaltNight1.SameSaturday, WaltNight1.WaltAlive });
            else setup.allEvidence = true;
            var host = ArgumentHost.Begin(def, setup, null, _request.presenter);
            if (host == null) { Finish("no battle"); yield break; }
            host.SetAutopilot(true, true, _request.presenter == ArgumentPresenter.Sections ? SectionsFirstTime : BattlePilot.FirstTime);
            var timeline = new StringBuilder();
            timeline.Append("frame\tseconds\tstate\tbpm\tround\theat\tanger\tsky\tline\n");
            float start = Time.realtimeSinceStartup, next = 0f, endShown = -1f;
            int frame = 0;
            while (true)
            {
                float t = Time.realtimeSinceStartup - start;
                if (t >= next)
                {
                    next += _request.every;
                    frame++;
                    string name = "frame_" + frame.ToString("00000", CultureInfo.InvariantCulture) + ".png";
                    ScreenCapture.CaptureScreenshot(Path.Combine(_request.folder, name));
                    var f = host.Flow;
                    string line = f.Delivery.HasLine ? Typewriter.StripTags(f.Delivery.Current.who + ": " + f.Delivery.Typewriter.Plain) : "";
                    timeline.Append(frame).Append('\t').Append(t.ToString("0.0", CultureInfo.InvariantCulture)).Append('\t').Append(f.State).Append('\t')
                        .Append(f.Engine.HeartRate).Append('\t').Append(f.Round).Append('\t').Append(f.Engine.Heat).Append('\t').Append(f.Engine.Anger).Append('\t')
                        .Append(f.Sky ? "sky" : "-").Append('\t').Append(line.Replace('\t', ' ').Replace('\n', ' ')).Append('\n');
                }
                if (host.EndCardShown && endShown < 0f) endShown = t;
                if (endShown >= 0f && t - endShown > 3f) break;
                if (t > 1500f) { Finish("the run went on past 25 minutes"); yield break; }
                yield return null;
            }
            var flow = host.Flow;
            float active = flow.ActiveSeconds, talk = flow.TalkSeconds;
            string summary = "battle\t" + def.id + "\npresenter\t" + flow.Presenter + "\noutcome\t" + flow.Engine.Outcome + "\nstance\t" + flow.Engine.Stance + "\nseconds\t" + (active + talk).ToString("0", CultureInfo.InvariantCulture)
                + "\nin play\t" + active.ToString("0", CultureInfo.InvariantCulture) + "\ntalk\t" + talk.ToString("0", CultureInfo.InvariantCulture)
                + (flow.Sections ? "\narguing\t" + flow.ArgueSeconds.ToString("0", CultureInfo.InvariantCulture) + "\ndodging\t" + flow.DodgeSeconds.ToString("0", CultureInfo.InvariantCulture) + "\nsky\t" + flow.Sky : "")
                + "\nshare in play\t" + (100f * active / Mathf.Max(1f, active + talk)).ToString("0", CultureInfo.InvariantCulture) + "%\nframes\t" + frame + "\nevery\t" + _request.every.ToString("0.0#", CultureInfo.InvariantCulture) + " s\n";
            File.WriteAllText(Path.Combine(_request.folder, "timeline.txt"), timeline.ToString());
            File.WriteAllText(Path.Combine(_request.folder, "summary.txt"), summary);
            host.Continue();
            yield return Real(2f);
            Debug.Log("JAMKIT_BATTLE_RECORD_OK " + frame + " frames, " + flow.Engine.Outcome + " in " + (active + talk).ToString("0", CultureInfo.InvariantCulture) + " s (" + active.ToString("0", CultureInfo.InvariantCulture) + " s in play) in " + _request.folder);
            Application.Quit();
        }

        // ---------- The stills ----------

        /// <summary>A moment to catch: when it comes (in any order), the battle drops to real speed for
        /// <see cref="after"/> seconds and the still is taken.</summary>
        sealed class Moment
        {
            public string name, what;
            public Func<BattleFlow, bool> when;
            public float after;
            public Moment(string name, string what, Func<BattleFlow, bool> when, float after = 0f)
            {
                this.name = name;
                this.what = what;
                this.when = when;
                this.after = after;
            }
        }

        /// <summary>A first-time player's slips in the sections (as SectionsTests' timing run): three wrong cards, two wrong
        /// reasons, a hesitation, and one of her quotable lines let go by.</summary>
        static BattlePilot.Mistake[] SectionsFirstTime => new[]
        {
            BattlePilot.Mistake.WrongCard, BattlePilot.Mistake.WrongReason, BattlePilot.Mistake.MissQuote, BattlePilot.Mistake.WrongCard,
            BattlePilot.Mistake.Hesitate, BattlePilot.Mistake.WrongReason, BattlePilot.Mistake.WrongCard,
        };

        IEnumerator Stills()
        {
            if (_request.presenter == ArgumentPresenter.Sections) yield return SectionStills();
            else yield return DebateStills();
        }

        /// <summary>The sections presenter's stills (round 2): the argue section (her notebook page), the case file, a wrong
        /// card, Because, her words underlined and caught, a reply as a dodge with her bubble, hush, her pencil's notes, a
        /// break, the three rings, the ceiling lifting, a sky phase on her lines, her case as a constellation, her patience
        /// running out, the showdown, composure, every ending and its card (Heard out: the watch), Walt with Mags.</summary>
        IEnumerator SectionStills()
        {
            _presenter = ArgumentPresenter.Sections;
            // 1. Lena, honest, with a player's slips (human-paced at 3x).
            var host = Begin(DreamLenaDemo.Build(), All(ArgumentDifficulty.Normal, 21), true, BattlePilot.Mistake.WrongCard, BattlePilot.Mistake.WrongReason);
            yield return Collect(host, 3, true,
                new Moment("01-argue", "the argue section: her case on the table as a page of her notebook (each statement where she put it, pencil lines up to what it holds), one picked (its words in full above, the words that matter in gold), the card in hand, the keys, her patience drawing itself along the near edge",
                    f => f.State == BattleState.Argue && f.Turn >= 3 && f.StateTime > 1.5f && f.ArgueTime > 1f, 0.3f),
                new Moment("03-wrong-card", "a wrong card: her answer, a heartbeat, the card out of hand (the caption), the hand", f => f.Count(CueKind.WrongCard) > 0, 0.5f),
                new Moment("04-because", "Because...: the reason cards, her patience running down", f => f.State == BattleState.Because, 0.9f),
                new Moment("05-quote-open", "her own words, underlined as she says them, and the prompt to hold her to them",
                    f => f.QuoteOpen && f.Delivery.Typewriter.Shown >= f.QuoteEnd - 2),
                new Moment("06-quote-caught", "caught: her words fly to Nadia's hand as a torn page", f => f.Count(CueKind.QuoteCaught) > 0 && f.QuoteCaughtAge > 0.45f),
                new Moment("07-reply-dodge", "her reply as a dodge: plates and flour inked after the pencil, her line in a speech bubble over her",
                    f => f.State == BattleState.Pressure && f.Engine.Pressure != null && f.Engine.Pressure.Spec.calls == 0 && f.Engine.Pressure.Spec.hush == 0 && f.Engine.Pressure.Time > 1.2f
                        && f.Delivery.HasLine && f.Delivery.Current.speaker == Speaker.Opponent),
                new Moment("08-hush", "hush: her care closing in, soft and dashed; the light holding still (the whole ring round it)",
                    f => f.State == BattleState.Pressure && f.Engine.Pressure != null && f.Engine.Pressure.HushOn && HushRadius(f.Engine.Pressure) < 0.9f),
                new Moment("09-notes", "her notes: her pencil following the light, pencilling where it's been and inking it a moment later",
                    f => f.State == BattleState.Pressure && f.Engine.Pressure != null && f.Engine.Pressure.TrailOn && f.Engine.Pressure.Time > 3.5f),
                new Moment("10-break", "a premise broken (the flash; her note on the page struck through)", f => f.Count(CueKind.Break) > 0, 0.4f),
                new Moment("11-three-rings", "the three rings: \"He called you first\"; standing still hurts",
                    f => f.State == BattleState.Pressure && f.Engine.Pressure != null && f.Engine.Pressure.Spec.calls > 0 && f.Engine.Pressure.CallsRung >= 2, 0.7f),
                new Moment("12-sky-opening", "the ceiling lifts: her night comes down over the table (\"Someone just looked up and drew the lines.\")",
                    f => f.Sky && f.SkyProgress > 0.45f && f.SkyProgress < 0.8f),
                new Moment("13-sky-phase", "under her sky: the light on her lines, a shower of streaks at one angle along the lines still lit; a conceded star is cover",
                    f => f.State == BattleState.Pressure && f.Engine.Pressure != null && f.Engine.Pressure.Spec.sky && f.Engine.Pressure.Time > 2.2f),
                new Moment("14-sky-argue", "her case as a constellation: stars where her statements are, lit lines, rubbed-out traces, the one picked",
                    f => f.State == BattleState.Argue && f.Sky && f.SkyProgress >= 1f && f.StateTime > 1.5f),
                new Moment("15-ending-convinced", "the end beat: convinced (she strikes her own claim out)", f => f.State == BattleState.Ending && f.EndingProgress > 0.35f));
            yield return EndCard(host, "16-endcard-convinced", "the end card: convinced (Lena unsure)");

            // 2. A press: the showdown and a cut, then the ending it leads to.
            host = Begin(DreamLenaDemo.Build(), All(ArgumentDifficulty.Normal, 33), false, BattlePilot.Mistake.Press, BattlePilot.Mistake.MissCut);
            yield return Collect(host, 3, false,
                new Moment("17-showdown", "pressing: the showdown, her words coming at Nadia's mark", f => f.State == BattleState.Showdown && f.Showdown != null && f.Showdown.Time > 1.6f),
                new Moment("18-showdown-cut", "a cut", f => f.Count(CueKind.Cut) > 0, 0.08f));
            yield return EndCard(host, "19-endcard-pressed", "the end card: pressed (Lena condemns, Despair +1, Force +1)");

            // 3. Her patience, then composure: a player who neither moves nor answers.
            host = Begin(DreamLenaDemo.Build(), All(ArgumentDifficulty.Normal, 45), null);
            yield return Collect(host, 1, false,
                new Moment("20-argue-sketch", "her patience running out: the pencil line nearly across, the attack she's sketching at the table's edges (the overlap into the dodge)",
                    f => f.State == BattleState.Argue && f.ArgueSketch > 0.6f));
            if (host != null) host.Engine.Hurt(3);
            yield return Collect(host, 1, false,
                new Moment("21-panic", "composure going: 160 bpm", f => f.State == BattleState.Panic && f.PanicProgress > 0.5f),
                new Moment("22-composure-lost", "\"I can't... I can't breathe- I need to get out-\", and Lena's answer",
                    f => f.State == BattleState.Talk && f.Delivery.HasLine && f.Delivery.Current.speaker == Speaker.Opponent && f.Delivery.LineDone, 0.2f));
            yield return EndCard(host, "23-endcard-composure", "the end card: composure lost (Nadia left the table; Lena condemns; Despair +1)");

            // 4. Heard out: without the same Saturday or Walt's truth, "his wish" can't break. She gives Nadia her watch.
            var heard = new ArgumentSetup { seed = 52 };
            heard.evidence.AddRange(DreamLenaDemo.EvidenceWithoutWalt);
            host = Begin(DreamLenaDemo.Build(), heard, false);
            yield return Collect(host, 4, false,
                new Moment("24-watch", "Heard out: she gives Nadia her watch (\"Give me your wrist. The right one.\")",
                    f => f.Delivery.HasLine && f.Delivery.Current.speaker == Speaker.Opponent && f.Delivery.Typewriter.Plain.Contains("wrist") && f.Delivery.Typewriter.Progress > 0.6f),
                new Moment("25-ending-heardout", "the end beat: heard out", f => f.State == BattleState.Ending && f.EndingProgress > 0.35f));
            yield return EndCard(host, "26-endcard-heardout", "the end card: heard out (Lena's watch)");

            // 5. Giving in: no cards at all.
            host = Begin(DreamLenaDemo.Build(), new ArgumentSetup { seed = 61 }, false);
            yield return Collect(host, 4, false, new Moment("27-giving-in", "holding \"That's true\" on her claim: giving in", f => f.HoldingYield && f.HoldProgress > 0.5f));
            yield return EndCard(host, "28-endcard-yielded", "the end card: yielded");

            // 6. Walt's tutorial, Mags on the earpiece.
            var walt = new ArgumentSetup { seed = 4 };
            walt.evidence.AddRange(new[] { WaltNight1.WaltDead, WaltNight1.SameSaturday, WaltNight1.WaltAlive });
            host = Begin(WaltNight1.Build(), walt, true, BattlePilot.Mistake.WrongCard);
            yield return Collect(host, 3, false,
                new Moment("29-walt-mags", "Walt's step: Mags teaching on the earpiece",
                    f => f.Delivery.HasLine && f.Delivery.Current.speaker == Speaker.Supporter && f.Delivery.Typewriter.Progress > 0.7f),
                new Moment("30-walt-argue", "Walt's case on his step (no clock: the tutorial)", f => f.State == BattleState.Argue && f.Turn >= 2 && f.StateTime > 1.2f, 0.3f));
            yield return EndCard(host, "31-endcard-walt", "the end card: Walt");

            File.WriteAllLines(Path.Combine(_request.folder, "shots.txt"), _lines);
            Finish(null);
        }

        static float HushRadius(PressurePhase p)
        {
            foreach (var r in p.Rings) if (r.active && r.hush) return r.radius;
            return 99f;
        }

        ArgumentPresenter _presenter = ArgumentPresenter.Sections;

        /// <summary>Milestone 1's stills: the debate presenter (-battle-presenter debate).</summary>
        IEnumerator DebateStills()
        {
            _presenter = ArgumentPresenter.Debate;
            // 1. Lena, honest, with a player's slips: the debate, the case file, a wrong card, Because, a reply mid-line,
            // a break, phases, the three rings, the ending and its card.
            var host = Begin(DreamLenaDemo.Build(), All(ArgumentDifficulty.Normal, 21), false, BattlePilot.Mistake.WrongCard, BattlePilot.Mistake.WrongReason);
            yield return Collect(host, 3, true,
                new Moment("01-debate-mid-flight", "the debate: statements flying as she says them, weak words glowing, her pressure sketched then inked, Nadia's light",
                    f => f.State == BattleState.Debate && f.Debate.Round >= 2 && Flying(f) >= 1 && f.Debate.Hazards != null && f.Debate.Hazards.Spawned >= 2, 1.1f),
                new Moment("03-wrong-card", "a wrong card: her answer, a heartbeat, the card out of hand (the caption), the hand", f => f.Count(CueKind.WrongCard) > 0, 0.5f),
                new Moment("04-because", "Because...: the reason cards, her patience running down", f => f.State == BattleState.Because, 0.9f),
                new Moment("05-reply-mid-line", "a reply typed one line at a time, her face reacting",
                    f => f.State == BattleState.Talk && f.Delivery.HasLine && f.Delivery.Current.speaker == Speaker.Opponent && f.Delivery.Typewriter.Progress > 0.45f && f.Delivery.Typewriter.Progress < 0.8f),
                new Moment("06-break", "a premise broken (the statement shatters, the flash)", f => f.Count(CueKind.Break) > 0, 0.12f),
                new Moment("07-pressure", "a pressure phase (a wrong reason's, or a key premise she holds on to)",
                    f => f.State == BattleState.Pressure && f.Engine.Pressure != null && f.Engine.Pressure.Spec.calls == 0 && f.Engine.Pressure.Time > 2.5f),
                new Moment("08-three-rings", "the three rings: \"He called you first\"; standing still hurts",
                    f => f.State == BattleState.Pressure && f.Engine.Pressure != null && f.Engine.Pressure.Spec.calls > 0 && f.Engine.Pressure.CallsRung >= 2, 0.7f),
                new Moment("09-ending-convinced", "the end beat: convinced", f => f.State == BattleState.Ending && f.EndingProgress > 0.35f));
            yield return EndCard(host, "10-endcard-convinced", "the end card: convinced (Lena unsure)");

            // 2. A press: the showdown and a cut, then the ending it leads to.
            host = Begin(DreamLenaDemo.Build(), All(ArgumentDifficulty.Normal, 33), false, BattlePilot.Mistake.Press, BattlePilot.Mistake.MissCut);
            yield return Collect(host, 3, false,
                new Moment("11-showdown", "pressing: the showdown, her words coming at Nadia's mark", f => f.State == BattleState.Showdown && f.Showdown != null && f.Showdown.Time > 1.6f),
                new Moment("12-showdown-cut", "a cut", f => f.Count(CueKind.Cut) > 0, 0.08f));
            yield return EndCard(host, "13-endcard-pressed", "the end card: pressed (Lena condemns, Despair +1, Force +1)");

            // 3. Composure: a player who neither moves nor answers (started at 124 bpm to get there sooner).
            host = Begin(DreamLenaDemo.Build(), All(ArgumentDifficulty.Normal, 45), null);
            yield return Until(() => host == null || host.Flow.State == BattleState.Debate || host.Flow.State == BattleState.Argue || host.Flow.State == BattleState.Pressure, 40f);
            if (host != null) host.Engine.Hurt(3);
            yield return Collect(host, 1, false,
                new Moment("14-heart-racing", "her heart at 142: the room closing in", f => f.Engine.HeartStep >= 4 && f.State == BattleState.Debate, 0.6f),
                new Moment("15-panic", "composure going: 160 bpm", f => f.State == BattleState.Panic && f.PanicProgress > 0.5f),
                new Moment("16-composure-lost", "\"I can't... I can't breathe- I need to get out-\", and Lena's answer",
                    f => f.State == BattleState.Talk && f.Delivery.HasLine && f.Delivery.Current.speaker == Speaker.Opponent && f.Delivery.LineDone, 0.2f));
            yield return EndCard(host, "17-endcard-composure", "the end card: composure lost (Nadia left the table; Lena condemns; Despair +1)");

            // 4. Heard out: without the same Saturday or Walt's truth, "his wish" can't break.
            var heard = new ArgumentSetup { seed = 52 };
            heard.evidence.AddRange(DreamLenaDemo.EvidenceWithoutWalt);
            host = Begin(DreamLenaDemo.Build(), heard, false);
            yield return Collect(host, 4, false, new Moment("18-ending-heardout", "the end beat: heard out", f => f.State == BattleState.Ending && f.EndingProgress > 0.35f));
            yield return EndCard(host, "19-endcard-heardout", "the end card: heard out (her watch)");

            // 5. Giving in: no cards at all.
            host = Begin(DreamLenaDemo.Build(), new ArgumentSetup { seed = 61 }, false);
            yield return Collect(host, 4, false, new Moment("20-giving-in", "holding \"That's true\" on her claim: giving in", f => f.HoldingYield && f.HoldProgress > 0.5f));
            yield return EndCard(host, "21-endcard-yielded", "the end card: yielded");

            // 6. Walt's tutorial, Mags on the earpiece.
            var walt = new ArgumentSetup { seed = 4 };
            walt.evidence.AddRange(new[] { WaltNight1.WaltDead, WaltNight1.SameSaturday, WaltNight1.WaltAlive });
            host = Begin(WaltNight1.Build(), walt, true, BattlePilot.Mistake.WrongCard);
            yield return Collect(host, 3, false,
                new Moment("22-walt-mags", "Walt's step: Mags teaching on the earpiece",
                    f => f.Delivery.HasLine && f.Delivery.Current.speaker == Speaker.Supporter && f.Delivery.Typewriter.Progress > 0.7f),
                new Moment("23-walt-debate", "Walt's debate (gentler: sparser, slower)", f => f.State == BattleState.Debate && f.Debate.Round >= 2 && Flying(f) >= 1, 0.8f));
            yield return EndCard(host, "24-endcard-walt", "the end card: Walt");

            File.WriteAllLines(Path.Combine(_request.folder, "shots.txt"), _lines);
            Finish(null);
        }

        /// <summary>Plays the battle at <paramref name="steps"/> times speed, catching each moment as it comes (in any
        /// order). <paramref name="caseFile"/>: also opens the case file once, after the first moment. Moments that never
        /// come before the end card are logged as missing.</summary>
        IEnumerator Collect(ArgumentHost host, int steps, bool caseFile, params Moment[] moments)
        {
            if (host == null) { foreach (var m in moments) Miss(m.name, "no battle"); yield break; }
            host.PilotSteps = steps;
            var pending = new List<Moment>(moments);
            float end = Time.realtimeSinceStartup + 900f;
            while (pending.Count > 0 && Time.realtimeSinceStartup < end && ArgumentHost.Current == host && !host.EndCardShown)
            {
                Moment hit = null;
                foreach (var m in pending) if (m.when(host.Flow)) { hit = m; break; }
                if (hit == null) { yield return null; continue; }
                pending.Remove(hit);
                host.PilotSteps = 1;
                if (hit.after > 0f) yield return Real(hit.after);
                yield return Shoot(hit.name, hit.what);
                if (caseFile)
                {
                    caseFile = false;
                    host.OpenCase(true);
                    yield return Real(0.6f);
                    yield return Shoot("02-case-file", "the case file (the battle waits): her premises, what she said, the cards in hand, the transcript");
                    host.OpenCase(false);
                }
                host.PilotSteps = steps;
            }
            foreach (var m in pending) Miss(m.name, "it never came before the end");
        }

        static ArgumentSetup All(ArgumentDifficulty d, ulong seed) => new ArgumentSetup { difficulty = d, allEvidence = true, seed = seed };

        /// <summary>Starts a battle; <paramref name="human"/> null: nobody plays it (it stands still), false: the fast
        /// autopilot, true: the human-paced one.</summary>
        ArgumentHost Begin(ArgumentDef def, ArgumentSetup setup, bool? human, params BattlePilot.Mistake[] mistakes)
        {
            ArgumentHost.Current?.Close(false);
            var host = ArgumentHost.Begin(def, setup, null, _presenter);
            if (host != null && human.HasValue) host.SetAutopilot(true, human.Value, mistakes);
            return host;
        }

        static int Flying(BattleFlow f)
        {
            int n = 0;
            foreach (var s in f.Debate.Statements) if (s.active && s.fate == Debate.Fate.Flying && s.Alpha > 0.9f) n++;
            return n;
        }

        static bool Cued(BattleFlow f, CueKind kind)
        {
            foreach (var c in f.Cues) if (c.kind == kind) return true;
            return false;
        }

        IEnumerator EndCard(ArgumentHost host, string name, string what)
        {
            if (host == null) { Miss(name, "no battle"); yield break; }
            float end = Time.realtimeSinceStartup + 900f;
            while (Time.realtimeSinceStartup < end && ArgumentHost.Current == host && !host.EndCardShown) yield return null;
            if (!host.EndCardShown) { Miss(name, "no end card"); yield break; }
            yield return Real(1f);
            yield return Shoot(name, what + " (" + host.Engine.Outcome + ", " + host.Engine.Stance + ", " + (host.Flow.ActiveSeconds + host.Flow.TalkSeconds).ToString("0", CultureInfo.InvariantCulture) + " s)");
            host.Continue();
            yield return Real(1.6f);
        }

        IEnumerator Shoot(string name, string what)
        {
            string path = Path.Combine(_request.folder, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float end = Time.realtimeSinceStartup + 6f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < end) yield return null;
            if (File.Exists(path)) { _taken++; _lines.Add(name + ".png\t" + what); Debug.Log("JAMKIT_BATTLE_SHOT " + name); }
            else Miss(name, "the file never appeared");
        }

        void Miss(string name, string why)
        {
            _failed++;
            _lines.Add(name + ".png\tMISSING: " + why);
            Debug.LogWarning("JAMKIT_BATTLE_SHOT missing " + name + ": " + why);
        }

        void Finish(string problem)
        {
            if (problem != null) Debug.Log("JAMKIT_BATTLE_SHOT_FAIL " + problem);
            else Debug.Log((_failed == 0 ? "JAMKIT_BATTLE_SHOT_OK " : "JAMKIT_BATTLE_SHOT_FAIL ") + _taken + " stills, " + _failed + " missing, in " + _request.folder);
            Application.Quit();
        }

        static IEnumerator Until(Func<bool> condition, float timeout)
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
