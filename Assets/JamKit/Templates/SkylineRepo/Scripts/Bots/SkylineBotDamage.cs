using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Damage states, every bot (the creator, 2026-10-14, docs/SKYLINE-BOTS.md): "I want to see model state changes when taking damage
    /// changes health thresholds, for example a hit that takes a drone from above 90% to less than 30% of its health should show that
    /// damage in the model, with an animation that shows that damage happening."
    /// <para>The pure thresholds are <see cref="DamageStates"/> (stage 1 at 70% of the chassis, stage 2 at 30%). Crossing one plays the
    /// damage happening: a hard burst of sparks, torn panel shards thrown off, the panel-pop sound and a shake, then the look stays:
    /// scorched patches and dangling wires, sparks (and, from stage 2, smoke) now and then, a limp in the pose (a roll, a droop), the
    /// drone's rotor ducts bending, slower rotors and brushes, the face glitching to its hurt screen now and then. One hit that crosses
    /// both stages plays the two back to back, <see cref="StageGap"/> seconds apart. Separate from the Writ's spots (the cracked casing
    /// with yellow outlines), which stack with it, and from the plates, which shed on their own.</para>
    /// Everything here is cosmetic; the pure stage logic is tested headless, and where the damage sits on each body is a Unity
    /// check (gallery stills at each stage: <see cref="SkylineDisplayBot.damageStage"/>).
    /// </summary>
    public sealed partial class SkylineBot
    {
        /// <summary>Seconds between two stages played back to back.</summary>
        public const float StageGap = 0.3f;

        readonly DamageStates _damageStates = new DamageStates();
        readonly List<int> _crossedStages = new List<int>();
        readonly List<int> _pendingStages = new List<int>();
        readonly List<GameObject> _damageProps = new List<GameObject>();
        int _lookStage;
        float _nextStageAt, _sparkAt, _smokeAt, _glitchAt, _glitchUntil, _damageSpin = 1f, _damagePhase, _bendL, _bendR, _bendLTarget, _bendRTarget;
        Transform _ductL, _ductR;
        bool _ductsFound;

        /// <summary>The stage the bot looks like now (0 undamaged).</summary>
        public int DamageStage => _lookStage;

        /// <summary>The face shows its hurt screen for a moment (a glitching bot).</summary>
        bool DamageGlitch => _lookStage > 0 && _clock < _glitchUntil;

        /// <summary>Where each stage's damage sits on each body, in the rig's own (unscaled) space: the drone's rotor ducts, the
        /// floor-care unit's lid and flank, the spotlight's housings, the gondola's buckets.</summary>
        Vector3 DamagePoint(int stage)
        {
            bool one = stage <= 1;
            if (Def.layer == ThreatLayer.Walls) return one ? new Vector3(-0.8f, -0.3f, 0.3f) : new Vector3(0.8f, -0.3f, 0.3f);
            if (Def.moveSpeed > 0f) return one ? new Vector3(0.22f, 0.38f, 0.1f) : new Vector3(0.3f, 0.3f, -0.32f);
            if (Beam != null) return one ? new Vector3(0.44f, 0.1f, 0.1f) : new Vector3(-0.3f, 0.36f, -0.1f);
            return one ? new Vector3(-0.5f, 0.12f, 0.05f) : new Vector3(0.5f, 0.12f, 0.05f);
        }

        /// <summary>Called after a hit has landed: notes the new health and queues each stage it crossed.</summary>
        void OnDamageHealth()
        {
            if (Body == null || Parts == null || SkylinePerfSwitches.NoDamageFx) return;   // -skyline-no-damage-fx: a perf A/B, no stage ever starts
            _crossedStages.Clear();
            _damageStates.Observe(DamageStates.Integrity(Body), _crossedStages);
            for (int i = 0; i < _crossedStages.Count; i++) _pendingStages.Add(_crossedStages[i]);
            if (_pendingStages.Count > 0 && _nextStageAt < _clock) _nextStageAt = _clock;
        }

        /// <summary>Sets the bot to look as it does at <paramref name="stage"/> at once, with no animation (the gallery's stills, a
        /// restart). 0 clears the damage.</summary>
        public void ForceDamageStage(int stage)
        {
            ClearDamageProps();
            _pendingStages.Clear();
            _lookStage = 0;
            _damageStates.Reset();
            stage = Mathf.Clamp(stage, 0, _damageStates.Count);
            for (int s = 1; s <= stage; s++) EnterStage(s, false);
            _damageStates.Observe(stage == 0 ? 1f : stage == 1 ? 0.5f : 0.1f, _crossedStages);
            _crossedStages.Clear();
        }

        void ResetDamage()
        {
            ForceDamageStage(0);
            _damageSpin = 1f;
        }

        void ClearDamageProps()
        {
            for (int i = 0; i < _damageProps.Count; i++) if (_damageProps[i] != null) Destroy(_damageProps[i]);
            _damageProps.Clear();
            _bendLTarget = _bendRTarget = 0f;
            _damageSpin = 1f;
            if (!_ductsFound) FindDucts();
            _bendL = _bendR = 0f;
            if (_ductL != null) _ductL.localRotation = Quaternion.identity;
            if (_ductR != null) _ductR.localRotation = Quaternion.identity;
        }

        void TickDamage(float dt)
        {
            if (Parts == null || Body == null) return;
            // each queued stage plays its animation, one after another
            if (_pendingStages.Count > 0 && _clock >= _nextStageAt)
            {
                int s = _pendingStages[0];
                _pendingStages.RemoveAt(0);
                _nextStageAt = _clock + StageGap;
                EnterStage(s, true);
            }
            if (_lookStage <= 0) return;
            _damagePhase += dt;
            bool two = _lookStage >= 2;
            // sparks now and then, smoke from stage 2, a glitching face
            if (_clock >= _sparkAt)
            {
                _sparkAt = _clock + (two ? _rng.Range(0.7f, 1.4f) : _rng.Range(1.6f, 3f));
                Vector3 at = Rig.TransformPoint(DamagePoint(_rng.Range(0, 2) == 0 ? 1 : Mathf.Max(1, _lookStage)));
                SkylineSpark.Burst(at, Rig.up + Rig.right * _rng.Range(-1f, 1f), two ? 5 : 3, 2.5f);
            }
            if (two && _clock >= _smokeAt)
            {
                _smokeAt = _clock + _rng.Range(0.3f, 0.5f);
                SkylineSmokePuff.Emit(Rig.TransformPoint(DamagePoint(2)), 0.09f * Def.bodyScale);
            }
            if (_clock >= _glitchAt)
            {
                _glitchAt = _clock + (two ? _rng.Range(1.2f, 2.2f) : _rng.Range(2.5f, 4.5f));
                _glitchUntil = _clock + 0.35f;
            }
            // the drone's rotor ducts bend (a smooth move to the stage's angle)
            if (!_ductsFound) FindDucts();
            _bendL = Mathf.MoveTowards(_bendL, _bendLTarget, 70f * dt);
            _bendR = Mathf.MoveTowards(_bendR, _bendRTarget, 70f * dt);
            if (_ductL != null) _ductL.localRotation = Quaternion.Euler(0f, 0f, _bendL);
            if (_ductR != null) _ductR.localRotation = Quaternion.Euler(0f, 0f, _bendR);
        }

        void FindDucts()
        {
            _ductsFound = true;
            foreach (var w in Parts.weakParts)
            {
                if (w.name == "DuctL") _ductL = w;
                else if (w.name == "DuctR") _ductR = w;
            }
        }

        /// <summary>One stage reached. With <paramref name="animate"/> it plays the damage happening; either way the stage's look is set.</summary>
        void EnterStage(int stage, bool animate)
        {
            _lookStage = Mathf.Max(_lookStage, stage);
            Vector3 point = DamagePoint(stage);
            Vector3 world = Rig.TransformPoint(point);
            Vector3 outward = (world - Center);
            if (outward.sqrMagnitude < 1e-4f) outward = Rig.up;
            outward.Normalize();
            if (animate)
            {
                SkylineSpark.Burst(world, outward, stage >= 2 ? 22 : 16, stage >= 2 ? 6.5f : 5f);
                // a panel torn off: a few body-coloured shards thrown out where it happened
                float size = 0.06f * Def.bodyScale;
                var gold = SkylineBotLook.Lit(SkylineBotLook.Gold);
                var white = SkylineBotLook.Lit(SkylineBotLook.White);
                var dark = SkylineBotLook.Lit(SkylineBotLook.Dark);
                int shards = stage >= 2 ? 4 : 3;
                for (int i = 0; i < shards; i++) Shard(world, i % 3 == 0 ? gold : i % 3 == 1 ? white : dark, size * _rng.Range(0.8f, 1.4f));
                Sound("plate_shed");
                _shake = Mathf.Min(1f, _shake + 0.6f);
                _kick += outward * 0.5f;
                _glitchUntil = _clock + 0.6f;
                _sparkAt = _clock + 0.5f;
            }
            // what stays: scorching and wires at the damage, the drone's ducts bent, the rotors and brushes slower
            AddDamageProps(point, stage);
            _damageSpin = stage >= 2 ? 0.4f : 0.7f;
            if (Def.id == SkylineEnemies.DroneId)
            {
                if (stage == 1) _bendLTarget = 14f;
                else _bendRTarget = -22f;
            }
        }

        /// <summary>A scorched patch and two dangling wires (exposed wiring) at <paramref name="point"/> in the rig's space.</summary>
        void AddDamageProps(Vector3 point, int stage)
        {
            var rig = Rig;
            var scorch = SkylineBotLook.Visual(PrimitiveType.Sphere, "DamageScorch" + stage, rig, SkylineBotLook.Lit(SkylineBotLook.Scorched), false);
            scorch.transform.localPosition = point;
            scorch.transform.localScale = new Vector3(0.2f, 0.07f, 0.2f);
            scorch.transform.localRotation = Quaternion.Euler(_rng.Range(0f, 360f), _rng.Range(0f, 360f), 0f);
            _damageProps.Add(scorch);
            Color[] wire = { new Color(0.9f, 0.45f, 0.1f), new Color(0.25f, 0.5f, 0.95f) };
            for (int i = 0; i < 2; i++)
            {
                var w = SkylineBotLook.Visual(PrimitiveType.Cylinder, "DamageWire" + stage + "_" + i, rig, SkylineBotLook.Lit(wire[i]), false);
                w.transform.localPosition = point + new Vector3(_rng.Range(-0.05f, 0.05f), -0.07f, _rng.Range(-0.05f, 0.05f));
                w.transform.localScale = new Vector3(0.015f, 0.08f, 0.015f);
                w.transform.localRotation = Quaternion.Euler(_rng.Range(-25f, 25f), 0f, _rng.Range(-25f, 25f));
                _damageProps.Add(w);
            }
        }

        /// <summary>The limp the damage puts in the pose, applied on top of the pose each frame: the drone rolls and lists, the
        /// floor-care unit shimmies, the spotlight droops and twitches, the gondola sags to one side.</summary>
        Quaternion DamageTilt()
        {
            bool two = _lookStage >= 2;
            float t = _damagePhase;
            if (Def.layer == ThreatLayer.Walls) return Quaternion.Euler(0f, 0f, two ? 5f : 2f);
            if (Def.moveSpeed > 0f)
                return Quaternion.Euler((two ? 3.5f : 1.5f) * Mathf.Sin(t * 9f), 0f, (two ? 3.5f : 0f) * Mathf.Sin(t * 6.5f + 1f));
            if (Beam != null)
            {
                float droop = two ? 10f : 4f;
                float twitch = two ? 2.5f * Mathf.Sin(t * 23f) * Mathf.Max(0f, Mathf.Sin(t * 1.7f)) : 0f;
                return Quaternion.Euler(droop + twitch, twitch * 0.5f, 0f);
            }
            float roll = (two ? 6f : 2.5f) * Mathf.Sin(t * (two ? 8.2f : 5f));
            return Quaternion.Euler(0f, 0f, roll + (two ? -4f : 0f));
        }
    }

    /// <summary>A puff of smoke from a badly damaged bot: a small dark sphere that rises, swells and shrinks away over about a second.
    /// Pooled (a spent puff is reused), so a fight with several smoking bots makes no garbage.</summary>
    public sealed class SkylineSmokePuff : MonoBehaviour
    {
        const int PoolSize = 48;
        const float Life = 1f;
        static readonly List<SkylineSmokePuff> _pool = new List<SkylineSmokePuff>();
        static int _next;

        float _age, _size;
        Vector3 _drift;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _pool.Clear();
            _next = 0;
        }

        public static void Emit(Vector3 at, float size)
        {
            for (int i = _pool.Count - 1; i >= 0; i--) if (_pool[i] == null) _pool.RemoveAt(i);
            SkylineSmokePuff p = null;
            for (int i = 0; i < _pool.Count; i++) if (!_pool[i].gameObject.activeSelf) { p = _pool[i]; break; }
            if (p == null)
            {
                if (_pool.Count < PoolSize)
                {
                    var go = SkylineBotLook.Visual(PrimitiveType.Sphere, "Smoke", null, SkylineBotLook.Lit(new Color(0.2f, 0.2f, 0.21f)), false);
                    p = go.AddComponent<SkylineSmokePuff>();
                    _pool.Add(p);
                }
                else
                {
                    _next = (_next + 1) % _pool.Count;   // the pool is full: the oldest slot is taken over
                    p = _pool[_next];
                }
            }
            p._age = 0f;
            p._size = Mathf.Max(0.02f, size);
            p._drift = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.6f, 1f), Random.Range(-0.2f, 0.2f));
            p.transform.position = at;
            p.transform.localScale = Vector3.one * p._size * 0.5f;
            p.gameObject.SetActive(true);
        }

        void Update()
        {
            _age += Time.deltaTime;
            float k = _age / Life;
            if (k >= 1f) { gameObject.SetActive(false); return; }
            transform.position += _drift * Time.deltaTime;
            // swells over the first half, then shrinks away
            float s = k < 0.5f ? Mathf.Lerp(0.5f, 1.6f, k * 2f) : Mathf.Lerp(1.6f, 0f, (k - 0.5f) * 2f);
            transform.localScale = Vector3.one * _size * s;
        }
    }
}
