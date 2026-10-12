using System;
using System.Collections;
using Ink.Runtime;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>The slice's story side: places and the cards between them, the ink (what it
    /// records, what it asks for next), the journal's tutorial, Walt noticing you, and the end.</summary>
    public sealed partial class VisitingHoursGame
    {
        /// <summary>What an ink event asked for, done when that dialogue ends.</summary>
        enum Pending { None, ToBooth, JournalTutorial, Dive, Battle, Questions }

        Pending _pending;
        bool _afterJournalKnot, _waltWaved;
        int _rushingShown;
        float _areaTime;

        /// <summary>How many times the rushing check spoke ("I don't know enough yet.").</summary>
        public int RushingShown => _rushingShown;

        // ---------- Places and cards ----------

        IEnumerator BeginDay()
        {
            _mode = Mode.Transition;
            StartDayState();
            yield return Card("St. Brigid's", "Six weeks later", null, 2.4f, true, EnterAreaStaged(SliceArea.Hospital));
            _mode = Mode.Explore;
            _hud.ShowControls(16f);
            yield return PlayWhenFree(SliceContent.KnotDay1Intro);
        }

        /// <summary>Day 2 (docs/NIGHTS-1-2.md): short. Rafael's question, the visitor from LeBaget, one round, the chair by
        /// his bed, then the stairs down to the booth.</summary>
        IEnumerator BeginDay2()
        {
            _mode = Mode.Transition;
            StartDayState();
            yield return Card("St. Brigid's", "Day 2", null, 2.4f, true, EnterAreaStaged(SliceArea.HospitalDay2));
            _mode = Mode.Explore;
            yield return PlayWhenFree(SliceContent.KnotDay2Intro);
        }

        /// <summary>Black, the place's name, <paramref name="middle"/> while it's dark (or <paramref name="staged"/>, a frame a
        /// step: the hold counts from when it's done), then the fade back.</summary>
        IEnumerator Card(string title, string sub, Action middle, float hold, bool startBlack = false, IEnumerator staged = null)
        {
            float k = Fast ? 0.03f : 1f;
            _hud.SetCard(startBlack ? 1f : 0f, title, sub);
            if (!startBlack)
                for (float t = 0f; t < 0.9f * k; t += Time.unscaledDeltaTime)
                {
                    _hud.SetCard(t / (0.9f * k), null, null);
                    yield return null;
                }
            _hud.SetCard(1f, null, null);
            middle?.Invoke();
            if (staged != null)
            {
                float started = Time.realtimeSinceStartup;
                while (staged.MoveNext()) yield return staged.Current;
                // The building took part of the hold already (it happened under the same black).
                hold = Mathf.Max(0.3f, hold - (Time.realtimeSinceStartup - started) / Mathf.Max(0.01f, k));
            }
            for (float t = 0f; t < hold * k; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < 1.2f * k; t += Time.unscaledDeltaTime)
            {
                _hud.SetCard(1f - t / (1.2f * k), null, null);
                yield return null;
            }
            _hud.SetCard(0f, "", "");
        }

        /// <summary>Builds a place (the old one goes), puts Nadia at its start and sets its look and sound.</summary>
        public void EnterArea(SliceArea which)
        {
            if (_area != null && _area.root != null) Destroy(_area.root.gameObject);
            _area = VhWorld.Build(which, config, worldRoot);
            AreaBuilt(which);
        }

        /// <summary>
        /// <see cref="EnterArea"/> a step a frame (the ward room by room, docs/redesign/perf.md Day item 3), for building under
        /// a black card: the old place goes now, the new one is complete (and she's in it) when this ends.
        /// </summary>
        IEnumerator EnterAreaStaged(SliceArea which)
        {
            if (_area != null && _area.root != null) Destroy(_area.root.gameObject);
            _area = null;
            var area = VhWorld.NewArea(which, worldRoot);
            var steps = VhWorld.BuildSteps(area, config);
            while (steps.MoveNext()) yield return null;
            _area = area;
            AreaBuilt(which);
        }

        /// <summary>A place is up: she's at its start, its look and sound are set, its people are hooked up.</summary>
        void AreaBuilt(SliceArea which)
        {
            _areaTime = 0f;
            _target = null;
            player.Spawn(_area.spawn, _area.spawnYaw);
            ApplyLook(which);
            _hud.SetDim(0f);
            var audio = GameRoot.Audio;
            audio.StopMusic(1.5f);
            switch (which)
            {
                case SliceArea.Hospital:
                case SliceArea.HospitalDay2: audio.PlayAmbience("amb_vh_hospital"); break;
                case SliceArea.Booth:
                case SliceArea.BoothDay2: audio.PlayAmbience("amb_hum"); break;
                default: audio.PlayAmbience("amb_vh_suburb"); break;
            }
            // Room 14's monitor beeps by itself now (VhPatientMonitor); the old loop only where there's none.
            bool ward = which == SliceArea.Hospital || which == SliceArea.HospitalDay2;
            SetMonitor(ward && _area.patientMonitor == null ? _area.monitor : (Vector3?)null);
            RefreshRoundSheet();
            CastEntered();
        }

        void SetMonitor(Vector3? at)
        {
            if (at == null)
            {
                if (_monitor != null) _monitor.Stop();
                return;
            }
            if (_monitor == null)
            {
                var go = new GameObject("Heart monitor");
                go.transform.SetParent(transform, false);
                _monitor = go.AddComponent<AudioSource>();
                _monitor.playOnAwake = false;
                _monitor.loop = true;
                _monitor.spatialBlend = 1f;
                _monitor.minDistance = 2f;
                _monitor.maxDistance = 22f;
                _monitor.rolloffMode = AudioRolloffMode.Linear;
                _monitor.clip = ProceduralAudio.Get("vh_monitor");
            }
            _monitor.transform.position = at.Value;
            _monitor.mute = GameRoot.Audio.Muted;
            _monitor.volume = 0.55f * GameRoot.Settings.sfxVolume;
            if (!_monitor.isPlaying) _monitor.Play();
        }

        IEnumerator GoToBooth()
        {
            _mode = Mode.Transition;
            bool day2 = _segment == ChainSegment.Day2;
            yield return Card("11 PM", "The closed sleep-research wing", () => EnterArea(day2 ? SliceArea.BoothDay2 : SliceArea.Booth), 2.4f);
            _mode = Mode.Explore;
            yield return PlayWhenFree(day2 ? SliceContent.KnotBooth2 : SliceContent.KnotBooth);
        }

        /// <summary>
        /// Under: the chain's night when it's on and its scene is in the build (Day 1's booth: Night 1 on the hill street;
        /// Day 2's: Night 2 in the house), carrying the journal and Force. Otherwise the slice's own street (Day 1), or the
        /// end card (Day 2: there's no house to go to).
        /// </summary>
        IEnumerator Dive()
        {
            _mode = Mode.Transition;
            GameRoot.Audio.Play("glitch", 0.4f);
            var night = _segment == ChainSegment.Day2 ? ChainSegment.Night2 : ChainSegment.Night1;
            var runner = VhChainRunner.Instance;
            if (runner != null && VhChainRunner.Enabled && VhChainRunner.NightScene(night) != null)
            {
                bool started = false;
                // Night 2 starts on the hill, outside the house on Rowan Street (World's request), when the hill's in the build.
                string title = night == ChainSegment.Night1 ? "Larch Street"
                    : VhChainRunner.NightScene(night) == Chain.HillScene ? "Rowan Street" : "The house";
                yield return CardThenHold(title, night == ChainSegment.Night2 ? "Night 2" : "Night 1", () => started = runner.StartNight(night, _state.journal, _state.force), 2f);
                if (started) yield break;
                _hud.SetCard(0f, "", "");
            }
            if (_segment == ChainSegment.Day2)
            {
                yield return ChainEnd();
                yield break;
            }
            yield return Card("Larch Street", "Night 1", () => EnterArea(SliceArea.Street), 2f);
            _mode = Mode.Explore;
            yield return PlayWhenFree(SliceContent.KnotNightArrive);
        }

        /// <summary>A card that fades to black, does <paramref name="middle"/> (the next scene's load) and stays black.</summary>
        IEnumerator CardThenHold(string title, string sub, Action middle, float hold)
        {
            float k = Fast ? 0.03f : 1f;
            _hud.SetCard(0f, title, sub);
            for (float t = 0f; t < 0.9f * k; t += Time.unscaledDeltaTime)
            {
                _hud.SetCard(t / (0.9f * k), null, null);
                yield return null;
            }
            _hud.SetCard(1f, null, null);
            for (float t = 0f; t < hold * k; t += Time.unscaledDeltaTime) yield return null;
            middle?.Invoke();
        }

        // ---------- The chain's end card ----------

        /// <summary>
        /// The end of the chain (after Night 2): what happened with Walt and Lena, Force and Despair, how much the journal
        /// holds. PLACEHOLDER (agent-written): the creator's ending isn't written yet.
        /// </summary>
        IEnumerator ChainEnd()
        {
            _mode = Mode.Transition;
            // Thrown out of the house: she comes to in the booth first, and Mags says what it cost (the house's lines).
            if (Chain.ThrownOutOfNight2(SaveData)) yield return ThrownOutBooth();
            if (_area == null) EnterArea(SliceArea.BoothDay2);
            _hud.SetDim(1f);
            GameRoot.Audio.StopAmbience(1f);
            yield return Card("St. Brigid's", "The next morning", null, 1.6f, true);
            _hud.SetDim(1f);
            ShowChainEnd();
        }

        void ShowChainEnd()
        {
            _mode = Mode.End;
            _hud.ShowEnd("End of Night 2.", ChainEndBody(), Finish, RetryNight2, "Go back into the house");
        }

        /// <summary>The end card's text (tests read it).</summary>
        public string ChainEndBody()
        {
            var save = GameRoot.Exists ? GameRoot.Save.Data : null;
            var sb = new System.Text.StringBuilder();
            var walt = Chain.BattleResult(save, JamKit.Argument.ArgumentBattles.Walt);
            var lena = Chain.BattleResult(save, JamKit.Argument.ArgumentBattles.Lena);
            sb.Append("Walt: ").Append(walt != null ? walt.outcome.ToString() : NonEmpty(Chain.WaltOutcome(save), "not argued")).Append('.');
            sb.Append("\nLena: ").Append(lena != null ? (lena.Condemns ? "condemns her" : "unsure") : NonEmpty(Chain.LenaOutcome(save), "not argued")).Append('.');
            sb.Append("\nForce ").Append(Chain.ForceTally(save)).Append(", Despair ").Append(Chain.DespairTally(save)).Append(" of 3");
            if (Chain.Despair(save)) sb.Append(" (she went on into the rain)");
            sb.Append(".\nThe journal holds ").Append(Chain.Known(save).Count).Append(" entries.");
            sb.Append("\n\n(A placeholder end card: the ending isn't written yet.)");
            return sb.ToString();
        }

        static string NonEmpty(string s, string fallback) => string.IsNullOrEmpty(s) ? fallback : s;

        void RetryNight2()
        {
            var runner = VhChainRunner.Instance;
            if (runner != null) runner.JumpTo(ChainSegment.Night2);
        }

        /// <summary>Per-frame life of a place: the dog wanders, Walt waves when you first see him.</summary>
        void TickArea(float dt)
        {
            _areaTime += dt;
            TickCast();
            if (_area == null || _area.area != SliceArea.Street) return;
            if (_area.dog != null)
            {
                var p = _area.dogHome + new Vector3(Mathf.Sin(_areaTime * 0.35f) * 4f, 0f, Mathf.Sin(_areaTime * 0.21f) * 1.2f);
                _area.dog.transform.position = p;
                var spot = _area.Spot("dog");
                if (spot != null) spot.position = p + Vector3.up * 0.4f;
            }
            if (!_waltWaved && _area.walt != null && !GameRoot.Dialogue.IsActive && !_state.journal.Knows(SliceContent.MemWaltAlive))
            {
                Vector3 to = _area.walt.transform.position + Vector3.up * 1.4f - player.EyePosition;
                if (to.magnitude < config.waltNoticeRange && Vector3.Dot(to.normalized, cam.transform.forward) > 0.85f)
                {
                    _waltWaved = true;
                    Play(SliceContent.KnotWaltWave);
                }
            }
        }

        // ---------- Examining and talking ----------

        /// <summary>What E does on a thing: plays its knot (Walt: the rushing check, then the argument).</summary>
        public void Examine(string id)
        {
            var def = config.FindExaminable(id);
            if (def == null || GameRoot.Dialogue.IsActive) return;
            if (id == "walt")
            {
                TalkToWalt();
                return;
            }
            if (ExamineDay(id)) return;
            Play(def.knot);
        }

        void TalkToWalt()
        {
            if (!_state.journal.Knows(SliceContent.MemWaltAlive))
            {
                // Walking straight up to him: you remember him at the grill first.
                _waltWaved = true;
                _state.journal.Record(SliceContent.MemWaltAlive);
                Toast(SliceContent.MemWaltAlive);
            }
            if (_state.journal.HasOpenQuestions)
            {
                _rushingShown++;
                Play(SliceContent.KnotWaltRushing);
            }
            else Play(SliceContent.KnotWaltIntro);
        }

        /// <summary>Plays a knot once the game is Playing and no other dialogue runs (a pause or a
        /// running line never gets a dialogue started underneath it).</summary>
        IEnumerator PlayWhenFree(string knot)
        {
            yield return null;
            while (GameRoot.Flow.Current != Flow.GameState.Playing || GameRoot.Dialogue.IsActive || GameRoot.IsLoadingScene) yield return null;
            Play(knot);
        }

        bool Play(string knot)
        {
            if (config.story == null || string.IsNullOrEmpty(knot)) return false;
            return GameRoot.Dialogue.Play(config.story, knot);
        }

        // ---------- Ink ----------

        void BindInk(Story story)
        {
            story.BindExternalFunction("knows", new Func<string, object>(id => _state != null && _state.journal.Knows(id)), true);
            story.BindExternalFunction("open_questions", new Func<object>(() => _state != null ? _state.journal.OpenQuestions(_openScratch) : 0), true);
            story.BindExternalFunction("vh_state", new Func<string, object>(name => DayState(name)), true);
            story.BindExternalFunction("vh_topic", new Func<string, object>(id => TopicOpen(id)), true);
        }

        void OnInkEvent(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("vh:", StringComparison.Ordinal)) return;
            const string record = "vh:record:";
            if (name.StartsWith(record, StringComparison.Ordinal))
            {
                Record(name.Substring(record.Length));
                return;
            }
            if (DayEvent(name)) return;
            switch (name)
            {
                case "vh:to_booth": _pending = Pending.ToBooth; break;
                case "vh:open_journal": _pending = Pending.JournalTutorial; break;
                case "vh:dive": _pending = Pending.Dive; break;
                case "vh:battle": _pending = Pending.Battle; break;
                case "vh:questions": _pending = Pending.Questions; break;
                case "vh:questions_hint": break;
                default: Debug.LogWarning("Visiting Hours: unknown ink event \"" + name + "\""); break;
            }
        }

        void OnDialogueEnded(string knot)
        {
            DayDialogueEnded(knot);
            var p = _pending;
            _pending = Pending.None;
            switch (p)
            {
                case Pending.ToBooth: StartCoroutine(GoToBooth()); break;
                case Pending.Dive: StartCoroutine(Dive()); break;
                case Pending.Battle: StartCoroutine(NextFrame(StartBattle)); break;
                case Pending.JournalTutorial:
                    _afterJournalKnot = true;
                    StartCoroutine(NextFrame(() => { _journal.Open(VhJournalScreen.Tab.Facts, true); _journalOpenedThisFrame = true; }));
                    break;
                case Pending.Questions:
                    StartCoroutine(NextFrame(() => { _journal.Open(VhJournalScreen.Tab.Facts, false); _journalOpenedThisFrame = true; }));
                    break;
            }
        }

        static IEnumerator NextFrame(Action a)
        {
            yield return null;
            a();
        }

        void OnJournalClosed()
        {
            if (!_afterJournalKnot || !isActiveAndEnabled) return;
            _afterJournalKnot = false;
            StartCoroutine(PlayWhenFree(SliceContent.KnotBoothAfterJournal));
        }

        /// <summary>Writes an entry in the journal (the toast says so). False if it was already there.</summary>
        public bool Record(string id)
        {
            if (!_state.journal.Record(id))
            {
                if (_state.journal.Book.Find(id) == null) Debug.LogWarning("Visiting Hours: the ink records an unknown entry \"" + id + "\"");
                return false;
            }
            Toast(id);
            return true;
        }

        void Toast(string id)
        {
            var e = _state.journal.Book.Find(id);
            if (e == null) return;
            string verb;
            switch (e.kind)
            {
                case EntryKind.Fact: verb = "Charted: "; break;
                case EntryKind.Memory: verb = "Remembered: "; break;
                case EntryKind.Deduction: verb = "Worked out: "; break;
                case EntryKind.Truth: verb = "A truth: "; break;
                default: verb = "Kept: "; break;
            }
            _hud.Toast(verb + e.title + "      J  journal");
            GameRoot.Audio.Play("vh_journal", 0.8f);
        }

        /// <summary>The journal tried a pair (the screen calls this).</summary>
        public void OnCombined(string a, string b, CombineResult result, string made)
        {
            if (result == CombineResult.Made)
            {
                GameRoot.Audio.Play("chime", 0.5f);
                Toast(made);
            }
            else GameRoot.Audio.Play("vh_wrong", 0.5f);
        }

        /// <summary>Tries a pair as the journal's Combine does (tests, the smoke run).</summary>
        public CombineResult Combine(string a, string b)
        {
            _journal.Pick(a);
            _journal.Pick(b);
            if (_journal.Picks.Count != 2) return CombineResult.NotKnown;
            return _journal.CombinePicked();
        }

        // ---------- The end ----------

        IEnumerator AfterBattle(BattleOutcome outcome)
        {
            _mode = Mode.Transition;
            float k = Fast ? 0.05f : 1f;
            for (float t = 0f; t < 1f * k; t += Time.unscaledDeltaTime)
            {
                _battleView.SetFade(1f - t / k);
                yield return null;
            }
            _battleView.Hide();
            GameRoot.Run.Set("Walt", OutcomeName(outcome));
            yield return PlayWhenFree(SliceContent.OutcomeKnot(outcome));
            while (GameRoot.Dialogue.IsActive) yield return null;
            // The street dims; Mags pulls Nadia out.
            for (float t = 0f; t < 2.5f * k; t += Time.unscaledDeltaTime)
            {
                _hud.SetDim(t / (2.5f * k) * 0.85f);
                yield return null;
            }
            GameRoot.Audio.StopAmbience(2f);
            if (outcome != BattleOutcome.ComposureLost)
            {
                yield return PlayWhenFree(SliceContent.KnotSliceEnd);
                while (GameRoot.Dialogue.IsActive) yield return null;
            }
            _hud.SetDim(1f);
            ShowEndCard(outcome);
        }

        void ShowEndCard(BattleOutcome outcome)
        {
            _mode = Mode.End;
            string body = "Walt: " + OutcomeName(outcome) + ".";
            var b = config.walt;
            if (outcome == BattleOutcome.Convinced && _state.journal.Book.Find(b.convincedEntry) != null) body += "\nA truth: " + _state.journal.Book.Find(b.convincedEntry).title + ".";
            if (outcome == BattleOutcome.HeardOut && _state.journal.Book.Find(b.heardOutEntry) != null) body += "\nKept: " + _state.journal.Book.Find(b.heardOutEntry).title + ".";
            if (outcome == BattleOutcome.ComposureLost) body += "\nThe night ends.";
            body += "\n\nNotes for the creator: docs/SLICE-NOTES.md";
            _hud.ShowEnd("End of the slice.", body, Finish, RetryWalt);
        }

        public static string OutcomeName(BattleOutcome o)
        {
            switch (o)
            {
                case BattleOutcome.Convinced: return "Convinced";
                case BattleOutcome.HeardOut: return "Heard out";
                case BattleOutcome.Pressed: return "Pressed";
                case BattleOutcome.ComposureLost: return "Composure lost";
                default: return "Not argued";
            }
        }

        void Finish()
        {
            _mode = Mode.End;
            GameRoot.Win();
        }

        /// <summary>Back on the street in front of Walt, the journal as it was before the argument.</summary>
        public void RetryWalt()
        {
            _hud.HideEnd();
            _state.journal.Forget(config.walt.convincedEntry);
            _state.journal.Forget(config.walt.heardOutEntry);
            _state.waltOutcome = BattleOutcome.None;
            if (_area == null || _area.area != SliceArea.Street) EnterArea(SliceArea.Street);
            _hud.SetDim(0f);
            GameRoot.Audio.PlayAmbience("amb_vh_suburb");
            if (_area.walt != null)
            {
                Vector3 w = _area.walt.transform.position;
                player.Spawn(w + new Vector3(-3f, 0.05f, -2f), 56f);
            }
            _mode = Mode.Explore;
        }
    }
}
