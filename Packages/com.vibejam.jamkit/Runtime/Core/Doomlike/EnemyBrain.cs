using System;

namespace JamKit.Doomlike
{
    public enum EnemyState { Idle, Alert, Chase, Attack, Pain, Dead }

    /// <summary>The steps of one attack: a readable wind-up (the telegraph), the firing itself, a
    /// short recovery.</summary>
    public enum AttackPhase { None, Windup, Fire, Recover }

    /// <summary>How an enemy notices, chases and attacks: designer data, one per enemy type.
    /// Distances in metres, times in seconds, angles in degrees.</summary>
    [Serializable]
    public class EnemyBrainDef
    {
        public float sightRange = 28f;
        /// <summary>Field of view while idle (once alert it tracks you all round). 360 = eyes everywhere.</summary>
        public float fovDegrees = 200f;
        /// <summary>Gunfire within this distance wakes it (through walls).</summary>
        public float hearingRange = 22f;
        /// <summary>Delay between noticing you and moving (it turns and shouts first).</summary>
        public float reactionSeconds = 0.45f;

        public float attackRange = 16f;
        /// <summary>Won't start an attack closer than this (a mortar enemy backs off instead of firing).</summary>
        public float minAttackRange;
        /// <summary>Seconds after an attack before the next can start.</summary>
        public float attackCooldown = 1.6f;
        /// <summary>Chance per second to start an attack once ready, in range and in sight.</summary>
        public float aggression = 1.5f;
        /// <summary>The telegraph: how long the wind-up shows before the attack lands.</summary>
        public float windupSeconds = 0.45f;
        /// <summary>How long the firing lasts (a pattern runs for this long; hitscan and melee land
        /// on its first frame).</summary>
        public float fireSeconds = 0.1f;
        public float recoverSeconds = 0.35f;
        /// <summary>Stands still while attacking (most), or keeps walking (a strafing flyer).</summary>
        public bool stopToAttack = true;

        /// <summary>Chance that a damaging hit flinches it (interrupting a wind-up).</summary>
        public float painChance = 0.4f;
        public float painSeconds = 0.3f;

        /// <summary>Below this fraction of health it attacks more often and moves faster (bosses). 0 = never.</summary>
        public float enrageBelow;
        public float enrageSpeedScale = 1.3f;
        public float enrageCooldownScale = 0.6f;
    }

    /// <summary>What the Unity layer saw this frame, for the brain's decisions.</summary>
    public struct BrainSenses
    {
        /// <summary>Clear line of sight to the player.</summary>
        public bool canSee;
        public float distance;
        /// <summary>Angle between its facing and the player, in radians (0 = straight ahead).</summary>
        public float angleOffFacing;
        /// <summary>The player's gunfire reached it this frame.</summary>
        public bool heardNoise;
    }

    /// <summary>
    /// An enemy's decisions as a core <see cref="StateMachine{T}"/>: Idle (sees you in its field of
    /// view or hears gunfire) -> Alert (turns, shouts, waits its reaction time) -> Chase (walk to you;
    /// when in range, in sight and ready, roll to attack) -> Attack (wind-up, fire, recover) -> Chase.
    /// Damage can flinch it into Pain (cancelling a wind-up). Pure rules: the Unity layer feeds senses
    /// and acts on <see cref="WantsToMove"/>, <see cref="FacesTarget"/> and the events.
    /// </summary>
    public sealed class EnemyBrain
    {
        readonly EnemyBrainDef _def;
        readonly StateMachine<EnemyState> _sm = new StateMachine<EnemyState>(EnemyState.Idle);
        AttackPhase _phase;
        float _phaseTime;
        float _cooldown;
        float _painLock;
        bool _enraged;
        float _health01 = 1f;
        int _attackIndex;

        /// <summary>It noticed the player (the shout).</summary>
        public event Action Alerted;
        /// <summary>A wind-up began; the int says which attack (0 primary, 1 secondary).</summary>
        public event Action<int> AttackStarted;
        /// <summary>The attack lands now (hitscan and melee resolve here; a pattern starts).</summary>
        public event Action<int> AttackFired;
        public event Action AttackEnded;
        public event Action PainStarted;

        /// <summary>Chance (0..1) each attack uses the secondary attack instead; 0 = only the primary.</summary>
        public float secondaryChance;

        public EnemyBrain(EnemyBrainDef def)
        {
            _def = def ?? new EnemyBrainDef();
            _sm.OnEnter(EnemyState.Alert, () => Alerted?.Invoke());
            _sm.OnEnter(EnemyState.Pain, () => PainStarted?.Invoke());
        }

        public EnemyBrainDef Def => _def;
        public EnemyState State => _sm.Current;
        public AttackPhase Phase => _phase;
        public float TimeInState => _sm.TimeInState;
        public float PhaseTime => _phaseTime;
        public bool IsDead => _sm.Current == EnemyState.Dead;
        public bool IsAwake => _sm.Current != EnemyState.Idle && _sm.Current != EnemyState.Dead;
        public bool Enraged => _enraged;
        public int CurrentAttack => _attackIndex;
        public float CooldownLeft => _cooldown;

