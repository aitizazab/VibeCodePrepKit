using JamKit.Shell;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The epics that need the scene (the epic balance pass, docs/DESIGN-ROGUELITE.md section 2); their
    /// numbers are data in BulletHellConfig (Boosts and Shots):
    /// <list type="bullet">
    /// <item>Ghost Step: starting a sprint opens a short phase (bullets pass through), then a cooldown; a few
    /// per fight room, shown as pips under the SPRINT meter.</item>
    /// <item>Guardian Angel: a hit fires a bomb instead, spending every stored bomb, then a cooldown (shown).</item>
    /// <item>Last Gambit (last_stand): on the last life, more damage; every hit ricochets once to the nearest
    /// other enemy and kills burst in a small blast. The ship pulses red while it's on.</item>
    /// <item>Scatter: pellet damage falls off with distance. Blade: an arc slash in front instead of the guns.</item>
    /// <item>Still Point: the damage ramp itself is in PowerBoosts (fed with the ship's movement).</item>
    /// </list>
    /// </summary>
    public sealed partial class BulletHellGame
    {
        static readonly Color GambitColor = new Color(1f, 0.25f, 0.2f);

        readonly GhostPhase _ghostPhase = new GhostPhase();
        readonly GuardianAngel _angel = new GuardianAngel();
        float _bladeClock;
        bool _gambitWasOn, _inBlast;
        int _shownPhases = -1, _shownGhostCd = -1, _shownAngelCd = -1, _bladeSlashes, _ricochets, _blasts;

        public GhostPhase GhostPhases => _ghostPhase;
        public GuardianAngel Guardian => _angel;
        /// <summary>Last Gambit is on: owned, and the player is on the last life.</summary>
        public bool GambitActive => _loadout.Effect(ShmupEffects.LastStand) > 0 && player.Health != null && player.Health.Current <= 1.01f;
        public int BladeSlashes => _bladeSlashes;
        public int Ricochets => _ricochets;
        public int Blasts => _blasts;

        void EpicsBeginRun()
        {
            _ghostPhase.RoomStarted(config.boosts);
            _angel.Reset();
            _bladeClock = 0f;
            _gambitWasOn = false;
            _bladeSlashes = _ricochets = _blasts = 0;
            _shownPhases = _shownGhostCd = _shownAngelCd = -1;
            player.Glow = Color.clear;
            _loadout.Shots = config.shots;
            field.Player.bounceDamageScale = config.shots != null ? config.shots.reboundAfterBounce : 1f;
        }

        /// <summary>A fight room starts: Ghost Step's phases come back.</summary>
        void EpicsRoomStarted() => _ghostPhase.RoomStarted(config.boosts, _loadout.Effect(ShmupEffects.GhostStep));

        void TickEpics(float dt, Vector2 pp, int targets)
        {
            if (_loadout.Effect(ShmupEffects.GhostStep) > 0 && _ghostPhase.Tick(dt, player.Sprinting, config.boosts))
            {
                if (fx != null) fx.Ring(pp, new Color(0.75f, 0.85f, 1f, 0.8f), 0.2f, 1.2f, 0.3f);
                GameRoot.Audio.Play("graze", 0.8f);
            }
            _angel.Tick(dt);

            bool gambit = GambitActive;
            if (gambit && !_gambitWasOn)
            {
                GameRoot.Juice.FloatingText(pp + Vector2.up * 0.9f, "LAST GAMBIT", GambitColor);
                GameRoot.Juice.Flash(new Color(1f, 0.2f, 0.15f, 0.3f), 0.3f);
                GameRoot.Audio.Play("boss_phase", 0.6f);
            }
            _gambitWasOn = gambit;
            player.Glow = gambit ? GambitColor : Color.clear;

            // The Blade: an arc slash in front, instead of the main guns.
            if (_loadout.Effect(ShmupEffects.Blade) > 0 && (_phase == RoomPhase.Waves || _phase == RoomPhase.Boss) && player.ControlsEnabled)
            {
                var shots = config.shots;
                _bladeClock += dt;
                if (_bladeClock >= shots.bladeInterval)
                {
                    _bladeClock -= shots.bladeInterval;
                    Slash(pp, targets, shots);
                }
            }
        }

        void Slash(Vector2 pp, int targets, ShotRules shots)
        {
            _bladeSlashes++;
            var ship = new SVector2(pp.x, pp.y);
            float damage = BladeArc.SlashDamage(shots, config.player.shotDamage * _loadout.ShotDamageScale,
                _loadout.FireInterval(config.player.weapon != null ? config.player.weapon.fireInterval : 0.085f));
            for (int i = 0; i < targets; i++)
            {
                if (!_targets[i].active || !BladeArc.Touches(ship, _targets[i].position, _targets[i].radius, shots.bladeRange, shots.bladeHalfAngle)) continue;
                var owner = _owners[i];
                if (owner == null) boss.Damage(damage);
                else if (owner.Alive && owner.Damage(damage)) KillEnemy(owner);
            }
            if (fx == null) return;
            // The arc, drawn as a fan of sparks.
            for (int k = -3; k <= 3; k++)
            {
                float a = (90f + k * shots.bladeHalfAngle / 3f) * Mathf.Deg2Rad;
                var at = pp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * shots.bladeRange * 0.85f;
                fx.Spark(at, new Color(0.85f, 0.95f, 1f, 0.9f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f, 0.14f, 0.12f);
            }
            if ((_bladeSlashes & 1) == 0) GameRoot.Audio.Play("shoot", 0.25f);
        }

        /// <summary>A hit's damage: Scatter pellets fall off with distance.</summary>
        float HitDamage(BulletHit h) =>
            (h.flags & BulletFlags.Falloff) != 0 ? h.damage * config.shots.ScatterFalloffAt(h.age) : h.damage;

        /// <summary>Last Gambit: a hit ricochets once to the nearest other enemy.</summary>
        void AfterHit(BulletHit h, float damage, int targets)
        {
            if (!GambitActive || (h.flags & BulletFlags.Ricocheted) != 0) return;
            var rules = config.boosts;
            int r = LastGambit.RicochetTarget(_targets, targets, h.targetId, h.position, rules.ricochetRange);
            if (r < 0) return;
            var to = _targets[r].position - h.position;
            if (field.FirePlayer(new Vector2(h.position.X, h.position.Y), Mathf.Atan2(to.Y, to.X), config.player.shotSpeed, damage * rules.ricochetDamage,
                    config.player.homingShotStyle, 0.6f, 0, BulletFlags.Ricocheted, 0f))
                _ricochets++;
        }

        /// <summary>Last Gambit: a kill bursts in a small blast (a blast's own kills don't blast again).</summary>
        void GambitBlast(Vector2 at)
        {
            if (!GambitActive || _inBlast) return;
            _inBlast = true;
            _blasts++;
            var rules = config.boosts;
            float damage = rules.blastDamage * config.player.shotDamage * _loadout.ShotDamageScale;
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (!e.Alive) continue;
                float reach = rules.blastRadius + e.Radius;
                if ((e.Position - at).sqrMagnitude <= reach * reach && e.Damage(damage)) KillEnemy(e);
            }
            if (boss.CanBeHurt && (boss.Position - at).magnitude <= rules.blastRadius + boss.Radius) boss.Damage(damage);
            if (fx != null) fx.Ring(at, GambitColor, 0.1f, rules.blastRadius, 0.25f);
            _inBlast = false;
        }

        /// <summary>Guardian Angel: when ready and the bay has a bomb, the hit fires a bomb that spends every bomb.</summary>
        bool TryGuardian(Vector2 pp)
        {
            if (_loadout.Effect(ShmupEffects.AutoBomb) <= 0) return false;
            int spent = _angel.TryTrigger(player.Bay, config.boosts);
            if (spent <= 0) return false;
            GameRoot.Juice.FloatingText(pp, spent > 1 ? "Guardian Angel! (" + spent + " bombs)" : "Guardian Angel!", new Color(1f, 0.85f, 0.4f));
            TriggerBomb(true, GuardianAngel.PowerFor(spent, config.boosts));
            return true;
        }

        /// <summary>The ability line under the SPRINT meter: Ghost Step's phases and Guardian Angel's cooldown.
        /// Only rebuilt when a shown number changes.</summary>
        void UpdateAbilityHud()
        {
            bool ghost = _loadout.Effect(ShmupEffects.GhostStep) > 0, angel = _loadout.Effect(ShmupEffects.AutoBomb) > 0;
            int phases = ghost ? _ghostPhase.PhasesLeft : -2;
            int ghostCd = ghost ? Mathf.CeilToInt(_ghostPhase.CooldownLeft) : -2;
            int angelCd = angel ? Mathf.CeilToInt(_angel.CooldownLeft) : -2;
            if (phases == _shownPhases && ghostCd == _shownGhostCd && angelCd == _shownAngelCd) return;
            _shownPhases = phases;
            _shownGhostCd = ghostCd;
            _shownAngelCd = angelCd;
            string text = "";
            if (ghost)
            {
                text = "Ghost ";
                int max = Mathf.Max(phases, config.boosts.ghostPhasesPerRoom);
                for (int i = 0; i < max; i++) text += i < phases ? "●" : "○";
                if (ghostCd > 0) text += "  " + ghostCd + " s";
            }
            if (angel) text += (text.Length > 0 ? "\n" : "") + (angelCd > 0 ? "Angel " + angelCd + " s" : "Angel ready");
            _hud.SetAbilities(text);
        }
    }
}
