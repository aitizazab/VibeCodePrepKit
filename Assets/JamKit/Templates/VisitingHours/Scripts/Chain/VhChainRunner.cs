using System;
using System.Collections;
using JamKit.DreamEscape;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The chain from the title to the end card (docs/NIGHTS-1-2.md): Day 1 and its booth in the Visiting Hours scene, Night 1
    /// in its own scene, Day 2 and its booth back in the Visiting Hours scene, Night 2 (the house), the end card. Lives on the
    /// GameRoot (added by <see cref="VhBoot"/>). The booth's dive asks it for the night (<see cref="StartNight"/>); the night's
    /// game says it's over (<see cref="DreamStreetGame.NightEnded"/>, <see cref="DreamHouseGame.Escaped"/>, <see cref="DreamEscapeGame.Escaped"/>, or any scene's
    /// <see cref="Chain.Done"/>) and the next waking segment loads. What's carried (the journal, Force, the outcomes) is in
    /// the save (<see cref="Chain"/>). Off in batch runs (tests, the default smoke) unless asked for: there the slice keeps
    /// its own Night 1 street, so its tests and the 20 s smoke stay as they were.
    /// </summary>
    public sealed class VhChainRunner : MonoBehaviour
    {
        /// <summary>The smoke budget (s) from which the smoke walks the whole chain (a shorter smoke keeps the slice's path).</summary>
        public const float SmokeChainBudget = 60f;

        static VhChainRunner _instance;
        static bool _forceEnabled;
        static bool _hasPending;
        static ChainSegment _pending;

        public static VhChainRunner Instance => _instance;

        /// <summary>Tests: the chain is on even in a batch run.</summary>
        public static bool ForceEnabled
        {
            get => _forceEnabled;
            set => _forceEnabled = value;
        }

        /// <summary>The chain runs: in a player's game always; in a batch run only when a test asks or the smoke has the time.</summary>
        public static bool Enabled => _forceEnabled || !Application.isBatchMode || SmokeWalksTheChain;

        /// <summary>The smoke run walks the whole chain (it was given at least <see cref="SmokeChainBudget"/> seconds).</summary>
        public static bool SmokeWalksTheChain => SmokeRunner.Requested(out float s) && s >= SmokeChainBudget;

        ChainSegment _night;
        bool _inNight;
        Action _onDone;
        Action<string> _onForcedOut;

        public bool InNight => _inNight;
        public ChainSegment Night => _night;

        /// <summary>A line per hand-off (the smoke log and the tests read them).</summary>
        public static event Action<string> HandOff;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _forceEnabled = false;
            _hasPending = false;
            HandOff = null;
        }

        void Awake()
        {
            _instance = this;
            _onDone = OnNightDone;
            _onForcedOut = OnForcedOut;
            Chain.SegmentDone += _onDone;
            Chain.ForcedOutRaised += _onForcedOut;
        }

        void OnDestroy()
        {
            Chain.SegmentDone -= _onDone;
            Chain.ForcedOutRaised -= _onForcedOut;
            Unhook();
            if (_instance == this) _instance = null;
        }

        static SaveData Save => GameRoot.Exists ? GameRoot.Save.Data : null;

        static string SliceScene => VhBoot.Config != null && !string.IsNullOrEmpty(VhBoot.Config.sceneName) ? VhBoot.Config.sceneName : "VisitingHours";

        static void Say(string what)
        {
            Debug.Log("JAMKIT_CHAIN " + what);
            HandOff?.Invoke(what);
        }

        // ---------- The waking scene asks where it starts ----------

        /// <summary>
        /// Where the Visiting Hours scene starts: the segment the chain loaded it for, or (a fresh run from the title, a
        /// presenter jump) Day 1 with the chain reset: an empty journal, no tallies.
        /// </summary>
        public static ChainSegment TakeStartSegment()
        {
            if (_hasPending)
            {
                _hasPending = false;
                return _pending;
            }
            if (Save != null) Chain.Reset(Save);
            return ChainSegment.Day1;
        }

        /// <summary>The scene a night is played in: the first of its candidates that's in the build, or null.</summary>
        public static string NightScene(ChainSegment night)
        {
            foreach (var name in Chain.NightScenes(night))
                if (SceneService.Exists(name)) return name;
            return null;
        }

        // ---------- Into a night, and out of it ----------

        /// <summary>
        /// The booth's dive: keeps what the day found (the journal, Force) in the save and loads the night's scene. False
        /// when the chain is off or the night's scene isn't in the build (the slice plays its own street instead).
        /// </summary>
        public bool StartNight(ChainSegment night, Journal journal, int force)
        {
            if (!Enabled || !Chain.IsNight(night) || !GameRoot.Exists) return false;
            string scene = NightScene(night);
            if (scene == null)
            {
                Say(night + ": no scene for it in the build (" + string.Join(", ", Chain.NightScenes(night)) + "); the slice carries on without it");
                return false;
            }
            var save = Save;
            if (journal != null)
                for (int i = 0; i < journal.Count; i++) Chain.Record(save, journal.IdAt(i));
            save.SetEntry(Chain.ForceKey, force.ToString());
            Chain.SetSegment(save, night);
            GameRoot.Save.Save();
            Hook(night);
            // Nothing of the waking day's sound goes under with her (each night sets its own).
            GameRoot.Audio.StopAmbience(0.8f);
            GameRoot.Audio.StopMusic(0.8f);
            TellTheHill(night, scene);
            if (!GameRoot.Instance.SceneService.Load(scene))
            {
                DreamStreetGame.NextNight = 0;
                Unhook();
                return false;
            }
            Say(Chain.Title(night) + ": loading " + scene + " (journal " + Chain.Known(save).Count + ", force " + Chain.Force(save) + ")");
            return true;
        }

        /// <summary>
        /// The hill plays both nights (World's request, docs/redesign/world.md): whoever loads it says which night first
        /// (<see cref="DreamStreetGame.NextNight"/>, used once). Night 1 says 1, Night 2 says 2: on Night 2 she starts on Rowan
        /// Street outside the house, and reaching the gate loads DreamHouse (the street does it; nothing here listens to
        /// <see cref="DreamStreetGame.Night2AtTheDoor"/>). Any other scene: nothing to say.
        /// </summary>
        static void TellTheHill(ChainSegment night, string scene)
        {
            int n = Chain.NightOnTheHill(night, scene);
            DreamStreetGame.NextNight = n;
            if (n > 0) Say(Chain.Title(night) + ": the hill plays night " + n);
        }

        void Hook(ChainSegment night)
        {
            Unhook();
            _night = night;
            _inNight = true;
            DreamHouseGame.Escaped += _onDone;
            DreamEscapeGame.Escaped += _onDone;
            DreamStreetGame.NightEnded += _onDone;
        }

        void Unhook()
        {
            if (_onDone == null) return;
            DreamHouseGame.Escaped -= _onDone;
            DreamEscapeGame.Escaped -= _onDone;
            DreamStreetGame.NightEnded -= _onDone;
            _inNight = false;
        }

        /// <summary>
        /// The dream threw her out (<see cref="Chain.ForceOut"/>: a night's fail state with a cost). The forced exit is kept
        /// (the next day opens on the crash call, his monitor runs higher, the strain is a step of Despair and burns Mags's
        /// "maybe six"), then the night ends as it would have.
        /// </summary>
        void OnForcedOut(string reason)
        {
            if (!_inNight) return;
            Chain.RecordForcedOut(Save, _night, reason);
            Say(Chain.Title(_night) + ": forced out (" + reason + "), strain " + Chain.Strain(Save));
            OnNightDone();
        }

        /// <summary>Tests and the debug menu: as if the night forced her out now.</summary>
        public void ForceOutNow(string reason) => OnForcedOut(reason);

        /// <summary>The night is over (Nadia's out): the next waking segment (Day 2, or the end card) loads.</summary>
        void OnNightDone()
        {
            if (!_inNight) return;
            var night = _night;
            Unhook();
            var save = Save;
            if (DreamHouseGame.LenaCondemned && string.IsNullOrEmpty(Chain.LenaOutcome(save))) Chain.SetLenaOutcome(save, "Condemns");
            var next = Chain.Next(night);
            Chain.SetSegment(save, next);
            GameRoot.Save.Save();
            Say(Chain.Title(night) + " done: " + Chain.Title(next) + " next");
            LoadWaking(next);
        }

        static void LoadWaking(ChainSegment segment)
        {
            _pending = segment;
            _hasPending = true;
            // The night's sound stops at the hand-off (the ward sets its own).
            GameRoot.Audio.StopAmbience(0.8f);
            GameRoot.Audio.StopMusic(0.8f);
            GameRoot.Instance.SceneService.LoadQueued(SliceScene);
        }

        // ---------- Debug jumps (the presenter panel's chain chapters) ----------

        /// <summary>A fresh run straight into a segment (the presenter's "Chain: ..." chapters). The journal is left as the
        /// save has it, so a jump to Night 2 still knows what was found before.</summary>
        public void JumpTo(ChainSegment segment)
        {
            if (!GameRoot.Exists) return;
            var save = Save;
            Chain.SetSegment(save, segment);
            if (Chain.IsNight(segment))
            {
                string scene = NightScene(segment);
                if (scene == null)
                {
                    Say(segment + ": no scene for it in the build");
                    return;
                }
                Hook(segment);
                TellTheHill(segment, scene);
                if (GameRoot.Instance.StartRunAt(scene)) Say("jump: " + Chain.Title(segment) + " (" + scene + ")");
                else
                {
                    DreamStreetGame.NextNight = 0;
                    Unhook();
                }
                return;
            }
            Unhook();
            _pending = segment;
            _hasPending = true;
            if (GameRoot.Instance.StartRunAt(SliceScene)) Say("jump: " + Chain.Title(segment));
            else _hasPending = false;
        }

        /// <summary>Ends the current night as if Nadia got out (the smoke run, tests, the debug skip in a night).</summary>
        public void EndNightNow()
        {
            if (_inNight) OnNightDone();
        }

        // ---------- The smoke run, through the whole chain ----------

        /// <summary>
        /// The smoke's walk through the chain once the slice's Day 1 has reached the dive: each night's scene loads and
        /// its game starts, then it's ended as if she got out; Day 2 is skipped through to its booth; the end card shows.
        /// Any hand-off that doesn't happen fails the run (a logged error).
        /// </summary>
        public IEnumerator SmokeWalk()
        {
            foreach (var night in new[] { ChainSegment.Night1, ChainSegment.Night2 })
            {
                yield return Until(() => _inNight && _night == night && !GameRoot.IsLoadingScene, Chain.Title(night) + " to load", 40f);
                yield return Until(() => NightGameUp(), Chain.Title(night) + "'s game to start", 20f);
                Say("smoke: " + Chain.Title(night) + " is up in " + SceneService.ActiveName);
                yield return Real(1f);
                EndNightNow();
                var day = Chain.Next(night);
                yield return Until(() => !GameRoot.IsLoadingScene && VisitingHoursGame.Current != null && VisitingHoursGame.Current.ChainSegment == day, Chain.Title(day) + " to load", 40f);
                Say("smoke: " + Chain.Title(day) + " is up");
                if (day == ChainSegment.End)
                {
                    yield return Until(() => VisitingHoursGame.Current != null && VisitingHoursGame.Current.CurrentMode == VisitingHoursGame.Mode.End, "the end card", 15f);
                    Say("smoke: the end card: " + VisitingHoursGame.Current.Hud.EndBody.Replace('\n', ' '));
                    yield break;
                }
                // Day 2: through to its booth and the dive (the debug skip does each step).
                for (int step = 0; step < 6 && !_inNight; step++)
                {
                    var g = VisitingHoursGame.Current;
                    yield return Until(() => g == null || g.CurrentMode == VisitingHoursGame.Mode.Explore || _inNight, "Day 2 to be free", 20f);
                    if (_inNight || g == null) break;
                    for (int i = 0; i < 4 && GameRoot.Dialogue.IsActive; i++) GameRoot.Dialogue.AutoStep();
                    g.DebugSkip();
                    yield return Real(0.5f);
                }
            }
        }

        static bool NightGameUp() => DreamHouseGame.Current != null || DreamEscapeGame.Current != null || DreamStreetGame.Current != null || !GameRoot.IsLoadingScene && SceneService.ActiveName != SliceScene;

        static IEnumerator Until(Func<bool> condition, string what, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.AutoStep();
                if (Time.realtimeSinceStartup > end)
                {
                    Debug.LogError("JAMKIT_CHAIN smoke: timed out waiting for " + what);
                    yield break;
                }
                yield return null;
            }
        }

        static IEnumerator Real(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
