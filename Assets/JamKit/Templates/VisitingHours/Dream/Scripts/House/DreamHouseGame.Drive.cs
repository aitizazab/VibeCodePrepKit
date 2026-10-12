using System;
using System.Collections;
using System.IO;
using System.Text;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 2 driving itself: the presenter's chapters and the debug skip, the autopilot (the hide-and-seek walked by
    /// <see cref="HouseWalker"/>: to each memory in turn, breaking away when Theo sees her; the escape run by the course
    /// autopilot), attract mode, the smoke run, and the recorded runs for the judge (<c>-houseRecord &lt;folder&gt;</c>: a
    /// frame every 0.5 s of game time through the whole night, with a log of what happened).
    /// </summary>
    public sealed partial class DreamHouseGame
    {
        // =====================================================================================================
        // Chapters and the skip
        // =====================================================================================================

        /// <summary>Presenter chapter: the street, hide-and-seek (just begun, from the table), the argument, the house tipping
        /// (the escape from its start), the last hall (the umbrella and the front door).</summary>
        public void JumpToChapter(string chapter)
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            switch (chapter)
            {
                case DreamHouse.ChapterArrival:
                    NewVisit();
                    break;
                case DreamHouse.ChapterHideAndSeek:
                    _story.Restart();
                    ResetHouse();
                    ApplyBedside();
                    _story.SitDown();
                    _round.Collect(DreamHouseStory.MemPlaceCard, out _);
                    { var v = ViewOf(DreamHouseStory.MemPlaceCard); if (v != null) v.SetFound(true); }
                    Teleport(ToWorld(_h.nadiaChair) + houseRoot.TransformDirection(new Vector3(0.1f, 0f, -1.0f)), WorldYaw(180f), Vector3.zero);
                    BeginHideAndSeek();
                    break;
                case DreamHouse.ChapterArgument:
                    _story.JumpTo(HousePhase.TheoAsks);
                    ResetHouse();
                    AnswerTheo(TheoAnswer.Honest);
                    break;
                case DreamHouse.ChapterTips:
                    JumpToEscape(_e.startCheckpoint);
                    break;
                case DreamHouse.ChapterFrontDoor:
                    JumpToEscape(_e.frontDoorCheckpoint);
                    break;
            }
        }

        /// <summary>Straight into the escape at one of its checkpoints, running (chapters, the smoke run, tests).</summary>
        public void JumpToEscape(int checkpoint, ArgumentOutcome outcome = ArgumentOutcome.Unsure)
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            _story.JumpTo(HousePhase.Escape, outcome);
            ResetHouse();
            BeginEscape(checkpoint < 0 ? _e.startCheckpoint : checkpoint);
        }

        bool SkipAhead()
        {
            switch (_story.Phase)
            {
                case HousePhase.WayIn:
                case HousePhase.Pancakes: JumpToChapter(DreamHouse.ChapterHideAndSeek); break;
                case HousePhase.HideAndSeek:
                case HousePhase.TheoAsks: JumpToChapter(DreamHouse.ChapterArgument); break;
                case HousePhase.Argument: Choose(ArgumentOutcome.Unsure); break;
                default: JumpToChapter(DreamHouse.ChapterFrontDoor); break;
            }
            return true;
        }

        // =====================================================================================================
        // The autopilot
        // =====================================================================================================

        string _pilotFor, _pilotSkip;
        float _flee, _pilotThere, _pilotSkipUntil;

        /// <summary>The house plays itself (attract, the smoke run, the recorder, tests): the hide-and-seek walked, the escape run.</summary>
        public void SetAutopilot(bool on)
        {
            if (!on) { player.Autopilot = null; _walker.Stop(); return; }
            if (_story.Phase == HousePhase.Escape) { _escapePilot.Reset(_s - 1f); player.Autopilot = _escapePilot.Next; }
            else player.Autopilot = HousePilot;
        }

        MoveInput HousePilot(MoveSim sim)
        {
            float dt = Time.deltaTime;
            if (_story.Phase == HousePhase.Escape) { player.Autopilot = _escapePilot.Next; return _escapePilot.Next(sim); }
            if (_busy || _speaking && _story.Phase != HousePhase.HideAndSeek) return default;
            var p = Feet;
            switch (_story.Phase)
            {
                case HousePhase.WayIn:
                    if (_pilotFor != "chair" || _walker.Arrived)
                    {
                        _pilotFor = "chair";
                        _walker.sprint = false;
                        _walker.Go(p, _h.nadiaChair + new NVec3(0f, 0f, -1.1f));
                    }
                    return _walker.Next(sim, dt);
                case HousePhase.HideAndSeek:
                    _walker.sprint = true;
                    // Seen: break away to somewhere far from him for a few seconds.
                    if (_theo.Visibility > 0.05f && _flee <= 0f && !_round.AllFound && !_theo.Counting)
                    {
                        _flee = 3.5f;
                        int best = -1;
                        float bd = -1f;
                        for (int i = 0; i < _h.nodes.Count; i++)
                        {
                            var n = _h.nodes[i].p;
                            if (Mathf.Abs(n.Y - p.Y) > 0.3f || NVec3.Distance(n, p) > 12f) continue;
                            float d = NVec3.Distance(n, _theo.Position);
                            if (d > bd) { bd = d; best = i; }
                        }
                        if (best >= 0) { _walker.Go(p, _h.nodes[best].p); _pilotFor = "flee"; }
                    }
                    if (_flee > 0f) { _flee -= dt; return _walker.Next(sim, dt); }
                    if (_round.AllFound) return default;   // she waits for him
                    // There and still not taken after 3 s (or blocked): leave that one for 30 s and try another.
                    if (_pilotFor != null && _pilotFor != "flee" && _pilotFor != "chair" && (_walker.Arrived || _walker.Stuck))
                    {
                        _pilotThere += dt;
                        if (_pilotThere > 3f || _walker.Stuck) { _pilotSkip = _pilotFor; _pilotSkipUntil = Time.time + 30f; _pilotThere = 0f; _pilotFor = null; }
                        else if (_walker.Arrived) return default;
                    }
                    else _pilotThere = 0f;
                    if (_pilotFor == null || _pilotFor == "flee" || _pilotFor == "chair" || _round.IsFound(_pilotFor) || _walker.Stuck || _walker.Arrived)
                    {
                        _pilotFor = null;
                        float best = float.MaxValue;
                        foreach (var m in _h.memories)
                        {
                            if (_round.IsFound(m.id)) continue;
                            if (m.id == _pilotSkip && Time.time < _pilotSkipUntil) continue;
                            if (!_theoRoomOpen && _round.RoomOf(m.id) == "theo") continue;
                            var at = _round.StandAtOf(m.id);
                            float d = NVec3.Distance(p, at) + Mathf.Abs(p.Y - at.Y) * 6f;
                            if (d < best) { best = d; _pilotFor = m.id; }
                        }
                        if (_pilotFor != null && !_walker.Go(p, _round.StandAtOf(_pilotFor))) _pilotFor = null;
                    }
                    return _walker.Next(sim, dt);
                default:
                    return default;
            }
        }

        IEnumerator AttractDrive(string segment, float seconds, SyntheticInput input)
        {
            SetAutopilot(true);
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && GameRoot.Flow.InRun) yield return null;
            SetAutopilot(false);
            input.Clear();
        }

        /// <summary>The smoke run: the last hall, the umbrella, out of the door onto the hill and to the end of the rain, by
        /// the autopilot. It must get out (not thrown out); otherwise it logs an error, which fails the smoke run.</summary>
        IEnumerator SmokeDrive()
        {
            JumpToEscape(_e.frontDoorCheckpoint);
            SetAutopilot(true);
            float end = Time.realtimeSinceStartup + 14f;
            while (Time.realtimeSinceStartup < end && GameRoot.Flow.InRun && _story.Phase == HousePhase.Escape) yield return null;
            SetAutopilot(false);
            bool ok = _story.Phase == HousePhase.Out || _outside;
            if (!ok) Debug.LogError("JAMKIT_HOUSE_SMOKE_FAIL the last hall: " + _story.Phase);
            else Debug.Log("JAMKIT_HOUSE_SMOKE the last hall ok: out" + (umbrella != null && umbrella.HasUmbrella ? " under the umbrella" : ""));
        }

        // =====================================================================================================
        // The recorded runs (-houseRecord <folder> [-houseRecordRun all|hideandseek|escape])
        // =====================================================================================================

        public const string RecordFlag = "-houseRecord", RecordRunFlag = "-houseRecordRun";
        const float FrameEvery = 0.5f;

        string _recDir, _recRun;
        bool _recording;
        float _recNext;
        int _recFrames;
        readonly StringBuilder _recLog = new StringBuilder();
        RenderTexture _recRt;
        Texture2D _recTex;

        public static bool RecordArgs(string[] args, out string dir, out string run)
        {
            dir = null;
            run = "all";
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == RecordFlag && i + 1 < args.Length) dir = args[i + 1].Trim('"');
                if (args[i] == RecordRunFlag && i + 1 < args.Length) run = args[i + 1].Trim('"').ToLowerInvariant();
            }
            return !string.IsNullOrEmpty(dir);
        }

        void RecorderStart()
        {
            if (!RecordArgs(Environment.GetCommandLineArgs(), out _recDir, out _recRun)) return;
            Directory.CreateDirectory(_recDir);
            StartCoroutine(Record());
        }

        /// <summary>The built player's recorded run (<see cref="HouseRecordFlag"/>: <c>-house-record</c>): plays the night
        /// on the autopilot into <paramref name="dir"/> and quits.</summary>
        public void StartRecording(string dir, string run)
        {
            if (_recording || string.IsNullOrEmpty(dir)) return;
            _recDir = dir;
            _recRun = string.IsNullOrEmpty(run) ? "all" : run;
            Directory.CreateDirectory(_recDir);
            StartCoroutine(Record());
        }

        void Note(string what)
        {
            string line = $"{Time.time,7:0.0}s  {what}";
            _recLog.AppendLine(line);
            Debug.Log("JAMKIT_HOUSE_RUN " + line);
        }

        IEnumerator Record()
        {
            yield return null;
            Note("recording " + _recRun + " to " + _recDir);
            if (_recRun == "all" || _recRun == "hideandseek")
            {
                _recSegment = "hide-and-seek";
                JumpToChapter(DreamHouse.ChapterHideAndSeek);
                SetAutopilot(true);
                _recording = true;
                float end = Time.time + 420f;
                int caught = 0, found = _round.FoundCount;
                while (Time.time < end && _story.Phase <= HousePhase.TheoAsks)
                {
                    if (_round.Catches != caught) { caught = _round.Catches; Note("caught (" + caught + "): " + _round.Counter); }
                    if (_round.FoundCount != found) { found = _round.FoundCount; Note("memory: " + _round.Counter); }
                    yield return null;
                }
                Note("the round: " + _round.End + ", " + _round.Counter + ", caught " + _round.Catches + "; " + _story.Phase);
                // On through the argument (the autopilot: unsure) into the escape.
                float until = Time.time + 60f;
                while (Time.time < until && _story.Phase < HousePhase.Escape) { if (_story.Phase == HousePhase.Argument && !_speaking && !_battleUp) Choose(ArgumentOutcome.Unsure); yield return null; }
                SetAutopilot(false);
                _recording = false;
            }
            if (_recRun == "all" || _recRun == "escape")
            {
                _recSegment = "escape";
                JumpToEscape(_e.startCheckpoint);
                SetAutopilot(true);
                _recording = true;
                float end = Time.time + 240f;
                int stretches = 0;
                while (Time.time < end && _story.Phase == HousePhase.Escape)
                {
                    if (_e.corridor.Stretches != stretches) { stretches = _e.corridor.Stretches; Note("the corridor stretched (" + stretches + ")"); }
                    yield return null;
                }
                Note("the escape: " + _story.Phase + (_story.ThrownOut ? " (thrown out)" : "") + ", " + _e.corridor.Stretches + " stretches");
                float until = Time.time + 6f;
                while (Time.time < until) yield return null;
                SetAutopilot(false);
                _recording = false;
            }
            File.WriteAllText(Path.Combine(_recDir, "run.txt"), _recLog.ToString());
            Debug.Log("JAMKIT_HOUSE_RUN_OK " + _recFrames + " frames in " + _recDir);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        string _recSegment = "";

        void RecorderTick()
        {
            if (!_recording || Time.time < _recNext) return;
            _recNext = Time.time + FrameEvery;
            var cam = player != null ? player.cam : Camera.main;
            if (cam == null) return;
            if (_recRt == null) _recRt = new RenderTexture(new RenderTextureDescriptor(1920, 1080, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _recRt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { var prevT = cam.targetTexture; cam.targetTexture = _recRt; cam.Render(); cam.targetTexture = prevT; }
            var prev = RenderTexture.active;
            RenderTexture.active = _recRt;
            if (_recTex == null) _recTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var tex = _recTex;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            _recFrames++;
            string dir = Path.Combine(_recDir, _recSegment);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "frame-" + _recFrames.ToString("0000") + ".jpg"), tex.EncodeToJPG(88));
        }
    }
}
