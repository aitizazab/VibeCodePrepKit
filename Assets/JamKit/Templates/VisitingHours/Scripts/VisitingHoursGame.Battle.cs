using JamKit.Shell;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>Walt's argument: the core ArgumentBattle ticked with the move input, drawn by
    /// VhBattleView, with sounds and screen feel for hits, cracks and wrong objects.</summary>
    public sealed partial class VisitingHoursGame
    {
        ArgumentBattle _battle;
        float _battleSpeed = 1f;
        PilotAction[] _pilot;
        bool _afterBattleStarted;

        public ArgumentBattle Battle => _battle;

        /// <summary>Plays the battle faster than real time (the smoke run and tests; 1 = normal).</summary>
        public float BattleSpeed
        {
            get => _battleSpeed;
            set => _battleSpeed = Mathf.Clamp(value, 0.1f, 40f);
        }

        /// <summary>When set, an autopilot plays the battle: per statement, listen, press or say its key.</summary>
        public PilotAction[] Pilot
        {
            get => _pilot;
            set => _pilot = value;
        }

        /// <summary>The argument starts (after Walt's "Easy. Watch the pattern first.").</summary>
        public void StartBattle()
        {
            if (_mode == Mode.Battle) return;
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            _battle = new ArgumentBattle(config.walt, _state.journal, (ulong)Random.Range(1, int.MaxValue));
            var s = GameRoot.Settings;
            _battle.SpeedScale = s.enemyBulletSpeedScale;
            _battle.DensityScale = s.enemyDensityScale;
            _battle.Invulnerable = DebugMenu.PlayerInvincible;
            _mode = Mode.Battle;
            _afterBattleStarted = false;
            _hud.SetPrompt("");
            _hud.SetExploring(false);
            _battleView.Show(_battle, config);
            _battle.Start();
            GameRoot.Audio.PlayMusic("music_story_tense", 1.5f);
        }

        void TickBattle(float dt)
        {
            if (_battle == null) return;
            if (_battle.Phase == BattlePhase.Done)
            {
                if (!_afterBattleStarted)
                {
                    _afterBattleStarted = true;
                    _state.waltOutcome = _battle.Outcome;
                    _state.force += _battle.ForceAdded;
                    GameRoot.Audio.StopMusic(1.5f);
                    StartCoroutine(AfterBattle(_battle.Outcome));
                }
                return;
            }
            _battle.Invulnerable = DebugMenu.PlayerInvincible || _pilot != null && _battleInvulnerable;
            // Substeps keep a fast-forwarded battle exact (the emitters are frame-rate independent anyway).
            float total = dt * _battleSpeed;
            int steps = Mathf.Clamp(Mathf.CeilToInt(total / (1f / 60f)), 1, 60);
            float step = total / steps;
            for (int i = 0; i < steps && _battle.Phase != BattlePhase.Done; i++)
            {
                Vector2 move;
                if (_pilot != null)
                {
                    int si = _battle.StatementIndex;
                    var action = si >= 0 && si < _pilot.Length ? _pilot[si] : PilotAction.Listen;
                    if (_battle.Phase == BattlePhase.Interlude && si + 1 < _pilot.Length && _pilot[si + 1] == PilotAction.Press) action = PilotAction.Press;
                    string key = _battle.Statement != null ? _battle.Statement.key : "";
                    var v = BattlePilot.Steer(_battle, BattlePilot.Goal(_battle, action, key));
                    move = new Vector2(v.X, v.Y);
                }
                else move = GameRoot.Input.MoveVector;
                _battle.Tick(step, new System.Numerics.Vector2(move.x, move.y));
                React();
            }
        }

        bool _battleInvulnerable = true;

        /// <summary>The autopilot can't be hit (default) or plays for real (tests of losing composure).</summary>
        public bool PilotInvulnerable
        {
            get => _battleInvulnerable;
            set => _battleInvulnerable = value;
        }

        void React()
        {
            var events = _battle.Events;
            var audio = GameRoot.Audio;
            var juice = GameRoot.Juice;
            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i].kind)
                {
                    case BattleEventKind.Hit:
                        audio.Play("vh_hit");
                        juice.Shake(0.25f);
                        juice.Flash(new Color(0.6f, 0.1f, 0.12f, 0.25f), 0.12f);
                        break;
                    case BattleEventKind.Crack:
                        audio.Play("vh_crack");
                        juice.Flash(new Color(1f, 0.97f, 0.88f, 0.75f), 0.35f);
                        juice.Shake(0.3f);
                        break;
                    case BattleEventKind.Wrong:
                        audio.Play("vh_wrong");
                        juice.Shake(0.15f);
                        break;
                    case BattleEventKind.ObjectAppeared:
                        audio.Play("page", 0.4f);
                        break;
                    case BattleEventKind.StatementStart:
                        audio.Play("voice_low", 0.6f);
                        break;
                }
            }
        }
    }
}
