using System;
using System.Collections;
using System.Collections.Generic;
using JamKit.Argument;
using JamKit.DeepBlock;
using JamKit.Flow;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using BattleOutcome = JamKit.Argument.ArgumentOutcome;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 1, played (docs/LARCH-HILL.md; docs/NIGHTS-1-2.md "Night 1: The Street"; core <see cref="LarchHill"/> and
    /// <see cref="LarchStreetStory"/>):
    /// <list type="number">
    /// <item><b>The bottom.</b> She arrives on the creek's far bank, the whole hill ahead, the house's tower and its beacon
    /// at the top. Mags: "You're lighter in here." The creek is the first double jump; Walt's half-open garage door the
    /// first slide.</item>
    /// <item><b>Walt.</b> In the shop's yard he waves (the memory: Walt alive; with Day 1's memorial card, the deduction
    /// "Walt died in 2015. This is memory."), then stands in the gate to the hill: "Let him have it. Come back tomorrow."
    /// His argument (the argument agent's battle, <see cref="ArgumentHost.BeginBattle"/>, "walt_night1", with what she
    /// carries in the dream journal); whatever the outcome he steps aside, and how she treated him is kept in the save.</item>
    /// <item><b>Before Walt</b>, both halves of his optional premise: on the far bank the neighbours' loop (the sprinkler,
    /// the dog's ball, "Lovely Saturday for it!" on a fixed timer: <see cref="NeighbourLoop"/>), heard twice, is a memory;
    /// with Saturday's paper on his yard bench it's the deduction "He's living one Saturday, over and over."</item>
    /// <item><b>The climb.</b> Four ways up; a neighbour at his grill on terrace 1 chats as she passes.</item>
    /// <item><b>The top.</b> Elias at the barbecue goes in as she arrives. At the garden gate she sees him through the
    /// window at the stove, Theo setting the table; he looks up and sees her; Lena steps between them, takes him inside,
    /// the door closes, the porch light goes out. Mags: "His heart's up. That's enough for tonight. You found the house."</item>
    /// </list>
    /// Everything learnt goes into the save's shared dream journal (<see cref="DreamJournal"/>), which Night 2 reads. Every
    /// line is a PLACEHOLDER. Without a listener on <see cref="NightEnded"/> the night starts again.
    /// </summary>
    public sealed class DreamStreetGame : MonoBehaviour
    {
        public MovementController player;
        [Tooltip("The collider that shuts the way up until Walt's argument (core LarchHill's gate piece).")]
        public GameObject waltGate;
        public Transform gateLeaf, walt, neighbour, dog, sprinklerJet, ball;
        public GameObject paper, ledger;
        public GameObject eliasOutside;
        public Transform eliasInside, theoInside, lenaInside, frontDoor;
        public Light porchLight;
        public GameObject porchGlow, beacon;
        public Transform houseRoot;
        [Tooltip("Standalone: Day 1's memorial card counts as known (the chain passes the real journal).")]
        public bool assumeDay1Facts = true;
        [Tooltip("Play Walt's argument (the argument agent's battle). Off, or under the autopilot: the debug choice.")]
        public bool useArgumentBattle = true;

        static DreamStreetGame _current;
        static Action _nightEnded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _nightEnded = null;
        }

        public static DreamStreetGame Current => _current;

        /// <summary>Fires when the door has shut and Mags has spoken (wire the chain here). Without a listener the night restarts.</summary>
        public static event Action NightEnded
        {
            add => _nightEnded += value;
            remove => _nightEnded -= value;
        }

        LarchHill _h;
        readonly LarchStreetStory _story = new LarchStreetStory();
        NeighbourLoop _loop;
        CourseAutopilot _pilot;
        BlockRoute _route;
        VisualElement _white;
        Label _subtitle, _prompt;
        readonly Queue<StreetLine> _lines = new Queue<StreetLine>();
        readonly List<string> _made = new List<string>();
        float _lineLeft, _night, _loopPrev, _whiteAlpha;
        string _lineText = "";
        Action _afterLines;
        int _heardLine, _checkpoint, _respawns;
        bool _saidDoor, _waitingForWalt, _ending, _ended, _eliasGone, _chatted;
        Vector3 _waltHome, _ballFrom, _ballTo, _dogHome;
        ArgumentHost _battle;
        Func<MoveSim, MoveInput> _hold;
        Func<bool> _skip;

        public LarchHill Hill => _h;
        public LarchStreetStory Story => _story;
        public StreetPhase Phase => _story.Phase;
        public NeighbourLoop Loop => _loop;
        public string LineText => _lineText;
        public bool Speaking => _lineText.Length > 0 || _lines.Count > 0;
        public bool WaitingForWalt => _waitingForWalt;
        public bool Ending => _ending;
        public bool Ended => _ended;
        public int HeardLine => _heardLine;
        public int Respawns => _respawns;
        public bool AutopilotOn => player != null && player.Autopilot != null && player.Autopilot != _hold;
        public float WhiteAlpha => _whiteAlpha;

        void Start()
        {
            if (!GameRoot.Exists || player == null)
            {
                Debug.LogError("DreamStreetGame: missing the player. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            _h = LarchHill.Build();
            _loop = new NeighbourLoop(1);
            _hold = sim => new MoveInput { yaw = sim.Yaw };
            if (walt != null) _waltHome = walt.position;
            _ballFrom = V(_h.Spot(LarchHill.SpotBallFrom).position);
            _ballTo = V(_h.Spot(LarchHill.SpotBallTo).position);
            _dogHome = dog != null ? dog.position : V(_h.Spot(LarchHill.SpotDog).position);
            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            GameRoot.Hud.SetScoreText("");
            BuildUi();
            MovementComfortScreen.Ensure();
            DreamStreetPresenter.Register(PresenterMode.Chapters, this, this);
            _skip = DebugSkip;
            DebugMenu.SkipHandler = _skip;
            GameRoot.Run.Set("Dream", "Larch Street (Night 1)");
            GameRoot.Audio.StopMusic(0.4f);
            NewNight();
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (!GameRoot.Exists) return;
            PresenterMode.Unregister(this);
            if (DebugMenu.SkipHandler == _skip) DebugMenu.SkipHandler = null;
            MovementComfortScreen.Remove();
            _white?.RemoveFromHierarchy();
            _subtitle?.RemoveFromHierarchy();
            _prompt?.RemoveFromHierarchy();
        }

        static Vector3 V(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        static System.Numerics.Vector3 N(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);

        // ---------- The night ----------

        /// <summary>Starts the night from the far bank: Walt in his gate, the house lit, everything to find again (what she
        /// learnt stays in the save's journal).</summary>
        public void NewNight()
        {
            StopAllCoroutines();
            _story.Reset();
            _lines.Clear();
            _lineText = "";
            _afterLines = null;
            _heardLine = 0;
            _saidDoor = _waitingForWalt = _ending = _ended = _eliasGone = _chatted = false;
            _night = 0f;
            _loopPrev = 0f;
            _route = null;
            if (_battle != null) { var b = _battle; _battle = null; b.Close(false); }
            if (player.Autopilot == _hold) player.Autopilot = null;
            SetGate(false);
            if (walt != null) walt.position = _waltHome;
            if (paper != null) paper.SetActive(true);
            if (ledger != null) ledger.SetActive(true);
            if (eliasOutside != null) eliasOutside.SetActive(true);
            SetWindow(0f);
            if (frontDoor != null) frontDoor.localRotation = Quaternion.Euler(0f, -95f, 0f);
            if (porchLight != null) porchLight.enabled = true;
            if (porchGlow != null) porchGlow.SetActive(true);
            if (beacon != null) beacon.SetActive(true);
            SetWhite(0f);
            if (_prompt != null) _prompt.style.display = DisplayStyle.None;
            if (assumeDay1Facts || Journal.Has(Entry, LarchStreetStory.FactEffects)) _story.Learn(LarchStreetStory.FactEffects);
            PlaceAt(_h.arrival, _h.arrivalYaw);
            _checkpoint = 0;
            Say(LarchStreetLines.Arrival, null);
        }

        // ---------- The save's dream journal ----------

        static string Entry => GameRoot.Exists ? GameRoot.Save.Data.GetEntry(DreamJournal.Key) : null;
        static class Journal
        {
            public static bool Has(string entry, string id) => DreamJournal.Has(entry, id);
        }

        /// <summary>Learns an entry (and any deduction it completes): into the story, the save's dream journal and the
        /// lines. True when it was new.</summary>
        public bool Learn(string id, StreetLine[] lines)
        {
            _made.Clear();
            if (!_story.Learn(id, _made)) return false;
            Keep(id);
            for (int i = 0; i < _made.Count; i++) Keep(_made[i]);
            Debug.Log("JAMKIT_STREET learnt " + id + (_made.Count > 0 ? " -> " + string.Join(", ", _made) : ""));
            GameRoot.Audio.Play("chime", 0.6f);
            if (lines != null) Say(lines, null);
            for (int i = 0; i < _made.Count; i++)
            {
                if (_made[i] == LarchStreetStory.DedWaltDead) Say(LarchStreetLines.WaltDead, null);
                else if (_made[i] == LarchStreetStory.DedSameSaturday) Say(LarchStreetLines.SameSaturday, null);
                GameRoot.Hud.Message("Deduction: " + (_made[i] == LarchStreetStory.DedWaltDead ? "Walt can't be here" : "the same Saturday"), 2.5f);
            }
            return true;
        }

        static void Keep(string id)
        {
            if (!GameRoot.Exists) return;
            var save = GameRoot.Save.Data;
            string before = save.GetEntry(DreamJournal.Key);
            string after = DreamJournal.Add(before, id);
            if (after == before) return;
            save.SetEntry(DreamJournal.Key, after);
            GameRoot.Save.Save();
        }

        // ---------- Frame ----------

        void Update()
        {
            float dt = Time.deltaTime;
            TickLines(dt);
            if (_waitingForWalt) ReadWaltChoice();
            if (_ended) return;
            _night += dt;
            TickLoop(dt);
            if (GameRoot.Flow.Current != GameState.Playing || _ending) return;
            var p = player.transform.position;
            var feet = N(p);
            if (p.y < -9f) { Respawn(); return; }
            TickPhase(feet);
            TickMemories(p);
            if (!_chatted && _h.Spot(LarchHill.SpotChatter).Contains(feet)) { _chatted = true; Say(LarchStreetLines.Chatter, null); }
        }

        void TickPhase(System.Numerics.Vector3 feet)
        {
            switch (_story.Phase)
            {
                case StreetPhase.Arrival:
                    if (feet.Z > 0.2f && feet.Y > -0.5f && feet.X < -44f)
                    {
                        _story.Advance(StreetPhase.Shop);
                        if (!_saidDoor) { _saidDoor = true; Say(LarchStreetLines.GarageDoor, null); }
                    }
                    break;
                case StreetPhase.Shop:
                    if (_h.Spot(LarchHill.SpotYard).Contains(feet))
                    {
                        _story.Advance(StreetPhase.Yard);
                        _checkpoint = 1;
                        Learn(LarchStreetStory.MemWaltAlive, LarchStreetLines.WaltAlive);
                    }
                    break;
                case StreetPhase.Yard:
                    if (System.Numerics.Vector3.Distance(feet, _h.Spot(LarchHill.SpotWalt).position) < 3.6f)
                    {
                        _story.Advance(StreetPhase.Walt);
                        Say(LarchStreetLines.WaltBlocks, BeginArgument);
                    }
                    break;
                case StreetPhase.Climb:
                    if (_h.Spot(LarchHill.SpotFoot).Contains(feet)) _checkpoint = 2;
                    if (feet.Y > LarchHill.Top - 0.6f && _h.Spot(LarchHill.SpotTopArrive).Contains(feet))
                    {
                        _story.Advance(StreetPhase.Top);
                        _checkpoint = 3;
                        if (!_eliasGone) StartCoroutine(EliasGoesIn());
                    }
                    break;
                case StreetPhase.Top:
                    if (_h.Spot(LarchHill.SpotGate).Contains(feet)) StartCoroutine(TheDoorCloses());
                    break;
            }
        }

        void TickMemories(Vector3 p)
        {
            var chest = p + Vector3.up * 0.9f;
            if (paper != null && paper.activeSelf && (chest - paper.transform.position).sqrMagnitude < 1.5f * 1.5f)
            {
                paper.SetActive(false);
                Learn(LarchStreetStory.MemSaturdayPaper, LarchStreetLines.Paper);
            }
            if (ledger != null && ledger.activeSelf && (chest - ledger.transform.position).sqrMagnitude < 1.5f * 1.5f)
            {
                ledger.SetActive(false);
                Learn(LarchStreetStory.MemWaltLedger, LarchStreetLines.Ledger);
            }
        }

        // ---------- The neighbours' loop ----------

        void TickLoop(float dt)
        {
            float t = _night;
            if (sprinklerJet != null) sprinklerJet.rotation = Quaternion.Euler(0f, 180f + _loop.SprinklerAngle(t), 0f);
            float dogOut = _loop.DogOut(t);
            if (dog != null) dog.position = Vector3.Lerp(_dogHome, _ballTo, dogOut);
            if (ball != null)
            {
                float f = _loop.BallFlight(t);
                if (f >= 0f) ball.position = Vector3.Lerp(_ballFrom, _ballTo, f) + Vector3.up * (2.2f * 4f * f * (1f - f));
                else if (_loop.BallWithDog(t) && dog != null) ball.position = dog.position + Vector3.up * 0.45f;
                else if (_loop.Phase(t) >= NeighbourLoop.LandAt && dogOut <= 0f && _loop.Phase(t) < NeighbourLoop.DogBackUntil) ball.position = _ballTo;
                else ball.position = _ballFrom;
            }
            bool said = _loop.LineBetween(_loopPrev, t);
            _loopPrev = t;
            if (!said || player == null) return;
            if (!_h.Spot(LarchHill.SpotLoopZone).Contains(N(player.transform.position))) return;
            _heardLine++;
            Say(new[] { new StreetLine(LarchStreetLines.Neighbour, LarchStreetLines.Placeholder + LarchStreetLines.LoopLine, 2.4f) }, null);
            if (_heardLine >= 2) Learn(LarchStreetStory.MemNeighboursLoop, LarchStreetLines.LoopHeard);
        }

        // ---------- Walt ----------

        /// <summary>The battle (the argument agent's), with what she carries; under the autopilot or without it, the debug
        /// choice (1 persuaded, 2 heard out, 3 pressed; F7 heard out).</summary>
        void BeginArgument()
        {
            _story.Advance(StreetPhase.Arguing);
            _waitingForWalt = true;
            var evidence = Evidence();
            Debug.Log("JAMKIT_STREET Walt's battle " + WaltNight1.Id + " with " + (evidence.Count == 0 ? "no evidence" : string.Join(", ", evidence)));
            if (useArgumentBattle && !AutopilotOn)
            {
                _battle = ArgumentHost.BeginBattle(WaltNight1.Id, DreamBattles.Context(evidence), OnBattleDone);
                if (_battle != null)
                {
                    _prompt.text = "Debug: F7 skips Walt's argument (heard out).";
                    _prompt.style.display = DisplayStyle.Flex;
                    return;
                }
            }
            _prompt.text = LarchStreetLines.ArgumentStub;
            _prompt.style.display = DisplayStyle.Flex;
        }

        /// <summary>The journal ids she carries (the story's and the save's dream journal).</summary>
        public List<string> Evidence()
        {
            var ids = new List<string>();
            foreach (var id in new[] { LarchStreetStory.DedWaltDead, LarchStreetStory.DedSameSaturday, LarchStreetStory.MemWaltAlive, LarchStreetStory.FactEffects,
                LarchStreetStory.MemSaturdayPaper, LarchStreetStory.MemNeighboursLoop, LarchStreetStory.MemWaltLedger })
                if (_story.Knows(id) || Journal.Has(Entry, id)) ids.Add(id);
            return ids;
        }

        void OnBattleDone(ArgumentResult result)
        {
            _battle = null;
            if (result != null)
            {
                if (!string.IsNullOrEmpty(result.truth)) Keep(result.truth);
                if (!string.IsNullOrEmpty(result.keepsake)) Keep(result.keepsake);
            }
            ChooseWalt(OutcomeOf(result));
        }

        /// <summary>Walt's three ways out (he steps aside in all of them).</summary>
        public static WaltOutcome OutcomeOf(ArgumentResult result)
        {
            if (result == null) return WaltOutcome.HeardOut;
            switch (result.outcome)
            {
                case BattleOutcome.Convinced: return WaltOutcome.Persuaded;
                case BattleOutcome.Pressed:
                case BattleOutcome.ComposureLost: return WaltOutcome.Pressed;
                default: return WaltOutcome.HeardOut;
            }
        }

        void ReadWaltChoice()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f7Key.wasPressedThisFrame) { ChooseWalt(WaltOutcome.HeardOut); return; }
            if (_battle != null && ArgumentHost.Current == _battle) return;
            if (AutopilotOn) { ChooseWalt(WaltOutcome.HeardOut); return; }
            if (!GameRoot.Input.GameplayAllowed || kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) ChooseWalt(WaltOutcome.Persuaded);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) ChooseWalt(WaltOutcome.HeardOut);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) ChooseWalt(WaltOutcome.Pressed);
        }

        /// <summary>How Walt's argument ended (the battle, the debug choice; tests and chapters call it): he steps aside, the
        /// gate opens, the outcome is kept in the save, the climb begins.</summary>
        public void ChooseWalt(WaltOutcome outcome)
        {
            if (outcome == WaltOutcome.None) outcome = WaltOutcome.HeardOut;
            if (_story.Phase < StreetPhase.Arguing) { _story.Advance(StreetPhase.Shop); _story.Advance(StreetPhase.Yard); _story.Advance(StreetPhase.Walt); _story.Advance(StreetPhase.Arguing); }
            if (!_story.EndArgument(outcome)) return;
            _waitingForWalt = false;
            if (_battle != null) { var b = _battle; _battle = null; b.Close(false); }
            if (_prompt != null) _prompt.style.display = DisplayStyle.None;
            if (GameRoot.Exists)
            {
                GameRoot.Save.Data.SetEntry(LarchStreetStory.WaltOutcomeKey, LarchStreetStory.WaltOutcomeValue(outcome));
                GameRoot.Save.Save();
            }
            Debug.Log("JAMKIT_STREET Walt steps aside: " + outcome);
            SetGate(true);
            if (walt != null) walt.position = V(_h.Spot(LarchHill.SpotWaltAside).position);
            Say(LarchStreetLines.WaltAside(outcome), null);
            Say(LarchStreetLines.FourWaysUp, null);
        }

        void SetGate(bool open)
        {
            if (waltGate != null) waltGate.SetActive(!open);
            if (gateLeaf != null) gateLeaf.localRotation = Quaternion.Euler(0f, open ? -100f : 0f, 0f);
        }

        // ---------- The top ----------

        IEnumerator EliasGoesIn()
        {
            _eliasGone = true;
            Say(LarchStreetLines.EliasGlimpsed, null);
            if (eliasOutside == null) yield break;
            var from = eliasOutside.transform.position;
            var to = from + new Vector3(-4f, 0f, 6f);
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                eliasOutside.transform.position = Vector3.Lerp(from, to, t / 1.4f);
                yield return null;
            }
            eliasOutside.SetActive(false);
            eliasOutside.transform.position = from;
            SetWindow(1f);
        }

        /// <summary>The people in the window: 0 nobody yet, 1 Theo at the table and Elias at the stove.</summary>
        void SetWindow(float on)
        {
            bool show = on > 0.5f;
            if (theoInside != null) theoInside.gameObject.SetActive(show);
            if (eliasInside != null) eliasInside.gameObject.SetActive(show);
            if (lenaInside != null) lenaInside.gameObject.SetActive(false);
        }

        /// <summary>The ending (no chase): at the garden gate she's held; Elias looks up and sees her; Lena steps between them,
        /// takes him inside; the door closes; the porch light goes out; Mags; the fade. Then <see cref="NightEnded"/>, or the
        /// night again.</summary>
        IEnumerator TheDoorCloses()
        {
            _ending = true;
            _story.Advance(StreetPhase.Ending);
            player.Autopilot = _hold;
            SetWindow(1f);
            if (eliasOutside != null) eliasOutside.SetActive(false);
            Debug.Log("JAMKIT_STREET the garden gate: the door closes");
            yield return Wait(1.2f);
            // He looks up: a little lift, toward the window.
            if (eliasInside != null)
            {
                var e0 = eliasInside.localPosition;
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) { eliasInside.localPosition = e0 + new Vector3(0.4f * t / 0.5f, 0.05f * Mathf.Sin(t * 12f), -0.6f * t / 0.5f); yield return null; }
            }
            yield return Wait(0.9f);
            // Lena steps between them.
            if (lenaInside != null && eliasInside != null)
            {
                lenaInside.gameObject.SetActive(true);
                var l0 = lenaInside.localPosition;
                var l1 = eliasInside.localPosition + new Vector3(0.3f, 0f, -0.5f);
                for (float t = 0f; t < 0.8f; t += Time.deltaTime) { lenaInside.localPosition = Vector3.Lerp(l0, l1, t / 0.8f); yield return null; }
                yield return Wait(0.7f);
                // ...and takes him inside, out of the window.
                var e1 = eliasInside.localPosition;
                var l2 = lenaInside.localPosition;
                for (float t = 0f; t < 1f; t += Time.deltaTime)
                {
                    float k = t / 1f;
                    eliasInside.localPosition = e1 + new Vector3(-3f * k, 0f, 1.2f * k);
                    lenaInside.localPosition = l2 + new Vector3(-3f * k, 0f, 1.2f * k);
                    yield return null;
                }
                eliasInside.gameObject.SetActive(false);
                lenaInside.gameObject.SetActive(false);
                lenaInside.localPosition = l0;
            }
            yield return Wait(0.5f);
            // The door closes.
            if (frontDoor != null)
            {
                for (float t = 0f; t < 0.6f; t += Time.deltaTime) { frontDoor.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-95f, 0f, t / 0.6f), 0f); yield return null; }
                frontDoor.localRotation = Quaternion.identity;
            }
            GameRoot.Audio.Play("ui_back", 0.8f);
            yield return Wait(0.8f);
            // The porch light goes out.
            if (porchLight != null) porchLight.enabled = false;
            if (porchGlow != null) porchGlow.SetActive(false);
            GameRoot.Audio.Play("click", 0.6f);
            yield return Wait(0.6f);
            bool spoken = false;
            Say(LarchStreetLines.Ending, () => spoken = true);
            while (!spoken) yield return null;
            Keep(LarchStreetStory.MemTheHouse);
            if (GameRoot.Exists) { GameRoot.Save.Data.SetFlag(LarchStreetStory.NightDoneFlag); GameRoot.Save.Save(); }
            _story.Advance(StreetPhase.Ended);
            for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime) { SetWhite(t / 1.4f); yield return null; }
            SetWhite(1f);
            _ended = true;
            Debug.Log("JAMKIT_STREET Night 1 ended");
            if (_nightEnded != null) { _nightEnded.Invoke(); yield break; }
            GameRoot.Hud.Message("End of Night 1 (placeholder: the chain goes on to Day 2).", 3f);
            yield return Wait(2.5f);
            NewNight();
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        /// <summary>The ending's beats skip ahead (tests): the lines end at once.</summary>
        public void SkipLines()
        {
            int guard = 32;
            while ((_lineText.Length > 0 || _lines.Count > 0) && guard-- > 0) { _lineLeft = 0f; NextLine(); }
        }

        // ---------- Placing, falling ----------

        void PlaceAt(System.Numerics.Vector3 feet, float yaw)
        {
            player.Teleport(V(feet) + Vector3.up * 0.02f, yaw, Vector3.zero);
        }

        /// <summary>A fall off the hill: back to the last place reached (the arrival, the yard, the foot square, the top).</summary>
        void Respawn()
        {
            _respawns++;
            switch (_checkpoint)
            {
                case 1: PlaceAt(_h.Spot(LarchHill.SpotYard).position - new System.Numerics.Vector3(0f, 1f, 0f), 0f); break;
                case 2: PlaceAt(_h.routes[0].start, 90f); break;
                case 3: PlaceAt(new System.Numerics.Vector3(-12f, LarchHill.Top, 108f), 0f); break;
                default: PlaceAt(_h.arrival, _h.arrivalYaw); break;
            }
            GameRoot.Hud.Message("The dream catches you.", 1.5f);
        }

        // ---------- Chapters, the autopilot ----------

        /// <summary>Presenter chapters: the creek (the night from the start), Walt (in the yard), each route's start (Walt
        /// already aside), the top (by the lookout).</summary>
        public void JumpToChapter(string chapter)
        {
            NewNight();
            SkipLines();
            if (chapter == LarchHill.ChapterArrival) return;
            _story.Advance(StreetPhase.Shop);
            _saidDoor = true;
            if (chapter == LarchHill.ChapterWalt)
            {
                PlaceAt(new System.Numerics.Vector3(-48.75f, 0f, 17f), -20f);
                return;
            }
            _story.Learn(LarchStreetStory.MemWaltAlive);
            ChooseWalt(WaltOutcome.HeardOut);
            SkipLines();
            if (chapter == LarchHill.ChapterTop)
            {
                _checkpoint = 3;
                PlaceAt(new System.Numerics.Vector3(-12f, LarchHill.Top, 108f), 0f);
                return;
            }
            var r = _h.routes.Find(x => x.chapter == chapter);
            if (r == null) return;
            _checkpoint = 2;
            PlaceAt(r.start, r.startYaw);
        }

        /// <summary>The course autopilot runs <paramref name="route"/> (the way in, or one of the four); null hands control back.</summary>
        public void Drive(BlockRoute route)
        {
            _route = route;
            if (route == null) { if (player.Autopilot != _hold) player.Autopilot = null; return; }
            _pilot = new CourseAutopilot(route.course);
            _pilot.Reset(route.course.route.Project(N(player.transform.position)));
            player.Autopilot = sim => _pilot.Next(sim);
        }

        bool DebugSkip()
        {
            if (_waitingForWalt) { ChooseWalt(WaltOutcome.HeardOut); return true; }
            if (_story.Phase < StreetPhase.Walt) { JumpToChapter(LarchHill.ChapterWalt); return true; }
            if (_story.Phase < StreetPhase.Top) { JumpToChapter(LarchHill.ChapterTop); return true; }
            return false;
        }

        // ---------- Lines and the screen ----------

        void Say(StreetLine[] lines, Action after)
        {
            if (lines != null) foreach (var l in lines) _lines.Enqueue(l);
            if (after != null)
            {
                if (_afterLines == null) _afterLines = after;
                else { var prev = _afterLines; _afterLines = () => { prev(); after(); }; }
            }
            if (_lineText.Length == 0) NextLine();
        }

        void NextLine()
        {
            if (_lines.Count == 0)
            {
                _lineText = "";
                if (_subtitle != null) _subtitle.style.display = DisplayStyle.None;
                var after = _afterLines;
                _afterLines = null;
                after?.Invoke();
                return;
            }
            var l = _lines.Dequeue();
            _lineText = l.speaker + ": " + l.text;
            _lineLeft = l.seconds;
            if (_subtitle != null)
            {
                _subtitle.text = _lineText;
                _subtitle.style.display = DisplayStyle.Flex;
            }
            GameRoot.Audio.Play("blip", 0.35f);
        }

        void TickLines(float dt)
        {
            if (_lineText.Length == 0) return;
            _lineLeft -= dt;
            if (_lineLeft <= 0f) NextLine();
        }

        void BuildUi()
        {
            _white = new VisualElement { name = "street-whiteout", pickingMode = PickingMode.Ignore };
            _white.style.position = Position.Absolute;
            _white.style.left = 0; _white.style.right = 0; _white.style.top = 0; _white.style.bottom = 0;
            _white.style.backgroundColor = new Color(1f, 0.97f, 0.92f, 1f);
            _white.style.opacity = 0f;
            _white.style.display = DisplayStyle.None;
            GameRoot.Ui.Root.Add(_white);
            _subtitle = Line("street-subtitle", 60, 22);
            _prompt = Line("street-argument-prompt", 140, 20);
            _prompt.style.backgroundColor = new Color(0.12f, 0.1f, 0.18f, 0.8f);
            _prompt.style.color = new Color(1f, 0.9f, 0.7f);
        }

        Label Line(string name, int bottom, int size)
        {
            var l = new Label { name = name, pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.left = Length.Percent(10); l.style.right = Length.Percent(10); l.style.bottom = bottom;
            l.style.fontSize = size;
            l.style.color = new Color(0.98f, 0.97f, 1f);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.display = DisplayStyle.None;
            GameRoot.Hud.Root.Add(l);
            return l;
        }

        void SetWhite(float a)
        {
            _whiteAlpha = a;
            if (_white == null) return;
            _white.style.opacity = a;
            _white.style.display = a > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
