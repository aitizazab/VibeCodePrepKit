using System.Collections;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Roguelite;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>Presenter mode: the chapter jumps (a floor, its boss, a room type) and attract mode's
    /// driver (a fight dodged with a hand-like stick, a draft and an encounter picked after a look).</summary>
    public sealed partial class BulletHellGame
    {
        AttractDriver _attractDriver;

        void SetupPresenter()
        {
            BulletHellPresenter.Register(PresenterMode.Chapters, this, this);
            _attractDriver = AttractDrive;
            PresenterMode.RegisterDriver(this, BulletHellPresenter.Scene, _attractDriver);
        }

        void TeardownPresenter() => PresenterMode.Unregister(this);

        /// <summary>Jumps to floor <paramref name="floor"/> (0-based; clamped to the run): its first fight,
        /// or its boss. Enemies, bullets, panels and lines go quietly; the player is healed and the
        /// floor's card, colours and music come up as if walking in.</summary>
        public void PresenterFloor(int floor, bool boss)
        {
            if (_run == null || _run.Plan == null || _phase == RoomPhase.Finished) return;
            var plan = _run.Plan;
            floor = Mathf.Clamp(floor, 0, plan.floors.Count - 1);
            int steps = plan.RoomCount(floor);
            var want = boss ? RoomType.Boss : RoomType.Combat;
            int step = boss ? steps - 1 : 0, choice = 0;
            for (int s = boss ? steps - 1 : 0; boss ? s >= 0 : s < steps; s += boss ? -1 : 1)
            {
                int c = plan.Step(floor, s).options.IndexOf(want);
                if (c < 0) continue;
                step = s;
                choice = c;
                break;
            }
            ClearForJump();
            _run.floor = floor;
            _run.step = step;
            _run.choice = choice;
            _run.complete = false;
            _lastFloor = -1; // the floor card, colours and bombs as on arrival
            EnterRoom();
        }

        /// <summary>Turns the current room into <paramref name="type"/> (a Treasure draft, an encounter...).</summary>
        public void PresenterRoom(RoomType type)
        {
            if (_run == null || _phase == RoomPhase.Finished) return;
            ClearForJump();
            DebugEnterRoom(type);
            GameRoot.Audio.PlayMusic(GameRoot.Config.gameMusic);
        }

        void ClearForJump()
        {
            _runStartPending = false;
            _lineActive = false;
            _afterLine = null;
            _eventActive = false;
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop(true);
            _choice.Close();
            _afterChoice = null;
            _rewardQueue.Clear();
            _pending.Clear();
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Alive) _enemies[i].Despawn();
            field.Enemy.Clear();
            field.Player.Clear();
            if (boss.IsActive) boss.Hide();
            _hud.HideBoss();
            _bombTimer = 0f;
            if (coins != null) { coins.Clear(); coins.attractAll = false; }
            if (fx != null) fx.ClearEffects();
            if (player.Health != null && player.Health.Current < player.Health.Max) player.Heal(player.Health.Max);
            player.RefillSprint();
        }

        // ---------- Attract mode ----------

        IEnumerator AttractDrive(string segment, float seconds, SyntheticInput input)
        {
            float end = Time.unscaledTime + seconds;
            switch (segment)
            {
                case "draft":
                case "encounter":
                    while (Time.unscaledTime < end)
                    {
                        if (_choice.IsOpen && GameRoot.Flow.Current == GameState.Playing)
                        {
                            yield return AttractChoose(segment == "encounter" ? 2.2f : 1.8f);
                            break;
                        }
                        yield return null;
                    }
                    // Back to the action for the rest of the segment.
                    yield return AttractFight(end, input);
                    break;
                default:
                    yield return AttractFight(end, input);
                    break;
            }
            input.Clear();
        }

        /// <summary>Weaves through the bullets near the bottom of the playfield with fire held, the way
        /// a player sways: a slow drifting target, pushed away from nearby enemy bullets, followed by a
        /// stick that can't snap. Panels that open (rewards, doors) are picked after a look.</summary>
        IEnumerator AttractFight(float end, SyntheticInput input)
        {
            var gameInput = GameRoot.Input;
            float t = Random.value * 10f;
            var stick = System.Numerics.Vector2.Zero;
            while (Time.unscaledTime < end)
            {
                if (_phase == RoomPhase.Finished || !GameRoot.Flow.InRun) yield break;
                if (_choice.IsOpen && GameRoot.Flow.Current == GameState.Playing)
                {
                    input.Hold(gameInput.Fire, false);
                    input.move = Vector2.zero;
                    yield return AttractChoose(1.6f);
                    continue;
                }
                float dt = Time.unscaledDeltaTime;
                t += dt;
                input.Hold(gameInput.Fire, true);
                Vector2 pp = player.Position;
                float w = _bounds.Width, h = _bounds.Height;
                var home = new Vector2(
                    (_bounds.minX + _bounds.maxX) * 0.5f + Mathf.Sin(t * 0.55f) * w * 0.32f + Mathf.Sin(t * 1.7f) * w * 0.05f,
                    _bounds.minY + h * (0.2f + 0.06f * Mathf.Sin(t * 0.83f)));
                Vector2 want = (home - pp) * 0.9f;
                want += Dodge(pp) * 1.6f;
                stick = NaturalStick.Step(stick, new System.Numerics.Vector2(want.x, want.y), 5f, dt);
                input.move = new Vector2(stick.X, stick.Y);
                yield return null;
            }
            input.Hold(gameInput.Fire, false);
            input.move = Vector2.zero;
        }

        /// <summary>Away from the enemy bullets within reach, the closest weighing most.</summary>
        Vector2 Dodge(Vector2 pp)
        {
            var pool = field.Enemy;
            var items = pool.Items;
            const float reach = 1.4f;
            Vector2 push = Vector2.zero;
            for (int i = 0; i < pool.Count; i++)
            {
                var b = items[i].position;
                float dx = pp.x - b.X, dy = pp.y - b.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 > reach * reach || d2 < 1e-6f) continue;
                float d = Mathf.Sqrt(d2);
                float k = (reach - d) / (reach * d);
                push.x += dx * k;
                push.y += dy * k;
            }
            return Vector2.ClampMagnitude(push, 1.5f);
        }

        /// <summary>Looks at the open panel for <paramref name="look"/> seconds, then picks like a player would:
        /// a card (the first it can take), and walks out of shops and encounters it has seen.</summary>
        IEnumerator AttractChoose(float look)
        {
            float until = Time.unscaledTime + look;
            while (Time.unscaledTime < until) yield return null;
            if (!_choice.IsOpen) yield break;
            var kind = _choice.Current;
            if (kind == ChoicePanel.Kind.Shop) { FinishChoice(); yield break; }
            int card = _choice.FirstEnabledCard();
            if (card >= 0 && card + 1 < _choice.CardCount && _choice.IsCardEnabled(card + 1) && Random.value < 0.5f) card++;
            if (card >= 0) _choice.Choose(card);
            else if (_choice.FooterCount > 0) _choice.ChooseFooter(0);
            else FinishChoice();
            // An encounter's pick list: look again, then take one.
            if (_choice.IsOpen && _choice.Current == ChoicePanel.Kind.Pick)
            {
                until = Time.unscaledTime + 1.4f;
                while (Time.unscaledTime < until) yield return null;
                int pick = _choice.FirstEnabledCard();
                if (pick >= 0) _choice.Choose(pick);
            }
            // Out of an encounter after taking what it offered.
            if (_choice.IsOpen && IsEncounterPanel(_choice.Current) && kind != ChoicePanel.Kind.Draft)
            {
                until = Time.unscaledTime + 1.2f;
                while (Time.unscaledTime < until) yield return null;
                if (_choice.IsOpen && IsEncounterPanel(_choice.Current)) FinishChoice();
            }
        }
    }
}
