using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEngine;

namespace JamKit.StealthLab
{
    /// <summary>
    /// The Stealth Lab (docs/STEALTH.md "The Stealth Lab"): a grey-box yard (core <see cref="StealthYard"/>) to test the
    /// stealth kit with the movement kit. Two rounds, picked by presenter chapter: <b>hide and seek</b> (the seeker counts
    /// to ten facing away, then searches every hiding spot and corner; found means the round's over) and <b>patrol</b>
    /// (a guard walks a loop; get spotted and it chases, lose it and it searches, then gives up). Either way there are
    /// five things to collect, each reached a different way (a mantle, a wallrun, a slide, the closet, the dark alley).
    /// R / View restarts the round. The stealth HUD shows the detection meter and what's left to find.
    /// </summary>
    public sealed class StealthLabGame : MonoBehaviour
    {
        public enum Round { HideAndSeek, Patrol }

        public MovementController player;
        public SeekerAgent seeker;
        public CollectibleCounter collectibles;
        public Vector3 playerStart;
        public float playerYaw;
        [Range(0f, 1f)] public float ambientLight = 0.75f;

        static StealthLabGame _current;
        StealthHud _hud;
        Round _round = Round.HideAndSeek;
        bool _over;
        int _founds, _rounds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static StealthLabGame Current => _current;
        public Round CurrentRound => _round;
        public bool RoundOver => _over;
        public int Founds => _founds;
        public StealthHud Hud => _hud;

        void Start()
        {
            if (!GameRoot.Exists || player == null || seeker == null)
            {
                Debug.LogError("StealthLabGame: missing the player or the seeker. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            StealthLightZone.Ambient = ambientLight;
            if (collectibles == null) collectibles = CollectibleCounter.Current;
            GameRoot.Hud.Show();
            GameRoot.Hud.ShowHealth(false);
            GameRoot.Hud.SetScoreText("");
            _hud = new StealthHud(GameRoot.Hud.Root);
            MovementComfortScreen.Ensure();
            StealthLabPresenter.Register(PresenterMode.Chapters, this, this);
            GameRoot.Run.Set("Lab", "Stealth Lab");
            seeker.Found += OnFound;
            collectibles.AllFound += OnAllFound;
            Begin(Round.HideAndSeek);
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (seeker != null) seeker.Found -= OnFound;
            if (collectibles != null) collectibles.AllFound -= OnAllFound;
            if (_hud != null) _hud.Remove();
            if (GameRoot.Exists) PresenterMode.Unregister(this);
        }

        /// <summary>Starts a round from the beginning: the player at the start, every collectible back, the seeker home
        /// (counting, for hide and seek). <paramref name="countSeconds"/> &lt; 0: the tuning's count.</summary>
        public void Begin(Round round, float countSeconds = -1f)
        {
            _round = round;
            _over = false;
            _rounds++;
            player.Teleport(playerStart, playerYaw, Vector3.zero);
            if (collectibles != null) collectibles.ResetAll();
            seeker.SetMode(round == Round.HideAndSeek ? SeekerMode.HideAndSeek : SeekerMode.Patrol, countSeconds);
            if (!GameRoot.Exists) return;
            GameRoot.Hud.Message(round == Round.HideAndSeek
                ? "Hide and seek: it counts to ten, then comes looking. Hide (bushes, under the deck, the closet), or keep moving and grab all five."
                : "Patrol: a guard walks the yard. Grab all five without being found. Crouch behind low walls, keep to the dark.", 6f);
        }

        void OnFound(SeekerAgent s)
        {
            _founds++;
            _over = true;
            s.Frozen = true;
            if (!GameRoot.Exists) return;
            GameRoot.Audio.Play("ui_back", 0.8f);
            GameRoot.Hud.Message((_round == Round.HideAndSeek ? "Found you! " : "Spotted! ") + collectibles.Summary + ". R (View) to play again.", 5f);
        }

        void OnAllFound()
        {
            if (_over) return;
            _over = true;
            seeker.Frozen = true;
            if (GameRoot.Exists) GameRoot.Hud.Message("All five, never found. R (View) to play again.", 5f);
        }

        void Update()
        {
            if (_hud != null) _hud.Tick();
            if (!GameRoot.Exists || !GameRoot.Input.GameplayAllowed) return;
            if (GameRoot.Input.Pressed(GameRoot.Input.Restart)) Begin(_round);
        }
    }
}
