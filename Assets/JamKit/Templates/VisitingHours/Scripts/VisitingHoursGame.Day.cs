using System.Collections;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using JamKit.VisitingHours.Characters;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The waking days' redesign (docs/redesign/day.md): the duty round (the handover starts it; each task is a small
    /// decision with a right answer and a slip she catches; the round sheet on the HUD), the finds that open topics, and
    /// the bedside (she sits, takes his hand, tells him up to two things; his body answers; E stands her up). What she
    /// tells him is kept in the save (<see cref="Chain.Tell"/>) and changes the next night (<see cref="DreamChanges"/>).
    /// </summary>
    public sealed partial class VisitingHoursGame
    {
        DutyRound _round;
        bool _handover, _sat, _roundDoneQueued, _bedsideTalkStarted, _riseRequested;
        readonly List<string> _toldToday = new List<string>();
        readonly List<string> _roundLines = new List<string>();
        VhBedside _bedside;
        VhWakingStyle _bedsideStyle;
        float _risePromptShown = -1f;
        static bool _autoRise;

        /// <summary>The smoke run and the captures: she stands up by herself when she's done talking.</summary>
        public static bool AutoRise
        {
            get => _autoRise;
            set => _autoRise = value;
        }

        /// <summary>1 on Day 1, 2 on Day 2.</summary>
        public int DayNumber => _segment == ChainSegment.Day2 ? 2 : 1;
        public DutyRound Round => _round;
        public bool HandedOver => _handover;
        public bool SatWithHim => _sat;
        public VhBedside Bedside => _bedside;
        /// <summary>The topics she's told him today (topic ids, the "just sit" one as silence1 or silence2).</summary>
        public IReadOnlyList<string> ToldToday => _toldToday;

        bool IsWard => Area == SliceArea.Hospital || Area == SliceArea.HospitalDay2;

        static bool IsChair(string id) => id == "sit" || id == "sit1";

        static SaveData SaveData => GameRoot.Exists ? GameRoot.Save.Data : null;

        /// <summary>A day starts: a fresh round (Day 1's three tasks, Day 2's five), nothing said yet.</summary>
        void StartDayState()
        {
            _round = new DutyRound(DayNumber);
            _handover = false;
            _sat = false;
            _roundDoneQueued = false;
            _toldToday.Clear();
            RefreshRoundSheet();
        }

        /// <summary>E on a thing on the ward: the chair (the bedside), or a round task where it's due. False: not the day's.</summary>
        bool ExamineDay(string id)
        {
            if (!IsWard || _round == null) return false;
            if (IsChair(id))
            {
                TrySit();
                return true;
            }
            var task = DayContent.TaskAt(DayNumber, id, _round);
            if (task == null) return false;
            Play(_handover ? task.knot : "round_not_handed_over");
            return true;
        }

        void TrySit()
        {
            if (_sat || !_round.Complete)
            {
                Play("bedside_sit");
                return;
            }
            StartBedside();
        }

        /// <summary>The bedside starts (the chair's E, the smoke run, the captures).</summary>
        public void StartBedside()
        {
            if (_area == null || !_area.HasEliasHand || _bedside != null) return;
            _bedsideStyle = new VhWakingStyle(VhBoot.Config != null ? VhBoot.Config.wakingMaterial : null);
            _bedside = new VhBedside(player, _area, _bedsideStyle);
            _sat = true;
            _bedsideTalkStarted = false;
            _riseRequested = false;
            _mode = Mode.Bedside;
            _hud.SetPrompt("");
            _hud.SetExploring(false);
            _bedside.Begin();
            GameRoot.Audio.Play("vh_rustle", 0.5f);
        }

        void TickBedside(float dt)
        {
            if (_bedside == null)
            {
                _mode = Mode.Explore;
                return;
            }
            bool running = _bedside.Tick(dt);
            if (_bedside.Holding && !_bedsideTalkStarted)
            {
                _bedsideTalkStarted = true;
                Play("bedside_intro");
            }
            if (_bedside.Current == VhBedside.Phase.Ready && !GameRoot.Dialogue.IsActive)
            {
                // The prompt shows a moment before E counts (the E that closed her last line doesn't stand her up).
                if (_risePromptShown < 0f) _risePromptShown = 0f;
                _risePromptShown += dt;
                _hud.SetPrompt("E  Stand up");
                var input = GameRoot.Input;
                if (_autoRise || _riseRequested || (_risePromptShown > 0.4f && input.Pressed(input.Interact)))
                {
                    _riseRequested = false;
                    _risePromptShown = -1f;
                    _hud.SetPrompt("");
                    _bedside.Rise();
                    Play("bedside_rise");
                }
            }
            if (!running)
            {
                _bedside = null;
                _bedsideStyle?.Dispose();
                _bedsideStyle = null;
                _mode = Mode.Explore;
                _hud.SetPrompt("");
            }
        }

        /// <summary>Stands her up when she's ready (the captures; the same as pressing E).</summary>
        public void RequestRise() => _riseRequested = true;

        /// <summary>A topic's dialogue ended at the bedside: another (up to two), or enough for today.</summary>
        void AfterBedsideTalk()
        {
            if (_bedside == null || _bedside.Current != VhBedside.Phase.Talking) return;
            bool spokeSilence = _toldToday.Exists(t => t.StartsWith("silence"));
            int spoken = SpokenCount();
            bool more = !spokeSilence && spoken < DayContent.MaxTopicsPerDay && AnyTopicLeft();
            StartCoroutine(PlayWhenFree(more ? "bedside_more" : "bedside_enough"));
        }

        int SpokenCount()
        {
            int n = 0;
            foreach (var t in _toldToday) if (!t.StartsWith("silence")) n++;
            return n;
        }

        bool AnyTopicLeft()
        {
            foreach (var t in DayContent.AvailableTopics(DayNumber, _state.journal.Knows, _toldToday))
                if (!DayContent.IsSilence(t)) return true;
            return false;
        }

        /// <summary>vh_topic(id): the topic is one she can choose now (its find made, not told, time left).</summary>
        bool TopicOpen(string id)
        {
            if (_bedside == null || _state == null) return false;
            if (SpokenCount() >= DayContent.MaxTopicsPerDay) return false;
            foreach (var t in DayContent.AvailableTopics(DayNumber, _state.journal.Knows, _toldToday))
                if (t.id == id) return true;
            return false;
        }

        /// <summary>vh_state(name): what the ink asks about the day.</summary>
        bool DayState(string name)
        {
            switch (name)
            {
                case "handover": return _handover;
                case "round_done": return _round != null && _round.Complete;
                case "sat": return _sat;
                case "told_any": return SpokenCount() > 0;
                case "told_silence": return _toldToday.Exists(t => t.StartsWith("silence"));
                case "day2": return DayNumber == 2;
                case "slipped": return _round != null && _round.Slips > 0;
                case "forced_out": return DayNumber == 2 && Chain.WasForcedOut(SaveData, ChainSegment.Night1);
            }
            if (name != null && name.StartsWith("told:")) return Chain.HasTold(SaveData, name.Substring(5));
            return false;
        }

        /// <summary>The day's ink events. True when handled.</summary>
        bool DayEvent(string name)
        {
            if (name == "vh:handover")
            {
                _handover = true;
                RefreshRoundSheet();
                return true;
            }
            if (name == "vh:bedside:rise")
            {
                _bedside?.ReadyToRise();
                return true;
            }
            const string elias = "vh:elias:";
            if (name.StartsWith(elias))
            {
                _bedside?.Respond(name.Substring(elias.Length));
                return true;
            }
            const string told = "vh:told:";
            if (name.StartsWith(told))
            {
                string id = name.Substring(told.Length);
                if (id == "silence") id = "silence" + DayNumber;
                var topic = DayContent.FindTopic(id);
                if (topic != null && !_toldToday.Contains(id))
                {
                    _toldToday.Add(id);
                    Chain.Tell(SaveData, topic);
                    Record(DayContent.ToldEntry(id));
                }
                return true;
            }
            const string round = "vh:round:";
            if (name.StartsWith(round))
            {
                string rest = name.Substring(round.Length);
                int colon = rest.LastIndexOf(':');
                if (colon <= 0 || _round == null) return true;
                string task = rest.Substring(0, colon);
                bool slip = rest.Substring(colon + 1) == "slip";
                if (_round.Mark(task, slip))
                {
                    if (slip)
                    {
                        Chain.AddSlips(SaveData, 1);
                        GameRoot.Audio.Play("vh_wrong", 0.35f);
                    }
                    else GameRoot.Audio.Play("vh_journal", 0.5f);
                    RefreshRoundSheet();
                    if (_round.Complete) _roundDoneQueued = true;
                }
                return true;
            }
            return false;
        }

        /// <summary>When a dialogue ends: the round's last task says so; at the bedside, what comes next.</summary>
        void DayDialogueEnded(string knot)
        {
            CastDialogueEnded();
            if (_mode == Mode.Bedside)
            {
                if (knot == "bedside_intro" || knot == "bedside_more") AfterBedsideTalk();
                return;
            }
            if (_roundDoneQueued)
            {
                _roundDoneQueued = false;
                StartCoroutine(PlayWhenFree("round_done"));
            }
        }

        /// <summary>The round sheet on the HUD: the day's tasks, ticked as they're done (hidden before the handover and once
        /// the round's done and she's sat with him).</summary>
        void RefreshRoundSheet()
        {
            if (_hud == null) return;
            if (_round == null || !_handover || !IsWard)
            {
                _hud.SetChecklist(null, null);
                return;
            }
            _round.Lines(_roundLines);
            string title = _round.Complete ? "Round done  —  sit with him" : "The round, 6 PM";
            _hud.SetChecklist(title, _roundLines);
        }

        /// <summary>Ends the bedside at once (the debug skip, a jump elsewhere): her arm goes, the view stands.</summary>
        void StopBedside()
        {
            if (_bedside == null) return;
            _bedside.Dispose();
            _bedside = null;
            _bedsideStyle?.Dispose();
            _bedsideStyle = null;
            player.ResetView();
            if (_mode == Mode.Bedside) _mode = Mode.Explore;
        }

        // ---------- The 3D cast (docs/redesign/people.md): heads turn to her, mouths move when they speak ----------

        /// <summary>How near (metres) a person has to be to turn their head to her.</summary>
        const float LookRange = 5.5f;
        System.Action<DialogueLineInfo> _onCastLine;
        VhCharacter _talker;

        /// <summary>The ward's and the booth's people listen to the dialogue (Start calls this).</summary>
        void HookCast()
        {
            _onCastLine = OnCastLine;
            GameRoot.Dialogue.LineShown += _onCastLine;
        }

        void UnhookCast()
        {
            if (_onCastLine != null && GameRoot.Exists) GameRoot.Dialogue.LineShown -= _onCastLine;
            _onCastLine = null;
        }

        /// <summary>A place is up: nobody's talking yet.</summary>
        void CastEntered()
        {
            _talker = null;
            TickCast();
        }

        /// <summary>Heads turn to her when she's near (EliasBed sleeps).</summary>
        void TickCast()
        {
            if (_area == null || cam == null) return;
            var eye = cam.transform.position;
            for (int i = 0; i < _area.cast.Count; i++)
            {
                var c = _area.cast[i];
                if (c == null || c == _area.elias) continue;
                bool near = c == _talker || (c.transform.position - eye).sqrMagnitude < LookRange * LookRange;
                if (near && !c.IsLooking) c.LookAt(cam.transform);
                else if (!near && c.IsLooking) c.StopLooking();
            }
        }

        /// <summary>A line of dialogue: its speaker, if they're one of the 3D cast here, says it (mouth, a talking gesture,
        /// eyes on her); whoever spoke before goes quiet.</summary>
        void OnCastLine(DialogueLineInfo line)
        {
            if (_area == null) return;
            string who = !string.IsNullOrEmpty(line.speakerId) ? line.speakerId : line.speaker;
            var c = _area.Person(VhWardPeople.ModelForSpeaker(who));
            if (_talker != null && _talker != c) Quiet(_talker);
            _talker = c;
            if (c == null) return;
            int chars = line.text != null ? line.text.Length : 20;
            c.Say(Mathf.Clamp(chars / 14f, 0.8f, 7f));
            if (cam != null) c.LookAt(cam.transform);
            bool sitting = c.IsPlaying("sit") || c.IsPlaying("sit-talk");
            c.Play(sitting ? "sit-talk" : "talk", 0.35f);
        }

        static void Quiet(VhCharacter c)
        {
            if (c == null) return;
            c.Talk(false);
            bool sitting = c.IsPlaying("sit") || c.IsPlaying("sit-talk");
            c.Play(sitting ? "sit" : "idle", 0.5f);
        }

        /// <summary>The dialogue ended: the speaker goes back to standing (or sitting) still.</summary>
        void CastDialogueEnded()
        {
            if (_talker == null) return;
            Quiet(_talker);
            _talker = null;
        }

        // ---------- Thrown out of Night 2: the booth plays the house's lines (docs/redesign/house.md, section 5) ----------

        /// <summary>The knot with DreamHouseLines.ThrownOutBooth, word for word (VisitingHoursInkTests checks it).</summary>
        public const string KnotThrownOutBooth = "booth_thrown_out";

        /// <summary>
        /// She was thrown out of the house (the save's vh_n2_thrown_out; the cost itself is Chain.ForceOut's, kept by the
        /// runner): she comes to in the booth's chair, Mags beside her, and the house's lines play (House's
        /// DreamHouseLines.ThrownOutBooth), with the costs said plainly. Then the end card, as after any Night 2.
        /// </summary>
        IEnumerator ThrownOutBooth()
        {
            yield return Card("The booth", "11 PM", () =>
            {
                EnterArea(SliceArea.BoothDay2);
                SitInTheDreamChair();
            }, 1.6f, true);
            _hud.Toast(JamKit.DreamEscape.DreamHouseLines.ThrownOutCosts, 6f);
            yield return PlayWhenFree(KnotThrownOutBooth);
            float give = Time.realtimeSinceStartup + 3f;
            while (!GameRoot.Dialogue.IsActive && Time.realtimeSinceStartup < give) yield return null;
            while (GameRoot.Dialogue.IsActive) yield return null;
            float t = 0f;
            while (t < (Fast ? 0.05f : 1.2f)) { t += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>Her view from the booth's reclined chair, turned to Mags (she can't walk until the end card).</summary>
        void SitInTheDreamChair()
        {
            if (_area == null || _area.chairEye == Vector3.zero) return;
            var e = _area.root.TransformPoint(_area.chairEye);
            player.Teleport(new Vector3(e.x, _area.root.position.y + 0.05f, e.z));
            if (player.head != null) player.head.localPosition = new Vector3(0f, e.y - _area.root.position.y - 0.05f, 0f);
            player.Look(0f, 6f - player.Pitch);
            player.FaceYaw(_area.chairYaw);
            var mags = _area.Person("Mags");
            if (mags != null && cam != null) mags.LookAt(cam.transform);
        }

        /// <summary>Tests and the captures: the round done (every task right), today's finds made.</summary>
        public void DebugCompleteRound(params string[] known)
        {
            _handover = true;
            if (_round == null) _round = new DutyRound(DayNumber);
            foreach (var t in _round.Tasks) _round.Mark(t.id, false);
            if (known != null) foreach (var id in known) _state.journal.Record(id);
            RefreshRoundSheet();
        }
    }
}
