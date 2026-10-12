using System.Collections;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using JamKit.Stealth;
using JamKit.VisitingHours;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 2's hide-and-seek (docs/redesign/house.md "Hide-and-seek"): Theo seeks (core <see cref="TheoBrain"/>, his
    /// figure <see cref="TheoView"/>), she finds the memories (each one up close, its thought, the counter), and being found
    /// costs: the house takes back the last memory she found and hides it somewhere new, he counts again (less each time),
    /// and the third time ends the round with whatever she's holding. Every memory found: the creator's line, and he comes
    /// for her. What she told Elias at his bedside changes the house (Chain.HasDreamChange).
    /// </summary>
    public sealed partial class DreamHouseGame
    {
        /// <summary>Theo's door, shut while his room is the base (a door leaf with a collider in his doorway).</summary>
        public GameObject theosDoorShut;

        bool _theoRoomOpen = true, _comingSaid;
        string _promptMemory;

        static SaveData SaveOrNull => GameRoot.Exists ? GameRoot.Save.Data : null;
        static bool Told(string change) => Chain.HasDreamChange(SaveOrNull, change);

        /// <summary>What she told Elias at his bedside, read as the visit starts (docs/redesign/day.md): the lake photo is
        /// up already (and counts), Lena waits at the window (warier) or greets her gently, Theo's room is open.</summary>
        void ApplyBedside()
        {
            if (Told(DreamChanges.PhotoWall))
            {
                _round.MarkFound(new[] { DreamHouseStory.FactPhoto });
                var v = ViewOf(DreamHouseStory.FactPhoto);
                if (v != null) v.SetFound(true);
            }
            if (Told(DreamChanges.LenaWary) && lena != null) lena.position = ToWorld(new NVec3(5.7f, 0f, 0.75f));
        }

        void BeginHideAndSeek()
        {
            if (!_story.StartHideAndSeek()) return;
            bool team = Told(DreamChanges.TheoRoomOpen);
            SetTheoRoom(HideAndSeekRound.TheoRoomOpen(team, _round.FoundCount));
            float count = _round.CountTime + (Told(DreamChanges.LenaSofter) ? 5f : 0f);
            _theo.StartCounting(_h.theoStart, _h.theoCountYaw, count, 0);
            PlaceTheo();
            _comingSaid = false;
            _peekHintDone = false;
            _peekHintText = null;
            _hud.ShowCounter(true);
            UpdateCounter();
            _hud.Title("Hide and seek");
            if (team) Say(DreamHouseLines.TheoTeam, null);
            else if (!_theoRoomOpen) Say(DreamHouseLines.TheoRoomShut, null);
            Debug.Log("JAMKIT_HOUSE hide-and-seek begins: " + _round.Counter + (_theoRoomOpen ? "" : ", Theo's room shut"));
        }

        void SetTheoRoom(bool open)
        {
            _theoRoomOpen = open;
            if (theosDoorShut != null) theosDoorShut.SetActive(!open);
            _theo.SetRoomOpen("theo", open);
        }

        // ---------- Teaching the peek ----------

        bool _peekHintDone, _peekHintPad;
        int _peekHintVersion = -1;
        string _peekHintText;
        const string PeekPromptId = "peek";

        /// <summary>The first time it's useful (Theo's first count): "[LB / RB]  Hold to peek round corners" in the prompt
        /// line, in the keys of the device in her hands, until she's peeked once or he's done counting. A memory's own prompt
        /// takes the line while she's at one.</summary>
        void PeekHint()
        {
            var input = GameRoot.Input;
            if (!_peekHintDone && (!_theo.Counting || Mathf.Abs(player.LeanFraction) > 0.6f || !input.Bindings.PeekOnBumpers)) _peekHintDone = true;
            if (_peekHintDone)
            {
                if (_promptFor == PeekPromptId) { _promptFor = null; _hud.Prompt(null); }
                return;
            }
            bool pad = input.PadMode;
            if (_peekHintText == null || pad != _peekHintPad || input.Bindings.Version != _peekHintVersion)
            {
                _peekHintPad = pad;
                _peekHintVersion = input.Bindings.Version;
                _peekHintText = "[" + PeekKeys(pad) + "]  Hold to peek round corners";
                if (_promptFor == PeekPromptId) _hud.Prompt(_peekHintText);
            }
            if (_promptFor == null)
            {
                _promptFor = PeekPromptId;
                _hud.Prompt(_peekHintText);
            }
        }

        /// <summary>"LB / RB" (or "L1 / R1", or "Q / F" on the keyboard): the peek keys as bound now.</summary>
        static string PeekKeys(bool pad)
        {
            var b = GameRoot.Input.Bindings;
            var style = ControlNames.DetectPadStyle();
            var col = pad ? ControlColumn.Gamepad : ControlColumn.KeyboardMouse;
            var l = b.FindRow("peek-left");
            var r = b.FindRow("peek-right");
            string left = l != null ? b.SlotName(l.Action, l.Primary(col), style) : "";
            string right = r != null ? b.SlotName(r.Action, r.Primary(col), style) : "";
            if (left.Length == 0) left = pad ? "LB" : "Q";
            if (right.Length == 0) right = pad ? "RB" : "F";
            return left + " / " + right;
        }

        /// <summary>The peek hint's text now (tests).</summary>
        public string PeekHintText => _promptFor == PeekPromptId ? _peekHintText : null;

        void UpdateCounter() => _hud.Counter(_round.FoundCount, _round.Total, _round.Catches, HideAndSeekRound.MaxCatches);

        /// <summary>Her as Theo's senses take her this frame (house space): feet, height, speed, posture, the hiding place
        /// she's in, and the peek: her head where her eyes really are, so peeking round a door frame shows him a small
        /// head, not the whole of her (StealthSight).</summary>
        public StealthTarget HerTarget()
        {
            var f = Feet;
            var sim = player.Sim;
            var target = StealthTarget.At(f, sim != null ? sim.Height : 1.7f);
            if (sim != null)
            {
                target.speed = sim.HorizontalSpeed;
                target.crouched = sim.Crouched;
                target.sliding = sim.Mode == JamKit.Movement.MoveMode.Slide;
            }
            Vector3 lean = houseRoot.InverseTransformVector(player.LeanWorld);
            target.lean = new NVec3(lean.x, 0f, lean.z);
            target.hidingSpot = _h.SpotAt(f);
            return target;
        }

        void TickHideAndSeek(float dt)
        {
            var f = Feet;
            var target = HerTarget();
            int spot = target.hidingSpot;
            _theo.SetHerSpot(spot);
            var noise = default(StealthNoise);
            var sp = StealthPlayer.Current;
            if (sp != null && sp.Noise.Any)
            {
                noise = sp.Noise;
                noise.position = ToHouse(new Vector3(sp.Noise.position.X, sp.Noise.position.Y, sp.Noise.position.Z));
            }
            if (!_busy) _theo.Tick(dt, target, noise);
            PlaceTheo();
            TheoSpeaks(_theo.Events);
            if (_theo.Events.found) { Caught(); return; }
            if (_round.AllFound && _theo.State != TheoState.Coming && _theo.State != TheoState.Celebrating && !_busy) _theo.ComeFind(f);
            Eye();
            Memories();
            PeekHint();
            if (!_theoRoomOpen && HideAndSeekRound.TheoRoomOpen(false, _round.FoundCount))
            {
                SetTheoRoom(true);
                _hud.TheoCalls(DreamHouseLines.TheoRoomOpens[0].text);
            }
        }

        void PlaceTheo()
        {
            if (theo == null) return;
            theo.Place(ToWorld(_theo.Position), WorldYaw(_theo.Yaw), _theo.Meter, _theo.Counting, _theo.Moving);
        }

        void TheoSpeaks(TheoEvents ev)
        {
            switch (ev.say)
            {
                case TheoSay.Count: _hud.TheoCalls(DreamHouseLines.Count(ev.arg)); break;
                case TheoSay.CountDone: _hud.TheoCalls(DreamHouseLines.CountDone); GameRoot.Audio.Play("chime", 0.35f); break;
                case TheoSay.Peeked: _hud.TheoCalls(DreamHouseLines.Peeked[ev.arg % DreamHouseLines.Peeked.Length]); break;
                case TheoSay.SawYouGo: _hud.TheoCalls(DreamHouseLines.SawYouGo[Mathf.Clamp(ev.arg, 0, 4)]); break;
                case TheoSay.RoomCall: _hud.TheoCalls(DreamHouseLines.RoomCall(ev.room)); break;
                case TheoSay.Heard: _hud.TheoCalls(DreamHouseLines.Heard[ev.arg % DreamHouseLines.Heard.Length]); break;
                case TheoSay.Suspicious: _hud.TheoCalls(DreamHouseLines.Suspicious[ev.arg % DreamHouseLines.Suspicious.Length]); break;
                case TheoSay.CheckedEmpty: _hud.TheoCalls(DreamHouseLines.CheckedEmpty[ev.arg % DreamHouseLines.CheckedEmpty.Length]); GameRoot.Audio.Play("click", 0.3f); break;
                case TheoSay.Giggle: _hud.TheoCalls(DreamHouseLines.Giggle[ev.arg % DreamHouseLines.Giggle.Length]); break;
                case TheoSay.LostYou: _hud.TheoCalls(DreamHouseLines.LostYou[ev.arg % DreamHouseLines.LostYou.Length]); break;
                case TheoSay.AsksMum: if (!_speaking) Say(DreamHouseLines.AsksMum, null); break;
                case TheoSay.Coming:
                    if (!_comingSaid) { _comingSaid = true; _hud.TheoCalls(DreamHouseLines.ComingToFindYou); }
                    break;
            }
        }

        // "Theo's counting  N" and "Theo's peeking!  N", made once per number (no string per frame).
        static readonly string[] CountingWords = new string[64], PeekingWords = new string[64];
        static string CountWord(bool peeking, int n)
        {
            n = Mathf.Clamp(n, 0, 63);
            var cache = peeking ? PeekingWords : CountingWords;
            return cache[n] ?? (cache[n] = (peeking ? "Theo's peeking!" : "Theo's counting") + "  " + n);
        }

        void Eye()
        {
            string word;
            switch (_theo.State)
            {
                case TheoState.Counting: word = CountWord(_theo.Peeking, Mathf.CeilToInt(_theo.CountLeft)); break;
                case TheoState.Coming: word = "He's coming to find you"; break;
                case TheoState.Investigating: word = _theo.Meter > 0.3f ? "He's seen something" : "He heard something"; break;
                case TheoState.Celebrating: word = "Found!"; break;
                default: word = _theo.Meter > 0.5f ? "He sees you!" : _theo.Meter > 0.05f ? "Hmm...?" : "He's looking for you"; break;
            }
            _hud.Eye(_theo.Meter, word);
        }

        // ---------- The memories ----------

        HouseMemoryView ViewOf(string id)
        {
            foreach (var v in memoryViews) if (v != null && v.id == id) return v;
            return null;
        }

        void Memories()
        {
            if (_busy || player.cam == null) return;
            var cam = player.cam.transform;
            HouseMemoryView near = null;
            foreach (var v in memoryViews)
            {
                if (v == null || _round.IsFound(v.id)) continue;
                // The autopilot measures from her eyes like the player does (from her feet it never reached the lake photo
                // hung over the stairs, and stood under it for the rest of the recorded run).
                if (AutopilotOn ? Vector3.Distance(cam.position, v.transform.position) < 2.4f : v.InReach(cam.position, cam.forward)) { near = v; break; }
            }
            string id = near != null ? near.id : null;
            if (id != _promptMemory)
            {
                _promptMemory = id;
                SetPrompt(id == null ? null : "mem:" + id, id == null ? null : "Take the memory: " + _h.Memory(id).name.ToLowerInvariant());
            }
            if (near != null && (InteractPressed() || AutopilotOn)) Collect(near.id, false);
        }

        /// <summary>She takes a memory (by sitting at the table, by looking at it and pressing the key, the autopilot).</summary>
        public void Collect(string id, bool quiet)
        {
            if (!_round.Collect(id, out bool last)) return;
            var v = ViewOf(id);
            if (v != null) v.SetFound(true);
            _promptMemory = null;
            SetPrompt(null, null);
            GameRoot.Audio.Play("vh_journal", 0.6f);
            bool paper = HasSaturdayPaper || _round.IsFound(DreamHouseStory.MemSaturdayPaper);
            var lines = DreamHouseLines.MemoryLine(id, paper);
            _hud.Memory(v != null ? v.picture : null, lines.Length > 0 ? lines[0].text : "", 3.4f);
            UpdateCounter();
            Debug.Log("JAMKIT_HOUSE memory: " + id + " (" + _round.Counter + ")");
            if (quiet) return;
            if (last)
            {
                var all = new List<HouseLine>(lines) { new HouseLine(DreamHouseLines.Nadia, DreamHouseLines.AllFound(DreamEscapeGame.Despair), 6.5f) };
                Say(all, () => _theo.ComeFind(Feet));
                Debug.Log("JAMKIT_HOUSE every memory found: \"" + DreamHouseLines.AllFound(DreamEscapeGame.Despair) + "\"");
            }
            else Say(lines, null);
        }

        // ---------- Being found ----------

        /// <summary>Debug and tests: Theo finds her where she stands (the same cost as his finding her).</summary>
        public void FoundNow()
        {
            if (_story.Phase == HousePhase.HideAndSeek && !_round.Over && !_busy) Caught();
        }

        void Caught()
        {
            var cost = _round.Caught(Feet);
            GameRoot.Audio.Play("mv_caught", 0.6f);
            GameRoot.Juice.Flash(new Color(1f, 0.92f, 0.78f, 0.45f), 0.35f);
            if (cameraFx != null) cameraFx.Roll(5f, 0.6f);
            if (cost.roundOver)
            {
                _hud.TheoCalls(_round.End == RoundEnd.CaughtThreeTimes ? DreamHouseLines.FoundYou[2] : "Found you! You let me, didn't you?");
                UpdateCounter();
                StartCoroutine(RoundOver());
                return;
            }
            _hud.TheoCalls(DreamHouseLines.FoundYou[Mathf.Clamp(cost.catchNumber - 1, 0, 2)]);
            if (cost.memoryTaken != null)
            {
                var v = ViewOf(cost.memoryTaken);
                if (v != null) v.MoveTo(ToWorld(cost.newPosition), MemoryYaw(cost.newPosition, cost.newStandAt));
                Say(DreamHouseLines.Caught[Mathf.Clamp(cost.catchNumber - 1, 0, 2)], null);
                Debug.Log("JAMKIT_HOUSE caught (" + cost.catchNumber + "): the house took back " + cost.memoryTaken + " and hid it in " + cost.newRoom);
            }
            else Say(DreamHouseLines.CaughtEmptyHanded, null);
            UpdateCounter();
            // He counts again where he found her, a little less each time.
            _theo.StartCounting(_theo.Position, _theo.Yaw, _round.CountTime, _round.Catches);
            _comingSaid = false;
        }

        IEnumerator RoundOver()
        {
            _busy = true;
            _theo.Stop();
            _hud.ShowCounter(false);
            SetPrompt(null, null);
            _story.RoundOver();
            Debug.Log("JAMKIT_HOUSE the round is over: " + _round.End + ", " + _round.Counter + ", caught " + _round.Catches);
            if (_round.End == RoundEnd.CaughtThreeTimes)
            {
                bool done = false;
                Say(DreamHouseLines.Caught[2], () => done = true);
                while (!done) yield return null;
            }
            yield return new WaitForSecondsRealtime(0.4f);
            AskTheo();
        }
    }
}
