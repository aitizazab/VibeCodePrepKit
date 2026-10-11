using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Argument;
using JamKit.Flow;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 2's house, played (docs/DREAM-HOUSE.md; core <see cref="DreamHouse"/> and <see cref="DreamHouseStory"/>):
    /// <list type="number">
    /// <item><b>The way in.</b> The house plays with her: the hallway stretches away as she enters, its floor gone (run
    /// the gold-striped wall), and a door half open onto a slide takes her down into the kitchen.</item>
    /// <item><b>Pancakes, then hide-and-seek.</b> Lena and Theo at the table set for four (placeholder lines). Theo
    /// counts and seeks (the stealth kit's <see cref="SeekerAgent"/> in hide-and-seek mode, his cone and meter shown);
    /// she hides (the pantry, the curtains, the coats, his wardrobe, low cover, shade) and finds the five memories,
    /// counted on screen (<see cref="StealthHud"/>). Found before the last memory: he counts again. The last memory:
    /// Nadia's line, hooked to <see cref="CollectibleCounter.AllFound"/>, its ending by route. The next time he finds
    /// her, the game is over and Elias hasn't come.</item>
    /// <item><b>The argument.</b> She questions Lena: the argument battle prototype (<see cref="ArgumentHost"/>,
    /// <see cref="DreamLenaDemo"/>); its stance decides (Condemns: condemns; Amenable or Unsure: unsure). A debug skip
    /// ends it at once (F7 unsure, F8 condemns; the autopilot skips it as unsure). Condemns is kept in the save (<see cref="LenaCondemned"/>) and makes the Despair route easier.</item>
    /// <item><b>The house tips.</b> It kicks her out: she runs the house on its side (the floor a wall, the doorframes,
    /// the staircase a slide) with the house folding shut behind her (a rubber-banded <see cref="Pursuer"/>), out of
    /// the front door into the rain, where <see cref="DreamUmbrella"/> and its Despair choice wait.</item>
    /// </list>
    /// Every line is a PLACEHOLDER (the dialogue is the creator's). Without a listener on <see cref="Escaped"/> the
    /// visit loops back to the front garden.
    /// </summary>
    public sealed class DreamHouseGame : MonoBehaviour, IDreamRainHost
    {
        public MovementController player;
        public SeekerAgent theo;
        public CollectibleCounter memories;
        /// <summary>The hallway's far end (it moves away as she enters: the house stretching the hallway).</summary>
        public Transform hallwayEnd;
        /// <summary>The house folding shut behind her in the escape (placed at the pursuer).</summary>
        public Transform fold;
        public DreamUmbrella umbrella;
        /// <summary>Play the argument battle with Lena (the prototype, <see cref="ArgumentHost"/>). Off: the debug choice only.</summary>
        public bool useArgumentBattle = true;

        static DreamHouseGame _current;
        static Action _escaped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _escaped = null;
        }

        public static DreamHouseGame Current => _current;

        /// <summary>Fires when Nadia reaches the end of the rain path (wire the story here). Without a listener the visit loops.</summary>
        public static event Action Escaped
        {
            add => _escaped += value;
            remove => _escaped -= value;
        }

        /// <summary>Lena condemned Nadia in an argument (kept in the save across visits; it makes the Despair route easier).</summary>
        public static bool LenaCondemned => GameRoot.Exists && GameRoot.Save.Data.HasFlag(DreamHouseStory.CondemnsFlag);

        DreamHouse _h;
        LabCourse _course;
        readonly DreamHouseStory _story = new DreamHouseStory();
        Pursuer _pursuer;
        CourseAutopilot _pilot;
        StealthHud _hud;
        VisualElement _white;
        Label _subtitle, _prompt;
        float _s, _whiteAlpha;
        int _checkpoint, _respawns, _caught, _falls, _finishes;
        bool _chasing, _respawning, _leaving, _stretched, _waitingForChoice;
        float _stretchT = -1f;
        Vector3 _hallwayHome;
        readonly Queue<HouseLine> _lines = new Queue<HouseLine>();
        float _lineLeft;
        string _lineText = "";
        Action _afterLines;
        DreamRainSpot _rain;
        Func<IEnumerator> _smokeDriver;
        Func<bool> _skip;
        const float FoundPause = 2.6f;

        public DreamHouse House => _h;
        public DreamHouseStory Story => _story;
        public HousePhase Phase => _story.Phase;
        public float S => _s;
        public int Checkpoint => _checkpoint;
        public bool Chasing => _chasing;
        public int Respawns => _respawns;
        public int Caught => _caught;
        public int Finishes => _finishes;
        public bool Respawning => _respawning;
        public bool Leaving => _leaving;
        public bool WaitingForChoice => _waitingForChoice;
        /// <summary>The line on screen ("" when none).</summary>
        public string LineText => _lineText;
        public bool Speaking => _lineText.Length > 0 || _lines.Count > 0;
        public bool AutopilotOn => player != null && player.Autopilot != null;
        public bool HallwayStretched => _stretched;
        public StealthHud Hud => _hud;
        MovementController IDreamRainHost.Player => player;
        public DreamRainSpot RainSpot => _rain;

        void Start()
        {
            Shader.SetGlobalColor("_VHWakeSky", Color.black);
            Shader.SetGlobalColor("_VHWakeGround", Color.black);
            Shader.SetGlobalFloat("_VHWakeSH", 0f);
            if (!GameRoot.Exists || player == null)
            {
                Debug.LogError("DreamHouseGame: missing the player. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            _h = DreamHouse.Build();
            _course = _h.course;
            _rain = DreamHouseStory.RainFor(_h.rain, LenaCondemned);
            var pp = _course.pursuer;
            _pursuer = new Pursuer { targetGap = pp.targetGap, k = pp.k, minSpeed = pp.minSpeed, maxSpeed = pp.maxSpeed };
            _pilot = new CourseAutopilot(_course);
            StealthLightZone.Ambient = _h.ambientLight;
            if (memories == null) memories = CollectibleCounter.Current;
            memories.AllFound += OnAllMemoriesFound;
            if (theo != null) theo.Found += OnTheoFound;
            if (hallwayEnd != null) _hallwayHome = hallwayEnd.position;

            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            GameRoot.Hud.SetScoreText("");
            _hud = new StealthHud(GameRoot.Hud.Root);
            BuildUi();
            MovementComfortScreen.Ensure();

            DreamHousePresenter.Register(PresenterMode.Chapters, this, this);
            PresenterMode.RegisterDriver(this, DreamHousePresenter.Scene, AttractDrive);
            _smokeDriver = SmokeDrive;
            SmokeRunner.GameplayDriver = _smokeDriver;
            _skip = SkipToNextCheckpoint;
            DebugMenu.SkipHandler = _skip;
            GameRoot.Run.Set("Dream", "The house (Night 2)");
            GameRoot.Audio.StopMusic(0.4f);
            NewVisit();
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (memories != null) memories.AllFound -= OnAllMemoriesFound;
            if (ArgumentRunning) { var b = _battle; _battle = null; b.Close(false); }
            if (theo != null) theo.Found -= OnTheoFound;
            if (!GameRoot.Exists) return;
            PresenterMode.Unregister(this);
            if (SmokeRunner.GameplayDriver == _smokeDriver) SmokeRunner.GameplayDriver = null;
            if (DebugMenu.SkipHandler == _skip) DebugMenu.SkipHandler = null;
            MovementComfortScreen.Remove();
            _hud?.Remove();
            _white?.RemoveFromHierarchy();
            _subtitle?.RemoveFromHierarchy();
            _prompt?.RemoveFromHierarchy();
        }

        // ---------- The visit ----------

        /// <summary>A fresh visit: the front garden, the hallway unstretched, Theo at the table, every memory back.</summary>
        public void NewVisit()
        {
            StopAllCoroutines();
            _story.Restart();
            ResetHouse();
            PlaceAt(_h.arrivalCheckpoint, false);
            Say(DreamHouseLines.Arrival, null);
        }

        void ResetHouse()
        {
            if (ArgumentRunning) { var b = _battle; _battle = null; b.Close(false); }
            _lines.Clear();
            _lineText = "";
            _lineLeft = 0f;
            _afterLines = null;
            _waitingForChoice = false;
            _leaving = false;
            _stretched = false;
            _stretchT = -1f;
            if (hallwayEnd != null) hallwayEnd.position = _hallwayHome;
            if (memories != null) memories.ResetAll();
            SetTheoPlaying(false);
            if (theo != null) theo.Restart();
            if (theo != null) theo.Frozen = true;
            SetWhite(0f);
            _rain = DreamHouseStory.RainFor(_h.rain, LenaCondemned);
            if (umbrella != null) umbrella.UseSpot(_rain, DreamHouseStory.RainDefFor(LenaCondemned));
            _prompt.style.display = DisplayStyle.None;
        }

        void SetTheoPlaying(bool on)
        {
            var sp = StealthPlayer.Current;
            if (sp != null) sp.findable = on;
            if (theo != null) theo.Frozen = !on;
            _hud?.Show(on);
        }

        void Update()
        {
            if (_course == null) return;
            float dt = Time.deltaTime;
            var p = player.transform.position;
            _s = _course.route.Project(new System.Numerics.Vector3(p.x, p.y, p.z));
            TickLines(dt);
            AnimateStretch(dt);
            if (_story.Phase == HousePhase.HideAndSeek) _hud.Tick();
            if (GameRoot.Flow.Current == GameState.Playing && !_respawning) Tick(dt, p);
            if (_waitingForChoice) ReadChoice();
        }

        void Tick(float dt, Vector3 p)
        {
            for (int i = _checkpoint + 1; i < _course.checkpoints.Count; i++)
            {
                if (_s < _course.checkpoints[i].s) break;
                if (_course.checkpoints[i].s > _course.checkpoints[_checkpoint].s + 60f && _story.Phase < HousePhase.Escape) break; // not past the ground floor
                _checkpoint = i;
            }
            switch (_story.Phase)
            {
                case HousePhase.WayIn:
                    if (!_stretched && _s >= _h.stretchTrigger) Stretch();
                    if (DreamHouse.InHub(ToN(p)) && _s >= _h.kitchenS && _story.ReachKitchen())
                    {
                        _checkpoint = _h.hubCheckpoint;
                        Say(DreamHouseLines.Pancakes, BeginHideAndSeek);
                    }
                    break;
                case HousePhase.Escape:
                    if (_chasing)
                    {
                        var dir = _course.route.DirectionAt(_s);
                        float along = Vector3.Dot(player.Velocity, new Vector3(dir.X, dir.Y, dir.Z));
                        bool caught = _pursuer.Step(_s, along, dt) && !DebugMenu.PlayerInvincible;
                        PlaceFold();
                        if (_s >= _h.chaseEndS) StopChase();
                        if (caught)
                        {
                            _caught++;
                            GameRoot.Audio.Play("mv_caught", 0.8f);
                            StartCoroutine(Respawn("The house folded shut on her."));
                            return;
                        }
                    }
                    if (!_leaving && _s >= _rain.rainEndS && player.Sim.Grounded) StartCoroutine(Leave());
                    break;
            }
            if (p.y < _course.killY)
            {
                _falls++;
                StartCoroutine(Respawn(_story.Phase == HousePhase.WayIn ? "The house puts her back at the door." : "The house let go of her."));
            }
        }

        static System.Numerics.Vector3 ToN(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);

        // ---------- The way in: the hallway stretches ----------

        void Stretch()
        {
            _stretched = true;
            _stretchT = 0f;
            GameRoot.Audio.Play("glitch", 0.45f);
        }

        void AnimateStretch(float dt)
        {
            if (hallwayEnd == null || _stretchT < 0f || _stretchT >= 1f) return;
            _stretchT = Mathf.Min(1f, _stretchT + dt / 1.6f);
            float k = Mathf.SmoothStep(0f, 1f, _stretchT);
            hallwayEnd.position = _hallwayHome + Vector3.forward * ((_h.stretchTo - _h.stretchFrom) * k);
        }

        // ---------- Hide-and-seek ----------

        void BeginHideAndSeek()
        {
            if (!_story.StartHideAndSeek()) return;
            SetTheoPlaying(true);
            theo.Restart();
            GameRoot.Hud.Message("Hide-and-seek: Theo counts to ten. Hide from him, and find the memories while he looks.", 4f);
        }

        void OnTheoFound(SeekerAgent s)
        {
            if (_story.Phase != HousePhase.HideAndSeek) return;
            bool over = _story.TheoFoundHer();
            SetTheoPlaying(false);
            if (over) Say(DreamHouseLines.EliasNotHere, BeginArgument);
            else Say(DreamHouseLines.FoundYou, () =>
            {
                // Another round: he goes back to the table and counts again; the memories she found stay found.
                SetTheoPlaying(true);
                theo.Restart();
            });
        }

        void OnAllMemoriesFound()
        {
            if (_story.Phase != HousePhase.HideAndSeek) return;
            string line = _story.FoundEveryMemory(DreamEscapeGame.Despair);
            Say(new[] { new HouseLine(DreamHouseLines.Nadia, line, 6f) }, null);
            Debug.Log("JAMKIT_HOUSE every memory found: \"" + line + "\"");
        }

        // ---------- The argument (a placeholder) ----------

        /// <summary>The argument with Lena: the battle prototype runs over the house (the world waits while it's up); its
        /// stance decides the outcome. Without the battle (or under the autopilot) the debug choice decides.</summary>
        void BeginArgument()
        {
            _waitingForChoice = true;
            bool battle = useArgumentBattle && !AutopilotOn;
            _prompt.text = battle ? DebugSkipHint : DreamHouseLines.ArgumentPrompt;
            _prompt.style.display = DisplayStyle.Flex;
            if (battle) _battle = ArgumentHost.Begin(DreamLenaDemo.Build(), new ArgumentSetup { allEvidence = true }, OnArgumentClosed);
        }

        const string DebugSkipHint = "Debug: F7 skips the argument (Lena unsure), F8 skips it (Lena condemns).";
        ArgumentHost _battle;

        /// <summary>The argument battle (or its prototype) is on screen.</summary>
        public bool ArgumentRunning => _battle != null && ArgumentHost.Current == _battle;

        /// <summary>The battle's end card was closed: its stance decides.</summary>
        void OnArgumentClosed(ArgumentRecord record)
        {
            _battle = null;
            // Closed before its end card had a record (the battle's Continue the very frame it ended): the house still moves
            // on, with Lena unsure (it kicks her out either way).
            if (record == null)
            {
                Debug.LogWarning("JAMKIT_HOUSE the argument closed without a record: Lena unsure");
                Choose(ArgumentOutcome.Unsure);
                return;
            }
            Debug.Log("JAMKIT_HOUSE the argument with Lena: " + record.outcome + ", " + record.stance);
            Choose(OutcomeOf(record.stance));
        }

        /// <summary>Lena condemns Nadia only on the battle's Condemns stance; Amenable and Unsure leave her unsure.</summary>
        public static ArgumentOutcome OutcomeOf(ArgumentStance stance) => stance == ArgumentStance.Condemns ? ArgumentOutcome.Condemns : ArgumentOutcome.Unsure;

        void ReadChoice()
        {
            var kb = Keyboard.current;
            // The debug skip works over the battle too (it blocks gameplay input while it's up).
            if (kb != null && kb.f7Key.wasPressedThisFrame) { Choose(ArgumentOutcome.Unsure); return; }
            if (kb != null && kb.f8Key.wasPressedThisFrame) { Choose(ArgumentOutcome.Condemns); return; }
            if (ArgumentRunning) return;
            if (!GameRoot.Input.GameplayAllowed) return;
            var pad = Gamepad.current;
            if ((kb != null && (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)) || (pad != null && pad.buttonWest.wasPressedThisFrame)) Choose(ArgumentOutcome.Unsure);
            else if ((kb != null && (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) Choose(ArgumentOutcome.Condemns);
            else if (AutopilotOn) Choose(ArgumentOutcome.Unsure);
        }

        /// <summary>How the argument ends (the battle's stance, the debug skip; tests and attract call it): Lena's line, then
        /// the house kicks her out. A battle still up is closed.</summary>
        public void Choose(ArgumentOutcome outcome)
        {
            if (!_story.Choose(outcome)) return;
            _waitingForChoice = false;
            if (ArgumentRunning) { var b = _battle; _battle = null; b.Close(false); }
            _prompt.style.display = DisplayStyle.None;
            if (outcome == ArgumentOutcome.Condemns)
            {
                var save = GameRoot.Save.Data;
                if (!save.HasFlag(DreamHouseStory.CondemnsFlag)) { save.SetFlag(DreamHouseStory.CondemnsFlag); GameRoot.Save.Save(); }
                Debug.Log("JAMKIT_HOUSE Lena condemns Nadia (the save's " + DreamHouseStory.CondemnsFlag + " is set): the Despair route comes easier");
            }
            Say(outcome == ArgumentOutcome.Condemns ? DreamHouseLines.Condemns : DreamHouseLines.Unsure, () => StartCoroutine(KickOut(true)));
        }

        // ---------- The house tips ----------

        IEnumerator KickOut(bool withLine)
        {
            if (withLine) Say(DreamHouseLines.KickedOut, null);
            GameRoot.Juice.Shake(0.35f);
            GameRoot.Audio.Play("mv_collapse", 0.9f);
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) { SetWhite(t / 0.35f); yield return null; }
            SetWhite(1f);
            BeginEscape(_h.escapeCheckpoint);
            yield return null;
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { SetWhite(1f - t / 0.4f); yield return null; }
            SetWhite(0f);
            GameRoot.Hud.Message("The house is turning over. Run for the front door.", 2.5f);
        }

        void BeginEscape(int checkpoint)
        {
            SetTheoPlaying(false);
            bool condemned = _story.LenaCondemned || LenaCondemned;
            _rain = DreamHouseStory.RainFor(_h.rain, condemned);
            if (umbrella != null) umbrella.UseSpot(_rain, DreamHouseStory.RainDefFor(condemned));
            PlaceAt(checkpoint, true);
        }

        void PlaceFold()
        {
            if (fold == null) return;
            var at = _course.route.PointAt(_pursuer.S, 0f);
            float floor = player.transform.position.y;
            fold.position = new Vector3(0f, floor - 3f, at.Z);
            fold.rotation = Quaternion.Euler(-12f * _pursuer.Threat(_s), 0f, 0f);
        }

        void StopChase()
        {
            _chasing = false;
            if (fold != null) fold.gameObject.SetActive(false);
        }

        IEnumerator Leave()
        {
            _leaving = true;
            _finishes++;
            _story.Escaped();
            GameRoot.Audio.Play("chime", 0.7f);
            Debug.Log("JAMKIT_HOUSE out of the house" + (umbrella != null && umbrella.HasUmbrella ? " under the umbrella" : " in the rain") + (DreamEscapeGame.Despair ? " (Despair set)" : ""));
            for (float t = 0f; t < 1.2f && _leaving && GameRoot.Flow.InRun; t += Time.unscaledDeltaTime) { SetWhite(0.8f * t / 1.2f); yield return null; }
            if (!_leaving || !GameRoot.Flow.InRun) { SetWhite(0f); yield break; }
            if (_escaped != null) { _escaped.Invoke(); yield break; }
            float until = Time.unscaledTime + 1.5f;
            while (Time.unscaledTime < until && _leaving && GameRoot.Flow.InRun) yield return null;
            if (!_leaving || !GameRoot.Flow.InRun) { SetWhite(0f); yield break; }
            NewVisit();
        }

        // ---------- Placing, falling ----------

        /// <summary>Puts her at a checkpoint (running, in the escape), sets the chase to match.</summary>
        void PlaceAt(int index, bool running)
        {
            index = Mathf.Clamp(index, 0, _course.checkpoints.Count - 1);
            var cp = _course.checkpoints[index];
            _checkpoint = index;
            _leaving = false;
            var pos = new Vector3(cp.position.X, cp.position.Y, cp.position.Z);
            var fwd = Quaternion.Euler(0f, cp.yaw, 0f) * Vector3.forward;
            player.Teleport(pos, cp.yaw, running ? fwd * player.Def.sprintSpeed : Vector3.zero);
            _s = cp.s;
            bool chase = _story.Phase == HousePhase.Escape && cp.s >= _h.chaseStartS && cp.s < _h.chaseEndS - 6f;
            _chasing = chase;
            if (chase) _pursuer.Reset(cp.s, player.Def.sprintSpeed);
            if (fold != null) fold.gameObject.SetActive(chase);
            if (chase) PlaceFold();
            _pilot.Reset(cp.s);
        }

        IEnumerator Respawn(string message)
        {
            _respawning = true;
            _respawns++;
            for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime) { SetWhite(t / 0.15f); yield return null; }
            SetWhite(1f);
            PlaceAt(_checkpoint, _story.Phase == HousePhase.Escape);
            GameRoot.Hud.Message(message, 1.6f);
            yield return null;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) { SetWhite(1f - t / 0.35f); yield return null; }
            SetWhite(0f);
            _respawning = false;
        }

        // ---------- Lines ----------

        /// <summary>Plays lines one after another on the subtitle line, then calls <paramref name="after"/>.</summary>
        void Say(IList<HouseLine> lines, Action after)
        {
            _lines.Clear();
            foreach (var l in lines) _lines.Enqueue(l);
            _afterLines = after;
            _lineLeft = 0f;
            _lineText = "";
            NextLine();
        }

        void NextLine()
        {
            if (_lines.Count == 0)
            {
                _lineText = "";
                _subtitle.style.display = DisplayStyle.None;
                var after = _afterLines;
                _afterLines = null;
                after?.Invoke();
                return;
            }
            var l = _lines.Dequeue();
            _lineText = l.speaker + ": " + l.text;
            _lineLeft = l.seconds;
            _subtitle.text = _lineText;
            _subtitle.style.display = DisplayStyle.Flex;
            GameRoot.Audio.Play("blip", 0.35f);
        }

        void TickLines(float dt)
        {
            if (_lineText.Length == 0) return;
            _lineLeft -= dt;
            if (_lineLeft <= 0f) NextLine();
        }

        /// <summary>Skips the lines on screen (tests, impatient presenters): the next step happens at once.</summary>
        public void SkipLines()
        {
            int guard = 16;
            while ((_lineText.Length > 0 || _lines.Count > 0) && guard-- > 0) { _lineLeft = 0f; NextLine(); }
        }

        // ---------- Screen ----------

        void BuildUi()
        {
            _white = new VisualElement { name = "house-whiteout", pickingMode = PickingMode.Ignore };
            _white.style.position = Position.Absolute;
            _white.style.left = 0; _white.style.right = 0; _white.style.top = 0; _white.style.bottom = 0;
            _white.style.backgroundColor = new Color(1f, 0.98f, 0.95f, 1f);
            _white.style.opacity = 0f;
            _white.style.display = DisplayStyle.None;
            GameRoot.Ui.Root.Add(_white);
            _subtitle = Line("house-subtitle", 60, 22);
            _prompt = Line("house-argument-prompt", 140, 20);
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
            l.style.paddingLeft = 12; l.style.paddingRight = 12; l.style.paddingTop = 6; l.style.paddingBottom = 6;
            l.style.backgroundColor = new Color(0.1f, 0.08f, 0.16f, 0.55f);
            l.style.display = DisplayStyle.None;
            GameRoot.Hud.Root.Add(l);
            return l;
        }

        void SetWhite(float a)
        {
            _whiteAlpha = Mathf.Clamp01(a);
            if (_white == null) return;
            _white.style.opacity = _whiteAlpha;
            _white.style.display = _whiteAlpha > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------- Presenter, smoke, autopilot ----------

        /// <summary>Presenter chapter: the front garden, hide-and-seek (just begun), the argument (the debug choice), the
        /// house tipping (the escape from its start, running), the front door (the umbrella and the rain).</summary>
        public void JumpToChapter(string chapter)
        {
            StopAllCoroutines();
            _respawning = false;
            SetWhite(0f);
            GameRoot.Ui.SetFade(0f);
            switch (chapter)
            {
                case DreamHouse.ChapterArrival:
                    NewVisit();
                    break;
                case DreamHouse.ChapterHideAndSeek:
                    _story.JumpTo(HousePhase.Pancakes);
                    ResetHouse();
                    _stretched = true;
                    PlaceAt(_h.hubCheckpoint, false);
                    BeginHideAndSeek();
                    break;
                case DreamHouse.ChapterArgument:
                    _story.JumpTo(HousePhase.Argument);
                    ResetHouse();
                    _stretched = true;
                    PlaceAt(_h.hubCheckpoint, false);
                    BeginArgument();
                    break;
                case DreamHouse.ChapterTips:
                case DreamHouse.ChapterFrontDoor:
                    _story.JumpTo(HousePhase.Escape);
                    ResetHouse();
                    _stretched = true;
                    BeginEscape(chapter == DreamHouse.ChapterTips ? _h.escapeCheckpoint : _h.frontDoorCheckpoint);
                    break;
            }
        }

        bool SkipToNextCheckpoint()
        {
            if (_story.Phase == HousePhase.WayIn) JumpToChapter(DreamHouse.ChapterHideAndSeek);
            else if (_story.Phase == HousePhase.HideAndSeek || _story.Phase == HousePhase.Pancakes) JumpToChapter(DreamHouse.ChapterArgument);
            else if (_story.Phase == HousePhase.Argument) JumpToChapter(DreamHouse.ChapterTips);
            else JumpToChapter(DreamHouse.ChapterFrontDoor);
            return true;
        }

        /// <summary>The course autopilot drives her (tests, the smoke run, attract mode): through the way in and the escape.</summary>
        public void SetAutopilot(bool on)
        {
            if (on)
            {
                _pilot.Reset(_s - 1f);
                player.Autopilot = _pilot.Next;
            }
            else player.Autopilot = null;
        }

        IEnumerator AttractDrive(string segment, float seconds, SyntheticInput input)
        {
            SetAutopilot(true);
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && GameRoot.Flow.InRun) yield return null;
            SetAutopilot(false);
            input.Clear();
        }

        /// <summary>The smoke run: the house tipping, driven by the autopilot from its start out of the front door to the
        /// end of the rain. It must get out with no respawn; otherwise it logs an error, which fails the smoke run.</summary>
        IEnumerator SmokeDrive()
        {
            JumpToChapter(DreamHouse.ChapterTips);
            int respawns = _respawns, finishes = _finishes;
            SetAutopilot(true);
            float end = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < end && GameRoot.Flow.InRun && _finishes == finishes) yield return null;
            SetAutopilot(false);
            bool out_ = _finishes > finishes;
            string report = "out " + out_ + ", respawns " + (_respawns - respawns);
            if (!out_ || _respawns != respawns) Debug.LogError("JAMKIT_HOUSE_SMOKE_FAIL the house tipping: " + report);
            else Debug.Log("JAMKIT_HOUSE_SMOKE the house tipping ok: " + report);
        }
    }
}