        /// <summary>Walk toward the player this frame.</summary>
        public bool WantsToMove =>
            _sm.Current == EnemyState.Chase || (_sm.Current == EnemyState.Attack && !_def.stopToAttack);

        /// <summary>Turn to face the player (alert, attacking, flinching), rather than where it walks.</summary>
        public bool FacesTarget =>
            _sm.Current == EnemyState.Alert || _sm.Current == EnemyState.Attack || _sm.Current == EnemyState.Pain;

        public float SpeedScale => _enraged ? _def.enrageSpeedScale : 1f;

        /// <summary>Wakes it (a trigger, an ally's alarm). From idle it goes through Alert first.</summary>
        public void Alert()
        {
            if (_sm.Current == EnemyState.Idle) _sm.TryGo(EnemyState.Alert);
        }

        /// <summary>It was hurt: wakes at once (no reaction delay), maybe flinches, tracks enrage.</summary>
        public void Damaged(float health01, Rng rng)
        {
            if (IsDead) return;
            _health01 = health01;
            if (!_enraged && _def.enrageBelow > 0f && health01 <= _def.enrageBelow) _enraged = true;
            if (_sm.Current == EnemyState.Idle || _sm.Current == EnemyState.Alert) _sm.TryGo(EnemyState.Chase);
            if (_painLock <= 0f && _def.painChance > 0f && rng != null && rng.Chance(_def.painChance))
            {
                EndAttack(false);
                _sm.TryGo(EnemyState.Pain);
                _painLock = Math.Max(_def.painSeconds * 2f, 0.2f);
            }
        }

        public void Kill()
        {
            EndAttack(false);
            _sm.TryGo(EnemyState.Dead);
        }

        public void Tick(float dt, BrainSenses senses, Rng rng)
        {
            if (!(dt > 0f)) dt = 0f;
            if (_cooldown > 0f) _cooldown = Math.Max(0f, _cooldown - dt);
            if (_painLock > 0f) _painLock = Math.Max(0f, _painLock - dt);
            _sm.Tick(dt);
            switch (_sm.Current)
            {
                case EnemyState.Idle:
                    if (senses.heardNoise && senses.distance <= _def.hearingRange) { _sm.TryGo(EnemyState.Alert); break; }
                    if (senses.canSee && senses.distance <= _def.sightRange
                        && Math.Abs(senses.angleOffFacing) <= _def.fovDegrees * 0.5f * GridMath.Deg)
                        _sm.TryGo(EnemyState.Alert);
                    break;
                case EnemyState.Alert:
                    if (_sm.TimeInState >= _def.reactionSeconds)
                    {
                        _sm.TryGo(EnemyState.Chase);
                        // A short grace before the first shot, so waking up isn't an instant hit.
                        _cooldown = Math.Max(_cooldown, 0.25f);
                    }
                    break;
                case EnemyState.Chase:
                    TryStartAttack(dt, senses, rng);
                    break;
                case EnemyState.Attack:
                    TickAttack(dt);
                    break;
                case EnemyState.Pain:
                    if (_sm.TimeInState >= _def.painSeconds) _sm.TryGo(EnemyState.Chase);
                    break;
            }
        }

        void TryStartAttack(float dt, BrainSenses senses, Rng rng)
        {
            if (_cooldown > 0f || !senses.canSee) return;
            if (senses.distance > _def.attackRange || senses.distance < _def.minAttackRange) return;
            float rate = Math.Max(0f, _def.aggression) * (_enraged ? 1.5f : 1f);
            float chance = 1f - (float)Math.Exp(-rate * dt);
            if (rng != null && !rng.Chance(chance)) return;
            _attackIndex = secondaryChance > 0f && rng != null && rng.Chance(secondaryChance) ? 1 : 0;
            _sm.TryGo(EnemyState.Attack);
            _phase = AttackPhase.Windup;
            _phaseTime = 0f;
            AttackStarted?.Invoke(_attackIndex);
            if (_def.windupSeconds <= 0f) TickAttack(0f);
        }

        void TickAttack(float dt)
        {
            _phaseTime += dt;
            switch (_phase)
            {
                case AttackPhase.Windup:
                    if (_phaseTime < _def.windupSeconds) return;
                    _phase = AttackPhase.Fire;
                    _phaseTime = 0f;
                    AttackFired?.Invoke(_attackIndex);
                    return;
                case AttackPhase.Fire:
                    if (_phaseTime < _def.fireSeconds) return;
                    _phase = AttackPhase.Recover;
                    _phaseTime = 0f;
                    return;
                case AttackPhase.Recover:
                    if (_phaseTime < _def.recoverSeconds) return;
                    EndAttack(true);
                    _sm.TryGo(EnemyState.Chase);
                    return;
            }
        }

        void EndAttack(bool completed)
        {
            if (_phase == AttackPhase.None) return;
            _phase = AttackPhase.None;
            _phaseTime = 0f;
            float cd = _def.attackCooldown * (_enraged ? _def.enrageCooldownScale : 1f);
            _cooldown = completed ? cd : Math.Max(_cooldown, cd * 0.5f);
            AttackEnded?.Invoke();
        }

        /// <summary>The fraction of health it last reported (for enrage tests and boss bars).</summary>
        public float Health01 => _health01;
    }
}
