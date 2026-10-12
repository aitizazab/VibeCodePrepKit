using System;
using System.Collections;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEngine;
using UnityEngine.InputSystem;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 2: the house (docs/redesign/house.md). Runs the night on core <see cref="DreamHouse"/> (the house),
    /// <see cref="DreamHouseStory"/> (the order of things), <see cref="HideAndSeekRound"/> (the game's rules and the cost of
    /// being found), <see cref="TheoBrain"/> (Theo seeking) and <see cref="HouseEscape"/> (the house throwing her out):
    /// up to the front door and in, the hall breathing, sitting down at the fourth chair, pancakes, hide-and-seek through
    /// the whole house, Theo's question, Lena (the argument battle), the house tipping, the corridor that keeps getting
    /// longer, out onto the hill in the rain, or thrown out of the dream. The house's frame is <see cref="houseRoot"/> (the
    /// hill places it); the escape is built at <see cref="escapeRoot"/>, upright, far below.
    /// This file: the night's flow, the lines, the prompts. HideAndSeek, Escape and Drive are its other parts.
    /// </summary>
    public sealed partial class DreamHouseGame : MonoBehaviour, IDreamRainHost
    {
        // ---------- Wired by the builder ----------
        public MovementController player;
        public Transform houseRoot, escapeRoot;
        public TheoView theo;
        /// <summary>Lena's figure (at the stove, at the table) and Lena in the tower's round window (seen from the hill).</summary>
        public Transform lena;
        public GameObject lenaInWindow;
        public HouseMemoryView[] memoryViews;
        public HouseCameraFx cameraFx;
        public DreamUmbrella umbrella;
        /// <summary>The house folding shut behind her in the escape.</summary>
        public Transform fold;
        /// <summary>The door at the end of the corridor: its collider (off when it bursts open) and its leaf (swings).</summary>
        public Collider corridorDoor;
        public Transform corridorDoorLeaf;
        /// <summary>Theo holding the corridor's door (Lena unsure): shown when he does.</summary>
        public GameObject theoAtTheDoor;
        /// <summary>The door she comes out of on the hill (its position and facing).</summary>
        public Transform exitDoor;
        /// <summary>Each escape room's dressing (folded away behind her).</summary>
        public Transform[] escapeRoomGroups;
        /// <summary>The fourth chair's collider (off while she sits in it).</summary>
        public Collider nadiaChair;
        public bool useArgumentBattle = true;

        static DreamHouseGame _current;
        static Action _escaped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _escaped = null;
            DebugSaturdayPaper = false;
        }

        /// <summary>The argument's battle API (DreamBattles plugs it in): starts battle <c>battleId</c> with the evidence
        /// she carries; <c>done</c> gets how it ended.</summary>
        public delegate void BattleStarter(string battleId, IReadOnlyList<string> evidence, Action<ArgumentOutcome> done);
        public static BattleStarter StartBattle;

        /// <summary>Testing Night 2 alone: as if Night 1's Saturday paper were in the journal (F6 in a debug build).</summary>
        public static bool DebugSaturdayPaper;
        public static bool HasSaturdayPaper => DebugSaturdayPaper || (GameRoot.Exists && DreamJournal.Has(GameRoot.Save.Data.GetEntry(DreamJournal.Key), DreamJournal.SaturdayPaper));
        public static string SavedTheoAnswer => GameRoot.Exists ? GameRoot.Save.Data.GetEntry(DreamHouseStory.TheoAnswerKey) : null;
        public static DreamHouseGame Current => _current;

        /// <summary>The night is over: she's out (onto the hill, or thrown out of the dream). The chain listens.</summary>
        public static event Action Escaped
        {
            add => _escaped += value;
            remove => _escaped -= value;
        }

        /// <summary>Lena condemned Nadia (this visit or an earlier one): the save's flag.</summary>
        public static bool LenaCondemned => GameRoot.Exists && GameRoot.Save.Data.HasFlag(DreamHouseStory.CondemnsFlag);
        /// <summary>She was thrown out of Night 2 (the save's flag).</summary>
        public static bool WasThrownOut => GameRoot.Exists && GameRoot.Save.Data.HasFlag(DreamHouseStory.ThrownOutFlag);

        // ---------- The night's state ----------
        DreamHouse _h;
        HouseEscape _e;
        readonly DreamHouseStory _story = new DreamHouseStory();
        HideAndSeekRound _round;
        TheoBrain _theo;
        StealthBoxWorld _sight;
        WaypointGraph _graph;
        HouseWalker _walker;
        HouseHud _hud;
        readonly Queue<HouseLine> _lines = new Queue<HouseLine>();
        Action _afterLines;
        float _lineLeft;
        bool _speaking;
        bool _saidDoor, _breathed, _busy, _asking, _battleUp;
        string _promptFor;
        Func<bool> _skip;
        Func<IEnumerator> _smokeDriver;

        public DreamHouse House => _h;
        public HouseEscape Escape => _e;
        public DreamHouseStory Story => _story;
        public HideAndSeekRound Round => _round;
        public TheoBrain Theo => _theo;
        public HousePhase Phase => _story.Phase;
        public HouseHud Hud => _hud;
        public bool Speaking => _speaking;
        public bool Busy => _busy;
        public bool Asking => _asking;
        public bool AutopilotOn => player != null && player.Autopilot != null;
        MovementController IDreamRainHost.Player => player;
        public bool Respawning => false;

        void Start()
        {
            if (!GameRoot.Exists || player == null || houseRoot == null || escapeRoot == null)
            {
                Debug.LogError("DreamHouseGame: missing its wiring. Rebuild with JamKit > Dream House > Rebuild Scene.");
                enabled = false;
                return;
            }
            _current = this;
            _h = DreamHouse.Build();
            _e = HouseEscape.Build();
            _sight = _h.StealthWorld();
            _graph = _h.Graph(_sight);
            _theo = new TheoBrain(_h, _sight, _graph, null, Environment.TickCount);
            _round = new HideAndSeekRound(_h);
            _walker = new HouseWalker(_h, _sight, _graph);
            BuildEscapeCourse();
            StealthLightZone.Ambient = _h.ambientLight;

            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            GameRoot.Hud.SetScoreText("");
            _hud = new HouseHud(GameRoot.Hud.Root, GameRoot.Ui.Root);
            MovementComfortScreen.Ensure();
            _sky0 = RenderSettings.ambientSkyColor; _equator0 = RenderSettings.ambientEquatorColor; _fog0 = RenderSettings.fogColor;

            DreamHousePresenter.Register(PresenterMode.Chapters, this, this);
            PresenterMode.RegisterDriver(this, DreamHousePresenter.Scene, AttractDrive);
            _smokeDriver = SmokeDrive;
            SmokeRunner.GameplayDriver = _smokeDriver;
            _skip = SkipAhead;
            DebugMenu.SkipHandler = _skip;
            GameRoot.Run.Set("Dream", "The house (Night 2)");
            GameRoot.Audio.StopMusic(0.4f);
            NewVisit();
            RecorderStart();
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (ArgumentRunning) CloseBattle();
            Time.timeScale = 1f;
            if (!GameRoot.Exists) return;
            PresenterMode.Unregister(this);
            if (SmokeRunner.GameplayDriver == _smokeDriver) SmokeRunner.GameplayDriver = null;
            if (DebugMenu.SkipHandler == _skip) DebugMenu.SkipHandler = null;
            MovementComfortScreen.Remove();
            _hud?.Remove();
        }

        // =====================================================================================================
        // The visit
        // =====================================================================================================

        /// <summary>A fresh visit: on the street, nothing found, Theo at the table, Lena at the stove.</summary>
        public void NewVisit()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            _story.Restart();
            ResetHouse();
            ApplyBedside();
            Teleport(ToWorld(_h.arrival), WorldYaw(_h.arrivalYaw), Vector3.zero);
            Say(DreamHouseLines.Street, null);
        }

        void ResetHouse()
        {
            if (ArgumentRunning) CloseBattle();
            _battleUp = false;
            _lines.Clear();
            _speaking = false;
            _afterLines = null;
            _hud.ClearLine();
            _hud.Prompt(null);
            _hud.HideChoice();
            _hud.Card(null, null);
            _hud.Fade(Color.black, 0f, 0f);
            _hud.ShowCounter(false);
            _saidDoor = _breathed = _busy = _asking = false;
            _promptFor = null;
            _round.Reset();
            foreach (var v in memoryViews)
            {
                if (v == null) continue;
                var m = _h.Memory(v.id);
                if (m == null) continue;
                v.MoveTo(ToWorld(m.position), MemoryYaw(m.position, m.standAt));
            }
            SetTheoRoom(true);
            _theo.Stop();
            _theo.Place(_h.theoStart, _h.theoCountYaw);
            PlaceTheo();
            if (lena != null) lena.position = ToWorld(_h.lenaAt);
            if (lenaInWindow != null) lenaInWindow.SetActive(false);
            if (theoAtTheDoor != null) theoAtTheDoor.SetActive(false);
            if (cameraFx != null) cameraFx.ResetAll();
            if (nadiaChair != null) nadiaChair.enabled = true;
            player.InputEnabled = true;
            ResetEscape();
        }

        void Update()
        {
            if (_h == null) return;
            float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;
            TickLines(udt);
            _hud.Tick(udt);
            if (GameRoot.Flow.Current == GameState.Playing)
            {
                switch (_story.Phase)
                {
                    case HousePhase.WayIn: TickWayIn(); break;
                    case HousePhase.HideAndSeek: TickHideAndSeek(dt); break;
                    case HousePhase.TheoAsks: ReadTheoAnswer(); break;
                    case HousePhase.Argument: ReadDebugArgument(); break;
                    case HousePhase.Escape:
                    case HousePhase.Out: TickEscape(dt); break;
                }
            }
            RecorderTick();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (DebugMenu.Allowed && kb != null && kb.f6Key.wasPressedThisFrame)
            {
                DebugSaturdayPaper = !DebugSaturdayPaper;
                GameRoot.Hud.Message("Debug: Night 1's Saturday paper " + (DebugSaturdayPaper ? "in the journal" : "not in the journal"), 2.5f);
            }
        }

        // ---------- The house's frame ----------

        public Vector3 ToWorld(NVec3 p) => houseRoot.TransformPoint(new Vector3(p.X, p.Y, p.Z));

        public NVec3 ToHouse(Vector3 world)
        {
            var p = houseRoot.InverseTransformPoint(world);
            return new NVec3(p.x, p.y, p.z);
        }

        float WorldYaw(float houseYaw) => houseYaw + houseRoot.eulerAngles.y;

        /// <summary>A memory faces where she stands to take it, square to its wall (snapped to 90 degrees: a calendar on
        /// the fridge door, a photo on the wall, a card on the table turned to her chair).</summary>
        float MemoryYaw(NVec3 p, NVec3 standAt)
        {
            float dx = standAt.X - p.X, dz = standAt.Z - p.Z;
            if (dx * dx + dz * dz < 1e-4f) return WorldYaw(0f);
            float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            return WorldYaw(Mathf.Round(yaw / 90f) * 90f);
        }

        void Teleport(Vector3 feet, float yaw, Vector3 velocity) => player.Teleport(feet, yaw, velocity);

        NVec3 Feet => ToHouse(player.transform.position);

        // =====================================================================================================
        // The way in: the street, the front door, the hall breathing, the fourth chair
        // =====================================================================================================

        void TickWayIn()
        {
            var f = Feet;
            if (!_saidDoor && f.Z > -3.2f && Mathf.Abs(f.X) < 3f && f.Z < 0f)
            {
                _saidDoor = true;
                var at = new List<HouseLine>(DreamHouseLines.AtTheDoor);
                if (Told(JamKit.VisitingHours.DreamChanges.LenaWary)) at.AddRange(DreamHouseLines.LenaWaryAtTheDoor);
                else if (Told(JamKit.VisitingHours.DreamChanges.LenaSofter)) at.AddRange(DreamHouseLines.LenaSofterAtTheDoor);
                Say(at, null);
            }
            if (!_breathed && f.Z > 0.6f && f.Z < 5f && Mathf.Abs(f.X) < 1.7f && f.Y > -0.5f && f.Y < 1f)
            {
                _breathed = true;
                if (cameraFx != null) cameraFx.Breathe();
                GameRoot.Audio.Play("glitch", 0.35f);
                var hall = new List<HouseLine>(DreamHouseLines.HallBreathes);
                if (Told(JamKit.VisitingHours.DreamChanges.PhotoWall)) hall.AddRange(DreamHouseLines.PhotoAlreadyUp);
                Say(hall, null);
            }
            // The fourth chair: sit down.
            float d = NVec3.Distance(new NVec3(f.X, 0f, f.Z), new NVec3(_h.nadiaChair.X, 0f, _h.nadiaChair.Z));
            bool near = d < 1.7f && Mathf.Abs(f.Y - _h.nadiaChair.Y) < 0.6f && !_busy;
            SetPrompt(near ? "sit" : null, "Sit down at the fourth chair");
            if (near && (InteractPressed() || AutopilotOn)) StartCoroutine(SitDown());
        }

        static readonly NVec3 LenasChair = new NVec3(4.555f, 0f, 3.95f);

        IEnumerator SitDown()
        {
            if (!_story.SitDown()) yield break;
            _busy = true;
            SetPrompt(null, null);
            player.InputEnabled = false;
            if (nadiaChair != null) nadiaChair.enabled = false;
            var seat = ToWorld(_h.nadiaChair);
            Teleport(seat, WorldYaw(_h.nadiaChairYaw), Vector3.zero);
            player.SetLook(WorldYaw(_h.nadiaChairYaw), 12f);
            if (cameraFx != null) cameraFx.Sit(true);
            if (lena != null) lena.position = ToWorld(LenasChair);
            _theo.Place(_h.theoAt, 0f);
            PlaceTheo();
            GameRoot.Audio.Play("chime", 0.4f);
            // The place card: the first memory, found by sitting down.
            Collect(DreamHouseStory.MemPlaceCard, true);
            bool done = false;
            Say(DreamHouseLines.Pancakes, () => done = true);
            while (!done) yield return null;
            // Up from the table: the game begins.
            if (cameraFx != null) cameraFx.Sit(false);
            Teleport(seat + houseRoot.TransformDirection(new Vector3(0.1f, 0f, -1.0f)), WorldYaw(180f), Vector3.zero);
            if (nadiaChair != null) nadiaChair.enabled = true;
            if (lena != null) lena.position = ToWorld(_h.lenaAt);
            player.InputEnabled = true;
            _busy = false;
            BeginHideAndSeek();
        }

        // =====================================================================================================
        // Theo's question, Elias not coming down, the argument
        // =====================================================================================================

        void AskTheo()
        {
            _busy = true;
            _hud.ShowCounter(false);
            var lead = new List<HouseLine>(DreamHouseLines.TheoAsksLead) { new HouseLine(DreamHouseLines.Theo, DreamHouseLines.TheoQuestion, 3f) };
            Say(lead, () =>
            {
                _hud.Choice("1   \"Yes. He will be.\"", "2   \"No. I promise.\"", "3   Say nothing");
                _asking = true;
            });
        }

        void ReadTheoAnswer()
        {
            if (!_asking) return;
            if (AutopilotOn) { AnswerTheo(TheoAnswer.Honest); return; }
            if (!GameRoot.Input.GameplayAllowed) return;
            int pick = PickedOption();
            if (pick > 0) AnswerTheo((TheoAnswer)pick);
        }

        static int PickedOption()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)) || (pad != null && pad.buttonWest.wasPressedThisFrame)) return 1;
            if ((kb != null && (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) return 2;
            if ((kb != null && (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)) || (pad != null && pad.buttonEast.wasPressedThisFrame)) return 3;
            return 0;
        }

        /// <summary>Her answer (keys, autopilot, tests): kept in the save; his reply; then everyone back to the table.</summary>
        public void AnswerTheo(TheoAnswer answer)
        {
            if (!_story.AnswerTheo(answer)) return;
            _asking = false;
            _hud.HideChoice();
            if (GameRoot.Exists)
            {
                GameRoot.Save.Data.SetEntry(DreamHouseStory.TheoAnswerKey, DreamHouseStory.AnswerText(answer));
                GameRoot.Save.Save();
            }
            Debug.Log("JAMKIT_HOUSE Theo asked \"" + DreamHouseLines.TheoQuestion + "\": " + DreamHouseStory.AnswerText(answer));
            Say(DreamHouseLines.TheoAnswers[(int)answer - 1], () => StartCoroutine(BackToTheTable()));
        }

        IEnumerator BackToTheTable()
        {
            _hud.Fade(Color.black, 1f, 2.5f);
            yield return new WaitForSecondsRealtime(0.5f);
            _theo.Stop();
            _theo.Place(_h.theoAt, 0f);
            PlaceTheo();
            if (lena != null) lena.position = ToWorld(LenasChair);
            Teleport(ToWorld(_h.nadiaChair) + houseRoot.TransformDirection(new Vector3(0.1f, 0f, -0.95f)), WorldYaw(-60f), Vector3.zero);
            player.SetLook(WorldYaw(-60f), 6f);
            _hud.Fade(Color.black, 0f, 2f);
            _busy = false;
            Say(DreamHouseLines.EliasNotHere, BeginArgument);
        }

        void BeginArgument()
        {
            var evidence = DreamHouseStory.EvidenceFor(_round.Found, HasSaturdayPaper);
            if (GameRoot.Exists)
            {
                var save = GameRoot.Save.Data;
                save.SetEntry(DreamHouseStory.CardsKey, string.Join(",", evidence));
                save.SetEntry(DreamHouseStory.CatchesKey, _round.Catches.ToString());
                foreach (var id in _round.Found) JamKit.VisitingHours.Chain.Record(save, id);
                GameRoot.Save.Save();
            }
            Debug.Log("JAMKIT_HOUSE the battle " + DreamHouseStory.LenaBattleId + " with " + (evidence.Count == 0 ? "no evidence" : string.Join(", ", evidence)) + " (caught " + _round.Catches + " times)");
            if (StartBattle != null && useArgumentBattle && !AutopilotOn)
            {
                if (DebugMenu.Allowed) _hud.Prompt("Debug: F7 ends the argument (Lena unsure), F8 (Lena condemns).");
                _battleUp = true;
                StartBattle(DreamHouseStory.LenaBattleId, evidence, outcome => Choose(outcome == ArgumentOutcome.None ? ArgumentOutcome.Unsure : outcome));
                return;
            }
            Choose(ArgumentOutcome.Unsure);
        }

        /// <summary>The argument battle is on screen.</summary>
        public bool ArgumentRunning => _battleUp && JamKit.Argument.ArgumentHost.Current != null;

        void CloseBattle()
        {
            _battleUp = false;
            JamKit.Argument.ArgumentHost.Current?.Close(false);
        }

        void ReadDebugArgument()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !DebugMenu.Allowed) return;
            if (kb.f7Key.wasPressedThisFrame) Choose(ArgumentOutcome.Unsure);
            else if (kb.f8Key.wasPressedThisFrame) Choose(ArgumentOutcome.Condemns);
        }

        /// <summary>Lena condemns Nadia only on the battle's Condemns stance; Amenable and Unsure leave her unsure.</summary>
        public static ArgumentOutcome OutcomeOf(JamKit.Argument.ArgumentStance stance) =>
            stance == JamKit.Argument.ArgumentStance.Condemns ? ArgumentOutcome.Condemns : ArgumentOutcome.Unsure;

        /// <summary>How the argument ended (the battle, the debug keys, the autopilot): Lena's line, Theo's parting line, and
        /// the house throws her out.</summary>
        public void Choose(ArgumentOutcome outcome)
        {
            if (!_story.Choose(outcome)) return;
            if (ArgumentRunning) CloseBattle();
            _battleUp = false;
            _hud.Prompt(null);
            if (outcome == ArgumentOutcome.Condemns && GameRoot.Exists)
            {
                var save = GameRoot.Save.Data;
                if (!save.HasFlag(DreamHouseStory.CondemnsFlag)) { save.SetFlag(DreamHouseStory.CondemnsFlag); GameRoot.Save.Save(); }
            }
            Debug.Log("JAMKIT_HOUSE the argument with Lena ended: " + outcome);
            var lines = new List<HouseLine>(outcome == ArgumentOutcome.Condemns ? DreamHouseLines.Condemns : DreamHouseLines.Unsure);
            lines.AddRange(DreamHouseLines.PartingFor(_story.TheoAnswered));
            lines.AddRange(DreamHouseLines.HouseTips);
            Say(lines, () => StartCoroutine(TipOver()));
        }

        // =====================================================================================================
        // Lines and prompts
        // =====================================================================================================

        /// <summary>Plays lines one after another (each its own reveal), then calls <paramref name="after"/>.</summary>
        public void Say(IList<HouseLine> lines, Action after)
        {
            _lines.Clear();
            foreach (var l in lines) _lines.Enqueue(l);
            _afterLines = after;
            _lineLeft = 0f;
            _speaking = true;
            NextLine();
        }

        void NextLine()
        {
            if (_lines.Count == 0)
            {
                _speaking = false;
                _hud.ClearLine();
                var after = _afterLines;
                _afterLines = null;
                after?.Invoke();
                return;
            }
            var l = _lines.Dequeue();
            _lineLeft = l.seconds;
            _hud.Line(l);
            Debug.Log("JAMKIT_HOUSE_LINE " + (string.IsNullOrEmpty(l.speaker) ? "(Nadia, thinking)" : l.speaker) + ": " + l.text);
            if (l.speaker == DreamHouseLines.Mags) GameRoot.Audio.Play("blip", 0.25f);
        }

        void TickLines(float dt)
        {
            if (!_speaking) return;
            float shown = _lineLeft;
            _lineLeft -= dt;
            // Seated or still (not running for her life), a click moves the line on.
            bool next = _story.Phase != HousePhase.Escape && GameRoot.Input.GameplayAllowed && shown < 99f && GameRoot.Input.Pressed(GameRoot.Input.Fire);
            if (_lineLeft <= 0f || next) NextLine();
        }

        /// <summary>Skips the lines on screen (tests, the recorder): the next step happens at once.</summary>
        public void SkipLines()
        {
            int guard = 32;
            while (_speaking && guard-- > 0) { _lineLeft = 0f; NextLine(); }
        }

        bool InteractPressed() => GameRoot.Input.GameplayAllowed && GameRoot.Input.Pressed(GameRoot.Input.Interact);

        static string InteractKey()
        {
            var a = GameRoot.Input.Interact;
            string k = a != null ? a.GetBindingDisplayString(0) : "E";
            return string.IsNullOrEmpty(k) ? "E" : k;
        }

        void SetPrompt(string id, string text)
        {
            if (id == _promptFor) return;
            _promptFor = id;
            _hud.Prompt(id == null ? null : "[" + InteractKey() + "]  " + text);
        }
    }
}
