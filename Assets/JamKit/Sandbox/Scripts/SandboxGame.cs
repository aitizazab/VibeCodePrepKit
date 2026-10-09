using System.Collections;
using System.Collections.Generic;
using Ink.UnityIntegration;
using JamKit.Combat;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Sandbox
{
    /// <summary>
    /// The sandbox: a tiny game that uses every part of the shell. An ink dialogue at the start,
    /// three coins to collect (Victory), a red hazard that hurts (Game Over at 0 HP), a sign to read
    /// with Interact, and the HUD showing health, coins and time. Read this file as the example of
    /// how a jam game talks to the shell.
    /// </summary>
    public sealed class SandboxGame : MonoBehaviour
    {
        // Presenter mode (docs/PRESENTING.md): the chapters a presenter can jump to, and the attract
        // script that plays the game by itself. Declared at load so the panel lists them on the title.
        public const string Scene = "Sandbox";
        public const string ChapterStart = "Start";
        public const string ChapterSign = "The sign";
        public static readonly string[] Chapters = { ChapterStart, ChapterSign };
        public static readonly string[] Segments = { "coins" };

        public static AttractScript Attract() => new AttractScript(Scene).Jump(ChapterStart, 1f).Drive("coins", 15f).Jump(ChapterSign, 0.5f).Wait(4f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void DeclarePresenter()
        {
            PresenterMode.Declare(Scene, Chapters);
            PresenterMode.SetAttractScript(Attract());
        }

        /// <summary>The live jumps (<paramref name="game"/> may be null in edit-mode tests).</summary>
        public static void RegisterChapters(ChapterRegistry chapters, SandboxGame game, object owner)
        {
            chapters.Register(Scene, ChapterStart, () => game.JumpToStart(), owner);
            chapters.Register(Scene, ChapterSign, () => game.JumpToSign(), owner);
        }

        public InkFile story;
        public string introKnot = "intro";
        public string signKnot = "sign";
        public SandboxPlayer player;
        public Transform sign;
        public float signRadius = 1.5f;
        public float pickupRadius = 0.7f;
        public Vector2 playerHalfSize = new Vector2(0.4f, 0.4f);

        readonly List<SandboxCoin> _coins = new List<SandboxCoin>();
        readonly List<SandboxHazard> _hazards = new List<SandboxHazard>();
        int _collected;
        int _hits;
        bool _nearSign;

        public int Collected => _collected;
        public int Total => _coins.Count;
        public IReadOnlyList<SandboxCoin> Coins => _coins;
        public IReadOnlyList<SandboxHazard> Hazards => _hazards;

        void Start()
        {
            _coins.AddRange(FindObjectsByType<SandboxCoin>(FindObjectsSortMode.None));
            _hazards.AddRange(FindObjectsByType<SandboxHazard>(FindObjectsSortMode.None));
            if (player == null) player = FindAnyObjectByType<SandboxPlayer>();

            var dmg = player.Damageable;
            dmg.Damaged += OnPlayerHurt;
            dmg.Died += _ => GameRoot.Lose();

            GameRoot.Hud.Show();
            RefreshHud();
            GameRoot.Dialogue.Ended += OnDialogueEnded;
            _start = player.transform.position;
            RegisterChapters(PresenterMode.Chapters, this, this);
            PresenterMode.RegisterDriver(this, Scene, AttractDrive);
            if (story != null) GameRoot.Dialogue.Play(story, introKnot);
        }

        void OnDestroy()
        {
            PresenterMode.Unregister(this);
            if (GameRoot.Exists) GameRoot.Dialogue.Ended -= OnDialogueEnded;
        }

        Vector3 _start;

        /// <summary>Chapter "Start": every coin back, full health, at the start, no dialogue.</summary>
        public void JumpToStart()
        {
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            foreach (var c in _coins) c.gameObject.SetActive(true);
            _collected = 0;
            var health = player.Damageable.Health;
            health.Heal(health.Max);
            player.transform.position = _start;
            _nearSign = false;
            RefreshHud();
            GameRoot.Hud.Message("Collect the 3 coins. Don't touch the red block!", 3.5f);
        }

        /// <summary>Chapter "The sign": standing at the sign, reading it.</summary>
        public void JumpToSign()
        {
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            if (sign != null) player.transform.position = sign.position + Vector3.down * 0.8f;
            if (story != null) GameRoot.Dialogue.Play(story, signKnot);
        }

        /// <summary>Attract mode: walks from coin to coin with a hand-like stick.</summary>
        IEnumerator AttractDrive(string segment, float seconds, SyntheticInput input)
        {
            float end = Time.unscaledTime + seconds;
            var stick = System.Numerics.Vector2.Zero;
            while (Time.unscaledTime < end && GameRoot.Flow.InRun)
            {
                SandboxCoin next = null;
                float best = float.MaxValue;
                Vector3 p = player.transform.position;
                foreach (var c in _coins)
                {
                    if (!c.gameObject.activeSelf) continue;
                    float d = (c.transform.position - p).sqrMagnitude;
                    if (d < best) { best = d; next = c; }
                }
                Vector2 want = Vector2.zero;
                if (next != null)
                {
                    Vector3 to = next.transform.position - p;
                    want = new Vector2(to.x, to.y).normalized * Mathf.Clamp01(to.magnitude);
                }
                stick = NaturalStick.Step(stick, new System.Numerics.Vector2(want.x, want.y), 4f, Time.unscaledDeltaTime);
                input.move = new Vector2(stick.X, stick.Y);
                yield return null;
            }
            input.Clear();
        }

        void OnDialogueEnded(string knot)
        {
            if (knot == introKnot) GameRoot.Hud.Message("Collect the 3 coins. Don't touch the red block!", 3.5f);
        }

        void OnPlayerHurt(DamageInfo info, DamageResult result)
        {
            _hits++;
            GameRoot.Run.Set("Hits taken", _hits.ToString());
            RefreshHud();
        }

        void Update()
        {
            if (GameRoot.Flow.Current != GameState.Playing || player == null) return;
            Vector3 p = player.transform.position;

            for (int i = 0; i < _coins.Count; i++)
            {
                var coin = _coins[i];
                if (coin.gameObject.activeSelf && (coin.transform.position - p).sqrMagnitude < pickupRadius * pickupRadius)
                    Collect(coin);
            }
            if (GameRoot.Flow.Current != GameState.Playing) return;

            for (int i = 0; i < _hazards.Count; i++)
            {
                var h = _hazards[i];
                if (h.isActiveAndEnabled && h.Touches(p, playerHalfSize)) player.Damageable.Hit(h.damage, DamageKind.Hazard);
            }

            if (sign != null && story != null)
            {
                bool near = (sign.position - p).sqrMagnitude < signRadius * signRadius;
                if (near != _nearSign)
                {
                    _nearSign = near;
                    if (near) GameRoot.Hud.Message("Press E (or X on a gamepad) to read the sign", 0f);
                    else GameRoot.Hud.ClearMessage();
                }
                if (near && GameRoot.Input.Pressed(GameRoot.Input.Interact))
                {
                    GameRoot.Hud.ClearMessage();
                    _nearSign = false;
                    GameRoot.Dialogue.Play(story, signKnot);
                }
            }
        }

        public void Collect(SandboxCoin coin)
        {
            if (!coin.gameObject.activeSelf) return;
            coin.gameObject.SetActive(false);
            _collected++;
            GameRoot.Run.score += 100;
            GameRoot.Audio.Play("coin");
            GameRoot.Juice.FloatingText(coin.transform.position, "+100", new Color(1f, 0.85f, 0.3f));
            GameRoot.Juice.Shake(0.15f);
            RefreshHud();
            if (_collected >= _coins.Count) GameRoot.Win();
        }

        void RefreshHud()
        {
            var health = player.Damageable.Health;
            GameRoot.Hud.SetHealth(health.Current, health.Max);
            GameRoot.Hud.SetScoreText("Coins " + _collected + " / " + _coins.Count);
            GameRoot.Run.Set("Coins", _collected + " / " + _coins.Count);
        }
    }
}
