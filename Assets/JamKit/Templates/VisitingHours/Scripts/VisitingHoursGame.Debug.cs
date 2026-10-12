using System;
using System.Collections;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>The debug menu's "Next scene" (the next part of the slice), the smoke run's driver
    /// (it plays the whole slice by itself and logs what it did) and helpers for tests.</summary>
    public sealed partial class VisitingHoursGame
    {
        /// <summary>Next scene in the debug menu: Day 1 to the booth, the booth to the street, the street
        /// to the argument (everything found), the argument to Convinced, the end card to Victory.</summary>
        public bool DebugSkip()
        {
            switch (_mode)
            {
                case Mode.End:
                    Finish();
                    return true;
                case Mode.Battle:
                    if (_battle != null && _battle.Phase != BattlePhase.Done) _battle.ForceOutcome(BattleOutcome.Convinced);
                    return true;
                case Mode.Transition:
                case Mode.Loading:
                    return true;
            }
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            StopBedside();
            _pending = Pending.None;
            _afterJournalKnot = false;
            _journal.Close();
            switch (Area)
            {
                case SliceArea.Hospital:
                    DebugCompleteRound(SliceContent.FactChart, SliceContent.FactEffects, SliceContent.FactPoliceReport, SliceContent.FactPhoto);
                    StartCoroutine(GoToBooth());
                    break;
                case SliceArea.HospitalDay2:
                    DebugCompleteRound(SliceContent.FactDidLenaGetOut, SliceContent.FactVisitor);
                    StartCoroutine(GoToBooth());
                    break;
                case SliceArea.Booth:
                case SliceArea.BoothDay2:
                    StartCoroutine(Dive());
                    break;
                default:
                    foreach (var id in SliceContent.KnownBeforeWalt()) _state.journal.Record(id);
                    _waltWaved = true;
                    StartBattle();
                    break;
            }
            return true;
        }

        /// <summary>Jumps straight to a place with these entries already in the journal (tests).</summary>
        public void DebugJumpTo(SliceArea area, params string[] known)
        {
            StopAllCoroutines();
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            StopBedside();
            int day = area == SliceArea.HospitalDay2 ? 2 : 1;
            if ((area == SliceArea.Hospital || area == SliceArea.HospitalDay2) && (_round == null || _round.Day != day))
                _round = new DutyRound(day);
            _pending = Pending.None;
            _afterJournalKnot = false;
            _journal.Close();
            _battleView.Hide();
            _battle = null;
            _hud.HideEnd();
            _hud.SetCard(0f, "", "");
            if (known != null) foreach (var id in known) _state.journal.Record(id);
            _waltWaved = _state.journal.Knows(SliceContent.MemWaltAlive);
            EnterArea(area);
            _mode = Mode.Explore;
        }

        /// <summary>Puts Nadia a step from a thing, facing it (the smoke run "walks" there this way).</summary>
        public void StandAt(string spotId)
        {
            var s = _area != null ? _area.Spot(spotId) : null;
            if (s == null) return;
            Vector3 from = Approach(spotId, s.position);
            player.Teleport(new Vector3(from.x, 0.05f, from.z));
            Vector3 d = s.position - from;
            player.FaceYaw(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
        }

        /// <summary>Where Nadia stands to look at each thing (the smoke run, the captures, the autopilot).</summary>
        public static Vector3 Approach(string id, Vector3 at)
        {
            switch (id)
            {
                // Ward 4 West (VhWardFloor: the corridor along z 11-14, rooms 12/14/16 north, the station, staff room,
                // quiet room, lobby and family room south).
                case "rafael": return new Vector3(21.3f, 0f, 11.5f);
                case "rafael2": return new Vector3(11.9f, 0f, 7.0f);
                case "police_report": return new Vector3(18.3f, 0f, 11.6f);
                case "station": return new Vector3(23.9f, 0f, 11.6f);
                case "round_board": return new Vector3(19.6f, 0f, 5.9f);
                case "chart": return new Vector3(22.6f, 0f, 19.3f);
                case "effects": return new Vector3(25.5f, 0f, 16.0f);
                case "photo": return new Vector3(24.6f, 0f, 21.7f);
                case "stairwell":
                case "stairwell2": return new Vector3(42f, 0f, 12.5f);
                case "elias": return new Vector3(21.5f, 0f, 21.0f);
                case "wristband":
                case "wristband2": return new Vector3(23.6f, 0f, 21.65f);
                case "vitals":
                case "vitals2": return new Vector3(21.0f, 0f, 21.5f);
                case "round2": return new Vector3(20.3f, 0f, 21.1f);
                case "bouquet": return new Vector3(25.3f, 0f, 21.8f);
                case "sit":
                case "sit1": return new Vector3(24.55f, 0f, 20.5f);
                case "okafor": return new Vector3(10.9f, 0f, 19.7f);
                case "okafor_chart": return new Vector3(11.6f, 0f, 21.6f);
                case "brandt": return new Vector3(37.7f, 0f, 21.5f);
                case "brandt_band": return new Vector3(35.6f, 0f, 20.0f);
                case "locker": return new Vector3(7.5f, 0f, 4.9f);
                case "rota": return new Vector3(10.2f, 0f, 9.4f);
                case "kettle": return new Vector3(9.7f, 0f, 1.7f);
                case "june": return new Vector3(41.5f, 0f, 7.3f);
                case "tv": return new Vector3(42.5f, 0f, 9.0f);
                case "vending": return new Vector3(46.3f, 0f, 9.9f);
                case "visitor": return new Vector3(37.6f, 0f, 8.0f);
                case "remembrance_book": return new Vector3(1.8f, 0f, 8.3f);
                case "candles": return new Vector3(1.7f, 0f, 5.5f);
                case "west_window": return new Vector3(1.5f, 0f, 12.5f);
                case "phone": return new Vector3(9.95f, 0f, 7.0f);
                case "newspaper": return new Vector3(-8f, 0f, -14f);
                case "shop_sign": return new Vector3(25f, 0f, 12f);
                case "roses": return new Vector3(-14f, 0f, 10.8f);
                case "grill": return new Vector3(26.6f, 0f, 8.4f);
                case "walt": return new Vector3(21f, 0f, 8.6f);
                default: return at + new Vector3(0f, 0f, -1.2f);
            }
        }

        static void Log(string what) => Debug.Log("JAMKIT_SMOKE vh " + what);

        bool _smokeFailed;

        /// <summary>A step of the smoke run didn't happen: an error the smoke run counts (its own
        /// JAMKIT_SMOKE lines aren't counted, so this one isn't prefixed), and the driver stops.</summary>
        void SmokeFail(string why)
        {
            if (_smokeFailed) return;
            _smokeFailed = true;
            Debug.LogError("Visiting Hours smoke run: " + why + " (mode " + _mode + ", area " + Area + ")");
        }

        /// <summary>Mashes through the current dialogue (first choice at each choice): several lines a
        /// frame, so the whole slice fits the smoke run's budget.</summary>
        static IEnumerator MashDialogue(float timeout = 10f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (GameRoot.Dialogue.IsActive && Time.realtimeSinceStartup < end)
            {
                for (int i = 0; i < 4 && GameRoot.Dialogue.IsActive; i++) GameRoot.Dialogue.AutoStep();
                yield return null;
            }
        }

        IEnumerator WaitFor(Func<bool> condition, string what, float timeout = 6f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!_smokeFailed && !condition())
            {
                for (int i = 0; i < 4 && GameRoot.Dialogue.IsActive; i++) GameRoot.Dialogue.AutoStep();
                if (Time.realtimeSinceStartup > end)
                {
                    SmokeFail("timed out waiting for " + what);
                    yield break;
                }
                yield return null;
            }
        }

        bool Ready => _mode == Mode.Explore && !GameRoot.Dialogue.IsActive && GameRoot.Flow.Current == GameState.Playing && !_journal.IsOpen;

        /// <summary>Walks up to a thing, checks it's the one in reach, presses E (as Examine does) and reads it through.</summary>
        IEnumerator LookAt(string id)
        {
            yield return WaitFor(() => Ready, "free to look at " + id);
            if (_smokeFailed) yield break;
            StandAt(id);
            yield return null;
            yield return null;
            if (_target == null || _target.def.id != id) Log("note: in front of " + id + " the target was " + (_target != null ? _target.def.id : "nothing"));
            Examine(id);
            Log("looked at " + id);
            yield return null;
            yield return MashDialogue();
        }

        /// <summary>The smoke run: the whole slice, played by itself. Day 1 (the four facts), the booth
        /// (the journal opens, is closed), the street (the memories, Walt notices), two deductions,
        /// Walt's argument with the autopilot (it says the key deduction: Convinced), the end card.
        /// Any step that doesn't happen is an error, so the run fails instead of passing on the debug win.</summary>
        IEnumerator SmokeDrive()
        {
            _smokeFailed = false;
            Log("start in " + Area);
            BattleSpeed = 1f;
            yield return WaitFor(() => Ready, "Day 1", 8f);
            if (_smokeFailed) yield break;
            if (Area == SliceArea.Hospital)
            {
                // Day 1 (docs/redesign/day.md): the handover, the round's three tasks, the finds, the bedside, the stairs.
                foreach (var id in new[] { "rafael", "wristband", "vitals", "okafor", "chart", "effects", "police_report", "photo", "locker", "june", "remembrance_book" })
                {
                    yield return LookAt(id);
                    if (_smokeFailed) yield break;
                }
                Log("journal: " + _state.journal.Count + " entries; round " + _round.DoneCount + "/" + _round.Tasks.Count + ", slips " + _round.Slips);
                if (!_round.Complete) { SmokeFail("the round wasn't done"); yield break; }
                yield return WaitFor(() => Ready, "free to sit with him");
                if (_smokeFailed) yield break;
                AutoRise = true;
                StandAt("sit1");
                Examine("sit1");
                yield return WaitFor(() => _mode == Mode.Bedside, "the bedside to start");
                if (_smokeFailed) yield break;
                yield return WaitFor(() => _mode == Mode.Explore && _bedside == null, "the bedside to end", 40f);
                AutoRise = false;
                if (_smokeFailed) yield break;
                Log("bedside: told " + string.Join(", ", _toldToday) + "; dream changes " + string.Join(", ", Chain.DreamChanges(GameRoot.Save.Data)));
                yield return LookAt("stairwell");
                yield return WaitFor(() => Area == SliceArea.Booth, "the booth");
                if (_smokeFailed) yield break;
            }
            if (Area == SliceArea.Booth)
            {
                yield return WaitFor(() => _journal.IsOpen, "the journal to open in the booth");
                if (_smokeFailed) yield break;
                Log("journal opened by Mags (" + _journal.CurrentTab + ")");
                _journal.Close();
                // A long enough smoke walks the whole chain from here (the nights, Day 2, the end card): the runner lives on
                // the GameRoot, so it carries on when this scene goes.
                var runner = VhChainRunner.Instance;
                if (VhChainRunner.SmokeWalksTheChain && runner != null)
                {
                    Log("the chain: Night 1, Day 2, Night 2, the end card");
                    yield return runner.SmokeWalk();
                    yield break;
                }
                yield return WaitFor(() => Area == SliceArea.Street && Ready, "the street", 8f);
                if (_smokeFailed) yield break;
            }
            if (Area != SliceArea.Street) { SmokeFail("never reached the street"); yield break; }
            Log("on the street");
            foreach (var id in new[] { "newspaper", "shop_sign", "roses" })
            {
                yield return LookAt(id);
                if (_smokeFailed) yield break;
            }
            // Walk up to the barbecue: Walt waves.
            yield return WaitFor(() => Ready, "free to see Walt");
            if (_smokeFailed) yield break;
            StandAt("walt");
            player.Teleport(player.transform.position + new Vector3(-4f, 0f, -4f));
            yield return WaitFor(() => _state.journal.Knows(SliceContent.MemWaltAlive), "Walt to wave");
            if (_smokeFailed) yield break;
            yield return MashDialogue();
            Log("open questions before combining: " + _state.journal.OpenQuestions(_openScratch));
            var r1 = Combine(SliceContent.FactEffects, SliceContent.MemWaltAlive);
            var r2 = Combine(SliceContent.MemNewspaper, SliceContent.MemSaturday);
            Log("combined: " + r1 + ", " + r2 + "; open questions now " + _state.journal.OpenQuestions(_openScratch));
            if (!_state.journal.Knows(SliceContent.DedWaltDead)) { SmokeFail("the key deduction wasn't made"); yield break; }
            _journal.Close();
            yield return null;

            Pilot = new[] { PilotAction.Listen, PilotAction.Listen, PilotAction.Touch, PilotAction.Touch };
            PilotInvulnerable = true;
            BattleSpeed = 22f;
            yield return LookAt("walt");
            yield return WaitFor(() => _mode == Mode.Battle, "the argument");
            if (_smokeFailed) yield break;
            Log("argument started");
            yield return WaitFor(() => _battle != null && _battle.Phase == BattlePhase.Done, "the argument to end", 10f);
            if (_smokeFailed) yield break;
            Log("outcome " + _battle.Outcome + ", force " + _state.force);
            if (_battle.Outcome != BattleOutcome.Convinced) { SmokeFail("expected Convinced, got " + _battle.Outcome); yield break; }
            yield return WaitFor(() => _mode == Mode.End, "the end card", 8f);
            if (_smokeFailed) yield break;
            Log("end card: " + _hud.EndBody.Replace('\n', ' '));
            Pilot = null;
            BattleSpeed = 1f;
        }
    }
}
