using System.Collections.Generic;
using JamKit.Dialogue;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The companion in battle (docs/DESIGN-ROGUELITE.md, section 6), from BulletHellConfig.companion
    /// (a CompanionDef; off with companionEnabled): it orbits the ship and shoots at the nearest enemy
    /// (weaker than the ship), takes one bullet meant for the player on a cooldown (its ring and the
    /// GUARD meter show when it's ready), and barks short lines from ink without stopping the game:
    /// the boss's next pattern a moment before it starts (companion_bark_&lt;boss&gt;_&lt;shape&gt;,
    /// companion_bark_&lt;shape&gt;), a guard (companion_bark_guard) and the last life (companion_bark_low_life).
    /// Its conversations between battles are the Rest room's line (rest), which can read companion_guards().
    /// </summary>
    public sealed partial class BulletHellGame
    {
        /// <summary>Upgrades with this tag target the companion; they leave the pool when it's off.</summary>
        public const string CompanionTag = "companion";

        readonly CompanionState _companion = new CompanionState();
        SpriteRenderer _companionBody, _companionRing;
        int _companionShots, _calloutPhase = -1;
        ulong _calledSteps;
        readonly List<string> _barksPlayed = new List<string>();

        public bool CompanionOn => config != null && config.companionEnabled && config.companion != null;
        /// <summary>On, and it has joined the run (CompanionRules.joinAfterBosses: 0 = from the start).</summary>
        public bool CompanionActive => CompanionOn && _bossesBeaten >= Mathf.Max(0, config.companion.rules.joinAfterBosses);
        public CompanionState Companion => _companion;
        /// <summary>Shots the companion fired this run.</summary>
        public int CompanionShots => _companionShots;
        /// <summary>Every bark knot shown this run, in order.</summary>
        public IReadOnlyList<string> BarksPlayed => _barksPlayed;
        string CompanionName => config != null && config.companion != null ? config.companion.displayName : "Companion";

        void CreateCompanion()
        {
            if (!CompanionOn) return;
            var def = config.companion;
            var go = new GameObject("Companion");
            go.transform.SetParent(transform, false);
            _companionBody = go.AddComponent<SpriteRenderer>();
            _companionBody.sprite = def.sprite;
            _companionBody.color = def.color;
            if (spriteMaterial != null) _companionBody.sharedMaterial = spriteMaterial;
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, def.size);
            var ring = new GameObject("Guard ring");
            ring.transform.SetParent(go.transform, false);
            _companionRing = ring.AddComponent<SpriteRenderer>();
            _companionRing.sprite = player.grazeRing != null ? player.grazeRing.sprite : def.sprite;
            if (spriteMaterial != null) _companionRing.sharedMaterial = spriteMaterial;
            if (player.body != null)
            {
                _companionBody.sortingLayerID = player.body.sortingLayerID;
                _companionBody.sortingOrder = player.body.sortingOrder;
                _companionRing.sortingLayerID = player.body.sortingLayerID;
                _companionRing.sortingOrder = player.body.sortingOrder - 1;
            }
            go.SetActive(false);
        }

        void BeginCompanion()
        {
            _companionShots = 0;
            _calloutPhase = -1;
            _calledSteps = 0;
            _barksPlayed.Clear();
            _hud.HideBark();
            if (!CompanionOn) return;
            _companion.Begin(config.companion.rules);
            if (_companionBody != null) _companionBody.gameObject.SetActive(true);
        }

        /// <summary>Orbit, shoot, show the guard, call out the boss's patterns. <paramref name="targets"/>
        /// is how many entries of _targets are filled this frame.</summary>
        void TickCompanion(float dt, Vector2 pp, int targets)
        {
            if (!CompanionOn || _companionBody == null) return;
            bool active = CompanionActive;
            if (_companionBody.gameObject.activeSelf != active) _companionBody.gameObject.SetActive(active);
            if (!active) return;
            var rules = config.companion.rules;
            bool fighting = (_phase == RoomPhase.Waves || _phase == RoomPhase.Boss) && player.ControlsEnabled;
            int shots = _companion.Tick(dt, fighting && targets > 0, _loadout.CompanionCooldownScale);
            var off = _companion.Offset;
            Vector2 at = pp + new Vector2(off.X, off.Y);
            _companionBody.transform.position = new Vector3(at.x, at.y, player.transform.position.z - 0.05f);

            // With its own health, enemy bullets that touch it hurt it; at 0 it's down until it revives.
            if (_companion.HasHealth && !_companion.Down && fighting)
            {
                int hitIndex = field.Enemy.FirstOverlap(new System.Numerics.Vector2(at.x, at.y), rules.hitRadius);
                if (hitIndex >= 0)
                {
                    field.Enemy.RemoveAt(hitIndex);
                    if (_companion.Hurt(1f))
                    {
                        GameRoot.Juice.FloatingText(at + Vector2.up * 0.4f, CompanionName + " is down!", new Color(1f, 0.5f, 0.4f));
                        Bark("down", null);
                    }
                }
            }

            // The guard ring: bright and full when ready, a growing dim ring while it recharges.
            float f = _companion.GuardFraction;
            bool ready = _companion.GuardReady;
            var c = config.companion.color;
            _companionBody.color = _companion.Down ? new Color(c.r * 0.4f, c.g * 0.4f, c.b * 0.4f, 0.5f) : c;
            if (_companion.Down) { f = 0f; ready = false; }
            _companionRing.color = new Color(c.r, c.g, c.b, ready ? 0.85f : 0.15f + 0.35f * f);
            _companionRing.transform.localScale = Vector3.one * (ready ? 2.1f : 1.2f + 0.7f * f);

            if (shots > 0)
            {
                float angle = Mathf.PI * 0.5f, best = float.MaxValue;
                for (int i = 0; i < targets; i++)
                {
                    if (!_targets[i].active) continue;
                    var tp = _targets[i].position;
                    float d = (tp.X - at.x) * (tp.X - at.x) + (tp.Y - at.y) * (tp.Y - at.y);
                    if (d < best) { best = d; angle = Mathf.Atan2(tp.Y - at.y, tp.X - at.x); }
                }
                float damage = config.player.shotDamage * rules.damageScale * _loadout.CompanionDamageScale;
                for (int s = 0; s < shots; s++)
                    if (field.FirePlayer(at, angle, rules.shotSpeed, damage, config.companion.shotStyle, 1.6f, 0, false, 0f)) _companionShots++;
            }
            if (_phase == RoomPhase.Boss) TickBossCallout();
        }

        /// <summary>A bullet would hit the player: the companion takes it if its guard is ready.</summary>
        bool TryCompanionGuard(Vector2 pp)
        {
            if (!CompanionActive || !_companion.TryGuard()) return false;
            field.Enemy.ClearInCircle(new System.Numerics.Vector2(pp.x, pp.y), 0.9f);
            player.Health.GrantInvulnerability(0.6f);
            var c = config.companion.color;
            if (fx != null) fx.Ring(pp, new Color(c.r, c.g, c.b, 0.9f), 0.2f, 1.6f, 0.35f);
            GameRoot.Juice.FloatingText(pp + Vector2.up * 0.6f, "Guarded!", c);
            GameRoot.Audio.Play("graze", 1f);
            Bark("guard", null);
            return true;
        }

        /// <summary>Each pattern step of a boss phase is called out once, a moment before it starts.</summary>
        void TickBossCallout()
        {
            if (boss.Current != ShmupBoss.State.Fighting || !_companion.CanBark || boss.Def == null) return;
            var seq = boss.CurrentAttack;
            if (seq == null) return;
            if (boss.Phase != _calloutPhase)
            {
                _calloutPhase = boss.Phase;
                _calledSteps = 0;
            }
            int step = PatternCallout.Upcoming(seq, boss.EmitterTime, config.companion.rules.barkLead);
            if (step < 0 || step >= 64 || (_calledSteps & (1UL << step)) != 0) return;
            _calledSteps |= 1UL << step;
            var pattern = seq.steps[step].pattern;
            if (pattern != null) Bark(PatternCallout.ShapeWord(pattern.shape), boss.Def.StoryId);
        }

        /// <summary>Shows companion_bark_&lt;boss&gt;_&lt;what&gt; or companion_bark_&lt;what&gt; (its first line) low on
        /// the screen, without stopping the game. False when it's off, cooling down or nothing is written.</summary>
        bool Bark(string what, string bossId)
        {
            if (!CompanionActive || _hookProbe == null || !_companion.CanBark) return false;
            string knot = StoryHooks.Resolve(StoryHooks.CompanionBark(what, bossId), _hasKnot);
            if (knot == null) return false;
            if (!GameRoot.Dialogue.TryReadLine(config.events.storyJson, knot, out string speaker, out string line, _bindInk)) return false;
            _hud.Bark(string.IsNullOrEmpty(speaker) ? CompanionName : speaker, line, config.companion.rules.barkSeconds);
            _companion.Barked();
            _barksPlayed.Add(knot);
            return true;
        }
    }
}
