using System;
using System.Collections;
using System.Collections.Generic;
using JamKit.Argument;
using JamKit.DeepBlock;
using JamKit.Flow;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.VisitingHours.Characters;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using BattleOutcome = JamKit.Argument.ArgumentOutcome;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 1 on Larch Hill, played (docs/redesign/world.md "Night 1, beat by beat"; core <see cref="LarchHill"/>,
    /// <see cref="LarchStreetStory"/>):
    /// <list type="number">
    /// <item><b>The far bank.</b> The whole hill ahead: Walt's shop across the creek, the house above its roof, St. Brigid's
    /// on top. The Lindqvists' loop plays behind her. The creek is the first double jump (a fall: wade to the ford, climb
    /// out, again).</item>
    /// <item><b>Walt's shop.</b> The garage door half up (the first slide); Saturday's paper on the counter; the ledger on
    /// the workbench (optional); the patched bike. Into the yard: Walt on his wall; his argument (the battle agent's).</item>
    /// <item><b>The back way</b> (the lessons): Walt's low wall (mantle), the Pruitts' pool (wall-run), the fallen pergola
    /// (slide), the Hendersons' wall (jump and grab), the slot (wall-jumps) up to Mill Street.</item>
    /// <item><b>The lower band</b> (the first test): up Larch Green, Back Lane B's garage roofs, the ditch, the Old Mill's
    /// shutter on the loop's timer, the loading bay, the scaffold's bar, the old oak up to Rowan Street (its treehouse and
    /// the lane's nail are optional memories).</item>
    /// <item><b>The Rewind</b> (the set piece): on Rowan Street she sees Eli at his barbecue and calls him; the dream
    /// rewinds the street behind her and she has to outrun it through the block party to the Lasnames' gate. Caught: back
    /// to the end of the street (a little slower wave each time).</item>
    /// <item><b>The gate.</b> Through the window: Eli at the stove, Theo setting the table. He looks up and sees her; Lena
    /// steps between them and takes him inside; the door shuts, the porch light goes out. Mags. The night ends.</item>
    /// </list>
    /// Everything learnt goes into the save's dream journal (<see cref="DreamJournal"/>). Without a listener on
    /// <see cref="NightEnded"/> the night starts again.
    /// </summary>
    public sealed class DreamStreetGame : MonoBehaviour
    {
        public MovementController player;
        [Tooltip("Walt's collider on his wall (in the way of the low end until his argument).")]
        public GameObject waltGate;
        public Transform walt, neighbour, dog, sprinklerJet, ball;
        public GameObject paper, ledger, nail, treehouseMemory;
        public Transform patchedBike, garageDoor, shutter, wave;
        public GameObject eliasOutside;
        public Transform eliasInside, theoInside, lenaInside, frontDoor;
        [Tooltip("The front door hinge's yaw while the door stands open (shut is 0): +90 for the house builder's door.")]
        public float frontDoorOpenYaw = -95f;
        public Light porchLight;
        public GameObject porchGlow;
        public Transform houseRoot;
        [Tooltip("The old plank across the creek (there when she told him about the creek: DreamChanges n1_creek_plank).")]
        public GameObject creekPlank;
        [Tooltip("The hospital's lit windows (there when she told him his patients ask after him: n1_hospital_lit).")]
        public GameObject hospitalLit;
        [Tooltip("Dream changes to act as if the bedside made them (debugging, captures); the chain's save is read too.")]
        public string[] forceDreamChanges = new string[0];
        [Tooltip("1: Night 1 from the far bank. 2: Night 2 on Rowan Street outside the house, the loop replaying.")]
        public int night = 1;
        [Tooltip("Standalone: Day 1's memorial card counts as known (the chain passes the real journal).")]
        public bool assumeDay1Facts = true;
        [Tooltip("Play Walt's argument (the argument agent's battle). Off, or under the autopilot: the debug choice.")]
        public bool useArgumentBattle = true;
        [Tooltip("Lena's star watch on Nadia's right wrist (People's VhKeepsakeWatch), built hidden; shown while the keepsake is hers.")]
        public VhKeepsakeWatch keepsakeWatch;
        [Tooltip("Whether Nadia has Lena's watch tonight (the keepsake: a Night 4 mechanic; the chain or a test sets it).")]
        public bool keepsakeOwned;
        [Tooltip("Night 2: the scene her reaching the gate goes on to when nothing listens to Night2AtTheDoor (the House agent's night).")]
        public string night2HouseScene = "DreamHouse";

        /// <summary>The night the next DreamStreetGame to start plays (0: its own <see cref="night"/>). Set it to 2 before
        /// loading DreamStreet for Night 2 (the chain); it's used once.</summary>
        public static int NextNight;

        static DreamStreetGame _current;
        static Action _nightEnded, _night2AtTheDoor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            NextNight = 0;
            _nightEnded = null;
            _night2AtTheDoor = null;
        }

        public static DreamStreetGame Current => _current;

        /// <summary>Fires when the door has shut and Mags has spoken (wire the chain here). Without a listener the night restarts.</summary>
        public static event Action NightEnded
        {
            add => _nightEnded += value;
            remove => _nightEnded -= value;
        }

        /// <summary>Night 2: she reached the Lasnames' gate (the House agent's night goes in from here).</summary>
        public static event Action Night2AtTheDoor
        {
            add => _night2AtTheDoor += value;
            remove => _night2AtTheDoor -= value;
        }

        LarchHill _h;
        readonly LarchStreetStory _story = new LarchStreetStory();
        NeighbourLoop _loop;
        CourseAutopilot _pilot;
        BlockRoute _route;
        readonly List<BlockRoute> _chain = new List<BlockRoute>();
        int _chainAt;
        VisualElement _white;
        Label _subtitle, _prompt;
        readonly Queue<StreetLine> _lines = new Queue<StreetLine>();
        readonly List<string> _made = new List<string>();
        readonly HashSet<string> _said = new HashSet<string>();
        float _lineLeft, _night, _loopPrev, _whiteAlpha;
        string _lineText = "";
        Action _afterLines;
        int _heardLine, _respawns, _catches;
        bool _waitingForWalt, _ending, _ended, _eliasGone, _wading, _waveOn, _safe;
        float _waveX, _waveT, _waveSpeed;
        string _band = "farbank";
        Vector3 _waltHome, _ballFrom, _ballTo, _dogHome, _eliasHome;
        ArgumentHost _battle;
        Func<MoveSim, MoveInput> _hold;
        Func<bool> _skip;
        MoveProfile _wade;
        bool _ledgerOut, _plank, _hospitalLit, _waltWarm;
        Collider _shutterCollider;
        VhCharacter _eliasModel;
        // The spots the frame asks about, looked up once (LarchHill.Spot allocated a lambda per call: Perf's find).
        HillSpot _spotChatter, _spotYard, _spotRowan, _spotGate, _spotCreekWater, _spotPool, _spotDitch, _spotLoopZone;

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
        /// <summary>How many times the Rewind has caught her this night.</summary>
        public int Catches => _catches;
        public bool WaveOn => _waveOn;
        public float WaveX => _waveX;
        public bool Wading => _wading;
        /// <summary>What the bedside changed in tonight's dream (docs/redesign/day.md): the ledger out, the plank, the lit
        /// hospital, Walt's warm hello.</summary>
        public bool LedgerOut => _ledgerOut;
        public bool PlankBack => _plank;
        public bool HospitalLit => _hospitalLit;
        public bool WaltWarm => _waltWarm;
        public string Band => _band;
        /// <summary>Which leg of the chained autopilot run it's on (-1: none).</summary>
        public int ChainLeg => _chain.Count > 0 ? _chainAt : -1;
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
            _spotChatter = _h.Spot(LarchHill.SpotChatter);
            _spotYard = _h.Spot(LarchHill.SpotYard);
            _spotRowan = _h.Spot(LarchHill.SpotRowan);
            _spotGate = _h.Spot(LarchHill.SpotGate);
            _spotCreekWater = _h.Spot(LarchHill.SpotCreekWater);
            _spotPool = _h.Spot(LarchHill.SpotPool);
            _spotDitch = _h.Spot(LarchHill.SpotDitch);
            _spotLoopZone = _h.Spot(LarchHill.SpotLoopZone);
            if (shutter != null) _shutterCollider = shutter.GetComponent<Collider>();
            if (eliasOutside != null) _eliasModel = eliasOutside.GetComponent<VhCharacter>();
            // Night 2 on the hill: whoever loads the scene for Night 2 says so first (NextNight), as the scene is the same.
            if (NextNight > 0) { night = NextNight; NextNight = 0; }
            _loop = new NeighbourLoop(1);   // Night 2 replays Night 1's loop exactly
            _hold = sim => new MoveInput { yaw = sim.Yaw };
            _wade = new MoveProfile { doubleJump = false, wallrun = false, slide = false, airStrafeScale = 0.3f, speedCap = 2.6f };
            if (walt != null) _waltHome = walt.position;
            if (eliasOutside != null) _eliasHome = eliasOutside.transform.position;
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
            GameRoot.Run.Set("Dream", night == 2 ? "Larch Hill (Night 2)" : "Larch Hill (Night 1)");
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

        /// <summary>The critical path's polylines at foot height (the geometry lint's routes: walls by them must run).</summary>
        public Vector3[][] LintRoutes()
        {
            var h = _h ?? LarchHill.Build();
            var all = new List<BlockRoute> { h.wayIn };
            all.AddRange(h.night1);
            var result = new Vector3[all.Count][];
            for (int i = 0; i < all.Count; i++)
            {
                var line = all[i].course.route;
                int n = Mathf.Max(2, (int)(line.Length / 2f));
                var pts = new Vector3[n + 1];
                float y = all[i].start.Y;
                for (int k = 0; k <= n; k++)
                {
                    var p = line.PointAt(line.Length * k / n, 0f);
                    float top = h.TopAt(p.X, p.Z, y + 2.5f);
                    if (top > -7f) y = top;
                    pts[k] = new Vector3(p.X, y + 0.1f, p.Z);
                }
                result[i] = pts;
            }
            return result;
        }

        // ---------- The night ----------

        /// <summary>Starts the night from the far bank (Night 2: from Rowan Street outside the house). What she learnt stays
        /// in the save's journal.</summary>
        public void NewNight()
        {
            StopAllCoroutines();
            _story.Reset();
            _lines.Clear();
            _said.Clear();
            _lineText = "";
            _afterLines = null;
            _heardLine = 0;
            _catches = 0;
            _waitingForWalt = _ending = _ended = _eliasGone = _waveOn = _safe = false;
            _night = 0f;
            _loopPrev = 0f;
            _route = null;
            _chain.Clear();
            if (_battle != null) { var b = _battle; _battle = null; b.Close(false); }
            if (player.Autopilot == _hold) player.Autopilot = null;
            SetWading(false);
            SetWaltAside(false);
            ReadDreamChanges();
            if (paper != null) paper.SetActive(true);
            if (ledger != null) ledger.SetActive(_ledgerOut);
            if (creekPlank != null) creekPlank.SetActive(_plank);
            if (hospitalLit != null) hospitalLit.SetActive(_hospitalLit);
            if (nail != null) nail.SetActive(true);
            if (treehouseMemory != null) treehouseMemory.SetActive(true);
            if (eliasOutside != null) { eliasOutside.SetActive(true); eliasOutside.transform.position = _eliasHome; }
            if (_eliasModel != null) _eliasModel.Play("idle");
            // Lena's watch on her wrist once it's hers (the Battle agent saves vh_lena_watch when Lena ends Heard out).
            SetKeepsakeOwned(keepsakeOwned || GameRoot.Exists && JamKit.VisitingHours.Chain.HasLenaWatch(GameRoot.Save.Data));
            SetWindow(false);
            if (frontDoor != null) frontDoor.localRotation = Quaternion.Euler(0f, frontDoorOpenYaw, 0f);
            if (porchLight != null) porchLight.enabled = true;
            if (porchGlow != null) porchGlow.SetActive(true);
            ShowWave(false);
            SetWhite(0f);
            if (_prompt != null) _prompt.style.display = DisplayStyle.None;
            if (assumeDay1Facts || Journal.Has(Entry, LarchStreetStory.FactEffects)) _story.Learn(LarchStreetStory.FactEffects);
            if (night == 2)
            {
                // Night 2: on the street outside the house, Walt already behind her, the loop replaying.
                _story.Advance(StreetPhase.Shop);
                _story.Learn(LarchStreetStory.MemWaltAlive);
                ChooseWalt(WaltOutcome.HeardOut, true);
                _story.Advance(StreetPhase.Top);
                _safe = true;
                _band = "rowan";
                PlaceAt(HillAnchors.Night2Start, HillAnchors.Night2StartYaw);
                return;
            }
            _band = "farbank";
            PlaceAt(_h.arrival, _h.arrivalYaw);
            Say(LarchStreetLines.Arrival, null);
            if (_plank) Say(LarchStreetLines.Plank, null);
            if (_hospitalLit) Say(LarchStreetLines.HospitalLit, null);
        }

        /// <summary>What she told him at the bedside, read from the save (Chain.HasDreamChange) and the forced list.</summary>
        void ReadDreamChanges()
        {
            var save = GameRoot.Exists ? GameRoot.Save.Data : null;
            bool Has(string id) => System.Array.IndexOf(forceDreamChanges ?? new string[0], id) >= 0 || JamKit.VisitingHours.Chain.HasDreamChange(save, id);
            _ledgerOut = Has(JamKit.VisitingHours.DreamChanges.WaltLedger);
            _plank = Has(JamKit.VisitingHours.DreamChanges.CreekPlank);
            _hospitalLit = Has(JamKit.VisitingHours.DreamChanges.HospitalLit);
            _waltWarm = Has(JamKit.VisitingHours.DreamChanges.WaltWarm);
            Debug.Log("JAMKIT_STREET dream changes: ledger " + _ledgerOut + ", plank " + _plank + ", hospital lit " + _hospitalLit + ", Walt warm " + _waltWarm);
        }

        // ---------- The save's dream journal ----------

        static string Entry => GameRoot.Exists ? GameRoot.Save.Data.GetEntry(DreamJournal.Key) : null;
        static class Journal
        {
            public static bool Has(string entry, string id) => DreamJournal.Has(entry, id);
        }

        /// <summary>Learns an entry (and any deduction it completes): into the story, the save's journal and the lines.</summary>
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
            TickLoop();
            TickShutter();
            if (GameRoot.Flow.Current != GameState.Playing || _ending) return;
            var p = player.transform.position;
            var feet = N(p);
            if (p.y < -9f) { Recover(); return; }
            TickWater(feet);
            TickPhase(feet);
            TickWave(dt, p);
            TickMemories(p);
            TickBand(feet);
            if (!_said.Contains("chatter") && _spotChatter != null && _spotChatter.Contains(feet)) { _said.Add("chatter"); Say(LarchStreetLines.Chatter, null); }
        }

        /// <summary>Says <paramref name="lines"/> the first time she stands in spot <paramref name="spot"/>.</summary>
        void Once(string spot, System.Numerics.Vector3 feet, StreetLine[] lines)
        {
            if (_said.Contains(spot)) return;
            var s = _h.Spot(spot);
            if (s == null || !s.Contains(feet)) return;
            _said.Add(spot);
            Say(lines, null);
        }

        void TickPhase(System.Numerics.Vector3 feet)
        {
            switch (_story.Phase)
            {
                case StreetPhase.Arrival:
                    if (feet.Z > LarchHill.CreekZ1 - 0.5f && feet.Y > -0.5f && feet.X > LarchHill.WaltX0 && feet.X < LarchHill.WaltX1)
                    {
                        _story.Advance(StreetPhase.Shop);
                        _band = "walt";
                        if (_said.Add("garage")) Say(LarchStreetLines.GarageDoor, null);
                    }
                    break;
                case StreetPhase.Shop:
                    if (_spotYard != null && _spotYard.Contains(feet))
                    {
                        _story.Advance(StreetPhase.Yard);
                        Learn(LarchStreetStory.MemWaltAlive, LarchStreetLines.WaltAlive);
                        _story.Advance(StreetPhase.Walt);
                        Say(_waltWarm ? LarchStreetLines.WaltWarm : LarchStreetLines.WaltBlocks, BeginArgument);
                    }
                    break;
                case StreetPhase.Climb:
                    Once(LarchHill.SpotLessonMantle, feet, LarchStreetLines.LessonMantle);
                    Once(LarchHill.SpotLessonWallrun, feet, LarchStreetLines.LessonWallrun);
                    Once(LarchHill.SpotLessonSlide, feet, LarchStreetLines.LessonSlide);
                    Once(LarchHill.SpotLessonClimb, feet, LarchStreetLines.LessonClimb);
                    Once(LarchHill.SpotLessonChimney, feet, LarchStreetLines.LessonChimney);
                    Once(LarchHill.SpotMillStreet, feet, LarchStreetLines.MillStreet);
                    Once(LarchHill.SpotLane, feet, LarchStreetLines.Lane);
                    Once(LarchHill.SpotShutter, feet, LarchStreetLines.Shutter);
                    Once(LarchHill.SpotOak, feet, LarchStreetLines.Oak);
                    if (feet.Y > LarchHill.RowanY - 0.3f && _spotRowan != null && _spotRowan.Contains(feet)) BeginRewind();
                    break;
                case StreetPhase.Top:
                    if (_spotGate != null && _spotGate.Contains(feet))
                    {
                        if (night == 2) { _story.Advance(StreetPhase.Ending); Debug.Log("JAMKIT_STREET Night 2 at the door"); Night2Door(); }
                        else StartCoroutine(TheDoorCloses());
                    }
                    break;
            }
        }

        /// <summary>Which band she's on (for the way back after a fall out of the world).</summary>
        void TickBand(System.Numerics.Vector3 feet)
        {
            if (!player.Sim.Grounded) return;
            _band = feet.Y > HillAnchors.M1Y - 0.5f ? "middle" : feet.Y > LarchHill.RowanY - 0.4f && feet.Z > LarchHill.LaneZ1 ? "rowan"
                : feet.Z > LarchHill.LaneZ0 - 6f && feet.X < LarchHill.GreenX0 ? "lane" : feet.Z > LarchHill.MillWallZ0 && feet.Y > LarchHill.MillY - 0.5f ? "mill"
                : feet.Z > LarchHill.YardZ0 && feet.X < LarchHill.WaltX0 ? "rowa" : feet.Z > LarchHill.CreekZ1 ? "walt" : "farbank";
        }

        /// <summary>Shows or hides Lena's watch on Nadia's wrist (the keepsake is hers, or not).</summary>
        public void SetKeepsakeOwned(bool owned)
        {
            keepsakeOwned = owned;
            if (keepsakeWatch != null) keepsakeWatch.SetOwned(owned);
        }

        /// <summary>Night 2 at the Lasnames' gate: whoever listens takes the night on (the chain, a test); with nobody
        /// listening, the House agent's scene loads (the house, hide-and-seek, the escape).</summary>
        void Night2Door()
        {
            if (_night2AtTheDoor != null) { _night2AtTheDoor.Invoke(); return; }
            if (!string.IsNullOrEmpty(night2HouseScene) && SceneService.Exists(night2HouseScene) && GameRoot.Exists)
            {
                Debug.Log("JAMKIT_STREET Night 2 goes into the house: " + night2HouseScene);
                GameRoot.Instance.SceneService.Load(night2HouseScene);
            }
        }

        // ---------- Water: wading is slow ----------

        void TickWater(System.Numerics.Vector3 feet)
        {
            bool creek = _spotCreekWater != null && _spotCreekWater.Contains(feet);
            bool pool = _spotPool != null && _spotPool.Contains(feet);
            bool ditch = _spotDitch != null && _spotDitch.Contains(feet) && feet.Y < LarchHill.LaneY - 2f;
            SetWading(creek || pool);
            if (creek && _said.Add("creekfall")) Say(LarchStreetLines.CreekFall, null);
            if (pool && _said.Add("poolfall")) Say(LarchStreetLines.PoolFall, null);
            if (ditch && _said.Add("ditchfall")) Say(LarchStreetLines.DitchFall, null);
        }

        void SetWading(bool on)
        {
            if (on == _wading) return;
            _wading = on;
            player.OverrideProfile(on ? _wade : null);
            if (on) GameRoot.Audio.Play("splash", 0.7f);
        }

        void TickMemories(Vector3 p)
        {
            var chest = p + Vector3.up * 0.9f;
            if (Near(paper, chest)) { paper.SetActive(false); Learn(LarchStreetStory.MemSaturdayPaper, LarchStreetLines.Paper); }
            if (Near(ledger, chest)) { ledger.SetActive(false); Learn(LarchStreetStory.MemWaltLedger, LarchStreetLines.Ledger); Say(LarchStreetLines.LedgerOpen, null); }
            if (Near(nail, chest)) { nail.SetActive(false); Learn(LarchStreetStory.MemTheNail, LarchStreetLines.Nail); }
            if (Near(treehouseMemory, chest)) { treehouseMemory.SetActive(false); Learn(LarchStreetStory.MemTreehouse, LarchStreetLines.Treehouse); }
            if (patchedBike != null && !_said.Contains("bike") && (chest - patchedBike.position - Vector3.up * 0.6f).sqrMagnitude < 2.2f * 2.2f)
            {
                _said.Add("bike");
                Say(LarchStreetLines.PatchedBike, null);
            }
        }

        static bool Near(GameObject m, Vector3 chest) => m != null && m.activeSelf && (chest - m.transform.position).sqrMagnitude < 1.6f * 1.6f;

        // ---------- The neighbours' loop, the shutter ----------

        void TickLoop()
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
            if (_spotLoopZone == null || !_spotLoopZone.Contains(N(player.transform.position))) return;
            _heardLine++;
            Say(new[] { new StreetLine(LarchStreetLines.Neighbour, night == 2 ? "Morning, Nadia! " + LarchStreetLines.LoopLine : LarchStreetLines.LoopLine, 2.4f) }, null);
            if (_heardLine >= 2) Learn(LarchStreetStory.MemNeighboursLoop, LarchStreetLines.LoopHeard);
        }

        /// <summary>The Old Mill's shutter goes up and down on the loop's clock: its collider follows its bottom edge.</summary>
        void TickShutter()
        {
            if (shutter == null) return;
            float gap = LarchHill.ShutterGap(_night);
            float h = LarchHill.ArchTop - LarchHill.LaneY - gap;
            shutter.localScale = new Vector3(1f, Mathf.Max(0.02f, h), 1f);
            shutter.position = new Vector3(shutter.position.x, LarchHill.LaneY + gap, shutter.position.z);
            bool solid = h > 0.05f;
            if (_shutterCollider != null && _shutterCollider.enabled != solid) _shutterCollider.enabled = solid;
        }

        // ---------- Walt ----------

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

        /// <summary>How Walt's argument ended: he gets up off his wall, the outcome is kept, the back way opens.</summary>
        public void ChooseWalt(WaltOutcome outcome) => ChooseWalt(outcome, false);

        void ChooseWalt(WaltOutcome outcome, bool quiet)
        {
            if (outcome == WaltOutcome.None) outcome = WaltOutcome.HeardOut;
            if (_story.Phase < StreetPhase.Arguing) { _story.Advance(StreetPhase.Shop); _story.Advance(StreetPhase.Yard); _story.Advance(StreetPhase.Walt); _story.Advance(StreetPhase.Arguing); }
            if (!_story.EndArgument(outcome)) return;
            _waitingForWalt = false;
            if (_battle != null) { var b = _battle; _battle = null; b.Close(false); }
            if (_prompt != null) _prompt.style.display = DisplayStyle.None;
            if (GameRoot.Exists && !quiet)
            {
                GameRoot.Save.Data.SetEntry(LarchStreetStory.WaltOutcomeKey, LarchStreetStory.WaltOutcomeValue(outcome));
                GameRoot.Save.Save();
            }
            Debug.Log("JAMKIT_STREET Walt steps aside: " + outcome);
            SetWaltAside(true);
            if (!quiet) Say(LarchStreetLines.WaltAside(outcome), null);
        }

        void SetWaltAside(bool aside)
        {
            if (waltGate != null) waltGate.SetActive(!aside);
            if (walt != null && _h != null) walt.position = aside ? V(_h.Spot(LarchHill.SpotWaltAside).position) : _waltHome;
        }

        // ---------- The Rewind ----------

        void BeginRewind()
        {
            if (_waveOn || _safe) return;
            _story.Advance(StreetPhase.Top);
            _band = "rowan";
            _waveOn = true;
            _waveT = 0f;
            _waveSpeed = Mathf.Max(LarchHill.RewindMinSpeed, LarchHill.RewindSpeed - LarchHill.RewindSlowPerCatch * _catches);
            _waveX = LarchHill.RewindFromX;
            ShowWave(true);
            if (_catches == 0) { Say(LarchStreetLines.EliasGlimpsed, null); Say(LarchStreetLines.RewindBegins, null); }
            if (!_eliasGone) StartCoroutine(EliasGoesIn());
            GameRoot.Audio.Play("rewind", 0.8f);
            Debug.Log("JAMKIT_STREET the Rewind begins (" + _waveSpeed.ToString("0.0") + " m/s, catch " + _catches + ")");
        }

        void TickWave(float dt, Vector3 p)
        {
            if (!_waveOn) return;
            _waveT += dt;
            if (_waveT > LarchHill.RewindDelay) _waveX += _waveSpeed * dt;
            if (wave != null) wave.position = new Vector3(_waveX, LarchHill.RowanY, wave.position.z);
            if (p.x >= LarchHill.RewindSafeX)
            {
                _waveOn = false;
                _safe = true;
                ShowWave(false);
                Say(LarchStreetLines.RewindOutrun, null);
                Debug.Log("JAMKIT_STREET outran the Rewind (" + _catches + " catches)");
                return;
            }
            if (p.x <= _waveX && p.y > LarchHill.LaneY - 1f) StartCoroutine(Caught());
        }

        /// <summary>The wave reached her: a white rewind, and she's back at the street's west end (the wave slower).</summary>
        IEnumerator Caught()
        {
            _waveOn = false;
            _catches++;
            var autopilot = player.Autopilot;
            player.Autopilot = _hold;
            GameRoot.Audio.Play("rewind", 1f);
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) { SetWhite(t / 0.35f); yield return null; }
            var band = _h.Band("rowan");
            PlaceAt(band.recover, band.recoverYaw);
            _night = 0f;   // the street's Saturday starts over (the loop, the shutter)
            ShowWave(false);
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) { SetWhite(1f - t / 0.5f); yield return null; }
            SetWhite(0f);
            player.Autopilot = autopilot == _hold ? null : autopilot;
            if (_route != null && _pilot != null) _pilot.Reset(0f);
            Say(LarchStreetLines.RewindCaught[Mathf.Min(_catches - 1, LarchStreetLines.RewindCaught.Length - 1)], null);
            BeginRewind();
        }

        void ShowWave(bool on)
        {
            if (wave == null) return;
            wave.gameObject.SetActive(on);
            wave.position = new Vector3(LarchHill.RewindFromX, LarchHill.RowanY, wave.position.z);
        }

        // ---------- The gate ----------

        IEnumerator EliasGoesIn()
        {
            _eliasGone = true;
            if (eliasOutside == null) yield break;
            var from = eliasOutside.transform.position;
            var to = from + new Vector3(-4.5f, 0f, 2.5f);
            var homeRotation = eliasOutside.transform.rotation;
            if (_eliasModel != null)
            {
                _eliasModel.transform.rotation = Quaternion.LookRotation(to - from, Vector3.up);
                _eliasModel.Play("walk", 0.2f);
            }
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                eliasOutside.transform.position = Vector3.Lerp(from, to, t / 2f);
                yield return null;
            }
            eliasOutside.SetActive(false);
            eliasOutside.transform.SetPositionAndRotation(from, homeRotation);
            SetWindow(true);
        }

        void SetWindow(bool show)
        {
            if (theoInside != null) theoInside.gameObject.SetActive(show);
            if (eliasInside != null) eliasInside.gameObject.SetActive(show);
            if (lenaInside != null) lenaInside.gameObject.SetActive(false);
        }

        /// <summary>The ending (no chase): at the gate she's held; Eli looks up and sees her; Lena steps between them and takes
        /// him inside; the door shuts; the porch light goes out; Mags; the fade. Then <see cref="NightEnded"/>.</summary>
        IEnumerator TheDoorCloses()
        {
            _ending = true;
            _waveOn = false;
            ShowWave(false);
            _story.Advance(StreetPhase.Ending);
            player.Autopilot = _hold;
            SetWindow(true);
            if (eliasOutside != null) eliasOutside.SetActive(false);
            Debug.Log("JAMKIT_STREET the garden gate: the door closes");
            yield return Wait(1.2f);
            if (eliasInside != null)
            {
                var e0 = eliasInside.localPosition;
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) { eliasInside.localPosition = e0 + new Vector3(0.4f * t / 0.5f, 0.05f * Mathf.Sin(t * 12f), -0.6f * t / 0.5f); yield return null; }
            }
            yield return Wait(0.9f);
            if (lenaInside != null && eliasInside != null)
            {
                lenaInside.gameObject.SetActive(true);
                var l0 = lenaInside.localPosition;
                var l1 = eliasInside.localPosition + new Vector3(0.3f, 0f, -0.5f);
                for (float t = 0f; t < 0.8f; t += Time.deltaTime) { lenaInside.localPosition = Vector3.Lerp(l0, l1, t / 0.8f); yield return null; }
                yield return Wait(0.7f);
                var e1 = eliasInside.localPosition;
                var l2 = lenaInside.localPosition;
                for (float t = 0f; t < 1f; t += Time.deltaTime)
                {
                    eliasInside.localPosition = e1 + new Vector3(-3f * t, 0f, 1.2f * t);
                    lenaInside.localPosition = l2 + new Vector3(-3f * t, 0f, 1.2f * t);
                    yield return null;
                }
                eliasInside.gameObject.SetActive(false);
                lenaInside.gameObject.SetActive(false);
                lenaInside.localPosition = l0;
            }
            yield return Wait(0.5f);
            if (frontDoor != null)
            {
                for (float t = 0f; t < 0.6f; t += Time.deltaTime) { frontDoor.localRotation = Quaternion.Euler(0f, Mathf.Lerp(frontDoorOpenYaw, 0f, t / 0.6f), 0f); yield return null; }
                frontDoor.localRotation = Quaternion.identity;
            }
            GameRoot.Audio.Play("ui_back", 0.8f);
            yield return Wait(0.8f);
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
            GameRoot.Hud.Message("End of Night 1.", 3f);
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

        // ---------- Placing, falling out of the world ----------

        void PlaceAt(System.Numerics.Vector3 feet, float yaw) => player.Teleport(V(feet) + Vector3.up * 0.02f, yaw, Vector3.zero);

        /// <summary>Only a bug drops her out of the world (every fall lands on the band below); if it happens she's put back
        /// at the start of the band she was on, and it costs her that band's climb.</summary>
        void Recover()
        {
            _respawns++;
            var band = _h.Band(_band) ?? _h.Band("farbank");
            PlaceAt(band.recover, band.recoverYaw);
            Debug.LogWarning("JAMKIT_STREET fell out of the world: back to " + band.name);
            GameRoot.Hud.Message("The dream catches you.", 1.5f);
        }

        // ---------- Chapters, the autopilot ----------

        /// <summary>Presenter chapters: the creek, Walt, the back way, Back Lane B, the old oak, the Rewind, the house.</summary>
        public void JumpToChapter(string chapter)
        {
            NewNight();
            SkipLines();
            if (chapter == LarchHill.ChapterArrival) return;
            _story.Advance(StreetPhase.Shop);
            _said.Add("garage");
            if (chapter == LarchHill.ChapterWalt)
            {
                PlaceAt(new System.Numerics.Vector3(96.1f, 0f, 10.5f), 0f);
                return;
            }
            Learn(LarchStreetStory.MemWaltAlive, null);
            ChooseWalt(WaltOutcome.HeardOut);
            SkipLines();
            BlockRoute r = null;
            if (chapter == LarchHill.ChapterBackWay) r = _h.night1[0];
            else if (chapter == LarchHill.ChapterLane) r = _h.night1[1];
            else if (chapter == LarchHill.ChapterOak) r = _h.night1[3];
            else if (chapter == LarchHill.ChapterRewind) r = _h.night1[4];
            else if (chapter == LarchHill.ChapterHouse)
            {
                _story.Advance(StreetPhase.Top);
                _safe = true;
                PlaceAt(new System.Numerics.Vector3(14f, LarchHill.RowanY, 110f), 45f);
                return;
            }
            if (r != null) PlaceAt(r.start, r.startYaw);
        }

        /// <summary>The course autopilot runs <paramref name="route"/>; null hands control back.</summary>
        public void Drive(BlockRoute route)
        {
            _chain.Clear();
            DriveOne(route);
        }

        void DriveOne(BlockRoute route)
        {
            _route = route;
            if (route == null) { if (player.Autopilot != _hold) player.Autopilot = null; return; }
            _pilot = new CourseAutopilot(route.course);
            _pilot.Reset(route.course.route.Project(N(player.transform.position)));
            player.Autopilot = sim => NextChained(sim);
        }

        /// <summary>The whole of Night 1's critical path under the autopilot, leg after leg (the recorded run): from the
        /// arrival, Walt's argument settled by the debug choice, to the gate.</summary>
        public void DriveNight1()
        {
            _chain.Clear();
            _chain.Add(_h.wayIn);
            _chain.AddRange(_h.night1);
            _chainAt = 0;
            PlaceAt(_h.arrival, _h.arrivalYaw);
            _route = _chain[0];
            _pilot = new CourseAutopilot(_route.course);
            _pilot.Reset(0f);
            player.Autopilot = sim => NextChained(sim);
        }

        MoveInput NextChained(MoveSim sim)
        {
            if (_route == null) return new MoveInput { yaw = sim.Yaw };
            float s = _route.course.route.Project(N(player.transform.position));
            if (_chain.Count > 0 && s >= _route.finishS - 0.6f && sim.Grounded && _chainAt + 1 < _chain.Count)
            {
                _chainAt++;
                _route = _chain[_chainAt];
                _pilot = new CourseAutopilot(_route.course);
                _pilot.Reset(_route.course.route.Project(N(player.transform.position)));
            }
            if (_waitingForWalt) return new MoveInput { yaw = sim.Yaw };
            return _pilot.Next(sim);
        }

        bool DebugSkip()
        {
            if (_waitingForWalt) { ChooseWalt(WaltOutcome.HeardOut); return true; }
            if (_story.Phase < StreetPhase.Walt) { JumpToChapter(LarchHill.ChapterWalt); return true; }
            if (_story.Phase < StreetPhase.Top) { JumpToChapter(LarchHill.ChapterRewind); return true; }
            if (!_ending) { JumpToChapter(LarchHill.ChapterHouse); return true; }
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
            _white.style.backgroundColor = new Color(1f, 0.95f, 0.84f, 1f);
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
