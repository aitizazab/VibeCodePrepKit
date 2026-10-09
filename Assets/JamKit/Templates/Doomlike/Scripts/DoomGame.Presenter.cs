using System.Collections;
using JamKit.Combat;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.AI;

namespace JamKit.Doomlike
{
    /// <summary>Presenter mode: the chapter jumps (a level, the boss up close, the end tally) and
    /// attract mode's driver (walks the level towards the exit along the NavMesh, turning like a stick
    /// does, and shoots what it sees).</summary>
    public sealed partial class DoomGame
    {
        AttractDriver _attractDriver;
        NavMeshPath _attractPath;

        void SetupPresenter()
        {
            DoomPresenter.Register(PresenterMode.Chapters, this, this);
            _attractDriver = AttractDrive;
            PresenterMode.RegisterDriver(this, DoomPresenter.Scene, _attractDriver);
        }

        void TeardownPresenter() => PresenterMode.Unregister(this);

        /// <summary>Level <paramref name="index"/> (0-based, clamped) from its start, with full health.</summary>
        public void PresenterLevel(int index)
        {
            ClearForJump();
            LoadLevel(Mathf.Clamp(index, 0, Mathf.Max(0, config.LevelCount - 1)));
        }

        /// <summary>The boss's level, every weapon in hand, a few steps from the boss and facing it (awake).</summary>
        public void PresenterBoss()
        {
            ClearForJump();
            for (int i = config.LevelCount - 1; i >= 0; i--)
            {
                LoadLevel(i);
                if (_boss != null) break;
            }
            _arsenal.GiveAll();
            if (_view != null) _view.SetWeapon(_arsenal.CurrentAsset);
            if (_boss == null) return;
            Vector3 b = _boss.transform.position;
            Vector3 away = _level.startPosition - b;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            Vector3 spot = b + away.normalized * 9f;
            if (NavMesh.SamplePosition(spot, out var hit, 6f, NavMesh.AllAreas)) spot = hit.position;
            player.Teleport(spot + Vector3.up * 0.05f);
            Vector3 d = b - spot;
            player.FaceYaw(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            _boss.Alert();
        }

        /// <summary>The last level's tally screen ("LEVEL COMPLETE"), its enemies beaten.</summary>
        public void PresenterTally()
        {
            ClearForJump();
            LoadLevel(Mathf.Max(0, config.LevelCount - 1));
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i].Alive) _enemies[i].TakeDamage(100000f, DamageKind.Generic, 0);
            projectiles.Clear();
            fx.Clear();
            CompleteLevel();
        }

        void ClearForJump()
        {
            _choice.Close();
            _hud.HideTally();
            _hud.HideCard();
            GameRoot.Hud.ClearMessage();
            _flash = 0f;
            _fireNeedsRelease = false;
            if (player.Health != null)
            {
                if (player.Health.IsDead) player.Health.Revive();
                player.Health.Heal(player.Health.Max);
            }
            player.ResetView();
        }

        // ---------- Attract mode ----------

        IEnumerator AttractDrive(string segment, float seconds, SyntheticInput input)
        {
            float end = Time.unscaledTime + seconds;
            player.GiveAllKeys(); // locked doors don't stop the tour
            if (segment == "fight") _arsenal.GiveAll();
            if (_attractPath == null) _attractPath = new NavMeshPath();
            var gameInput = GameRoot.Input;
            var stick = System.Numerics.Vector2.Zero;
            float repath = 0f, useClock = 0f, t = 0f;
            int corner = 1;
            Vector3 lastPos = player.transform.position;
            float stuck = 0f;
            while (Time.unscaledTime < end)
            {
                if (_phase != Phase.Playing || _level == null) { input.Clear(); yield return null; continue; }
                float dt = Time.unscaledDeltaTime;
                t += dt;
                Vector3 pos = player.transform.position;
                Vector3 eye = player.EyePosition;
                var foe = AttractTarget(eye, segment == "fight" ? 40f : 20f);
                float targetYaw, targetPitch = 0f;
                Vector2 move;
                bool fire = false;
                if (foe != null)
                {
                    Vector3 to = foe.Center - eye;
                    float flat = new Vector2(to.x, to.z).magnitude;
                    targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    targetPitch = -Mathf.Atan2(to.y, Mathf.Max(0.1f, flat)) * Mathf.Rad2Deg;
                    fire = Mathf.Abs(NaturalStick.DeltaAngle(player.Yaw, targetYaw)) < 7f;
                    // Strafe while shooting; close in when far, back off when close.
                    float fwd = flat > 12f ? 0.6f : flat < 5f ? -0.4f : 0f;
                    move = new Vector2(Mathf.Sin(t * 1.3f) * 0.7f, fwd);
                }
                else
                {
                    repath -= dt;
                    if (repath <= 0f || corner >= _attractPath.corners.Length)
                    {
                        repath = 1f;
                        corner = 1;
                        NavMesh.CalculatePath(pos, _level.ExitPosition, NavMesh.AllAreas, _attractPath);
                    }
                    var corners = _attractPath.corners;
                    while (corner < corners.Length && Flat(corners[corner] - pos) < 0.7f) corner++;
                    Vector3 goal = corner < corners.Length ? corners[corner] : _level.ExitPosition;
                    Vector3 to = goal - pos;
                    targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    float gap = Mathf.Abs(NaturalStick.DeltaAngle(player.Yaw, targetYaw));
                    move = new Vector2(0f, gap < 30f ? 1f : gap < 70f ? 0.45f : 0.1f);
                    // Bumping into something (a door): press Use now and then.
                    stuck = (pos - lastPos).sqrMagnitude < 0.0004f ? stuck + dt : 0f;
                    useClock -= dt;
                    if (stuck > 0.4f && useClock <= 0f)
                    {
                        input.Tap(gameInput.Interact);
                        useClock = 0.8f;
                    }
                }
                lastPos = pos;
                float yawIn = NaturalStick.Turn(player.Yaw, targetYaw, 50f);
                float pitchIn = Mathf.Clamp((player.Pitch - targetPitch) / 25f, -1f, 1f);
                stick = NaturalStick.Step(stick, new System.Numerics.Vector2(yawIn, pitchIn), 4f, dt);
                input.look = new Vector2(stick.X, stick.Y);
                input.move = Vector2.Lerp(input.move, move, Mathf.Clamp01(dt * 6f));
                input.Hold(gameInput.Fire, fire);
                yield return null;
            }
            input.Clear();
        }

        /// <summary>The nearest live enemy in range with a clear line from the eye (the boss first in a fight).</summary>
        DoomEnemy AttractTarget(Vector3 eye, float range)
        {
            DoomEnemy best = null;
            float bestD = range;
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (!e.Alive) continue;
                Vector3 to = e.Center - eye;
                float d = to.magnitude;
                if (d > bestD) continue;
                if (Physics.Raycast(eye, to / Mathf.Max(0.01f, d), out var hit, d - 0.3f, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider != e.body) continue;
                best = e;
                bestD = d;
            }
            return best;
        }
    }
}
