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
            _pending = Pending.None;
            _afterJournalKnot = false;
            _journal.Close();
            switch (Area)
            {
                case SliceArea.Hospital:
                    foreach (var id in new[] { SliceContent.FactChart, SliceContent.FactEffects, SliceContent.FactPoliceReport, SliceContent.FactPhoto }) _state.journal.Record(id);
                    StartCoroutine(GoToBooth());
                    break;
                case SliceArea.Booth:
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

        static Vector3 Approach(string id, Vector3 at)
        {
            switch (id)
            {
                case "chart": return new Vector3(4.2f, 0f, 19.5f);
                case "effects": return new Vector3(7.2f, 0f, 17.9f);
                case "police_report": return new Vector3(-2.4f, 0f, 4.9f);
                case "photo": return new Vector3(7.3f, 0f, 22.1f);
                case "stairwell": return new Vector3(0f, 0f, 24.6f);
                case "station": return new Vector3(-2.5f, 0f, 6.4f);
                case "elias": return new Vector3(5f, 0f, 20.4f);
                case "newspaper": return new Vector3(-8f, 0f, -14f);
                case "shop_sign": return new Vector3(25f, 0f, 12f);
                case "roses": return new Vector3(-14f, 0f, 10.8f);
                case "grill": return new Vector3(26.6f, 0f, 8.4f);
                case "walt": return new Vector3(21f, 0f, 8.6f);
                default: return at + new Vector3(0f, 0f, -1.2f);
            }
        }

        static void Log(string what) => Debug.Log("JAMKIT_SMOKE vh " + what);

        /// <summary>Mashes through the current dialogue (first choice at each choice), a line a frame.</summary>
        static IEnumerator MashDialogue(float timeout = 20f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (GameRoot.Dialogue.IsActive && Time.realtimeSinceStartup < end)
            {
                GameRoot.Dialogue.AutoStep();
                yield return null;
            }
        }

        IEnumerator WaitFor(Func<bool> condition, string what, float timeout = 10f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.AutoStep();
                if (Time.realtimeSinceStartup > end)
                {
                    Debug.LogError("JAMKIT_SMOKE vh timed out waiting for " + what + " (mode " + _mode + ", area " + Area + ")");
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
        /// Walt's argument with the autopilot (it says the key deduction: Convinced), the end card.</summary>
        IEnumerator SmokeDrive()
        {
            Log("start in " + Area);
            BattleSpeed = 1f;
            yield return WaitFor(() => Ready, "Day 1", 15f);
            if (Area == SliceArea.Hospital)
            {
                foreach (var id in new[] { "chart", "effects", "police_report", "photo" }) yield return LookAt(id);
                Log("journal: " + _state.journal.Count + " entries");
                yield return LookAt("stairwell");
                yield return WaitFor(() => Area == SliceArea.Booth, "the booth", 10f);
            }
            if (Area == SliceArea.Booth)
            {
                yield return WaitFor(() => _journal.IsOpen, "the journal to open in the booth", 15f);
                Log("journal opened by Mags (" + _journal.CurrentTab + ")");
                _journal.Close();
                yield return WaitFor(() => Area == SliceArea.Street && Ready, "the street", 20f);
            }
            if (Area != SliceArea.Street) { Debug.LogError("JAMKIT_SMOKE vh never reached the street"); yield break; }
            Log("on the street");
            foreach (var id in new[] { "newspaper", "shop_sign", "roses" }) yield return LookAt(id);
            // Walk up to the barbecue: Walt waves.
            yield return WaitFor(() => Ready, "free to see Walt");
            StandAt("walt");
            player.Teleport(player.transform.position + new Vector3(-4f, 0f, -4f));
            yield return WaitFor(() => _state.journal.Knows(SliceContent.MemWaltAlive), "Walt to wave", 5f);
            yield return MashDialogue();
            Log("open questions before combining: " + _state.journal.OpenQuestions(_openScratch));
            var r1 = Combine(SliceContent.FactEffects, SliceContent.MemWaltAlive);
            var r2 = Combine(SliceContent.MemNewspaper, SliceContent.MemSaturday);
            Log("combined: " + r1 + ", " + r2 + "; open questions now " + _state.journal.OpenQuestions(_openScratch));
            if (!_state.journal.Knows(SliceContent.DedWaltDead)) Debug.LogError("JAMKIT_SMOKE vh the key deduction wasn't made");
            _journal.Close();
            yield return null;

            Pilot = new[] { PilotAction.Listen, PilotAction.Listen, PilotAction.Touch, PilotAction.Touch };
            PilotInvulnerable = true;
            BattleSpeed = 14f;
            yield return LookAt("walt");
            yield return WaitFor(() => _mode == Mode.Battle, "the argument", 10f);
            Log("argument started");
            yield return WaitFor(() => _battle != null && _battle.Phase == BattlePhase.Done, "the argument to end", 20f);
            Log("outcome " + (_battle != null ? _battle.Outcome.ToString() : "none") + ", force " + _state.force);
            if (_battle == null || _battle.Outcome != BattleOutcome.Convinced) Debug.LogError("JAMKIT_SMOKE vh expected Convinced");
            yield return WaitFor(() => _mode == Mode.End, "the end card", 20f);
            Log("end card: " + _hud.EndBody.Replace("\n", " / "));
            Pilot = null;
            BattleSpeed = 1f;
        }
    }
}
