using System;

namespace JamKit.BulletHell
{
    /// <summary>
    /// Sprint tuning (PlayerDef > Sprint). Seconds are seconds of sprinting. Upgrades scale these through
    /// the stats sprint_speed, stamina_max, stamina_regen and room_sprint_budget (all base 1).
    /// </summary>
    [Serializable]
    public class SprintDef
    {
        /// <summary>Move speed while sprinting, times the normal speed.</summary>
        public float speedMultiplier = 1.6f;
        /// <summary>Seconds of sprint a full stamina bar holds.</summary>
        public float staminaSeconds = 1.5f;
        /// <summary>Seconds after letting go (or running dry) before stamina starts to come back.</summary>
        public float regenDelay = 0.4f;
        /// <summary>Seconds to refill an empty bar, once it's recharging.</summary>
        public float refillSeconds = 2f;
        /// <summary>Total seconds of sprint per fight room (combat, elite, boss); refilled when the next one starts.</summary>
        public float roomBudgetSeconds = 8f;
        /// <summary>After the bar runs dry, sprint comes back only once it has refilled this fraction (no stutter at 0).</summary>
        public float restartFraction = 0.25f;
    }

    /// <summary>
    /// The sprint meter: short-term stamina (drains while sprinting, recharges after a delay) inside a
    /// per-room fuel tank (the room budget) that only refills when a new fight room starts, so a player
    /// can't sprint forever or gain anything by dragging a fight out. Pure, frame-rate independent:
    /// when stamina or the budget runs out part-way through a frame, only that part counts as sprint
    /// (<see cref="Tick"/> returns the fraction of the frame spent sprinting).
    /// <code>
    /// meter.Configure(def.sprint, staminaScale, regenScale, budgetScale);
    /// meter.RefillRoom();                                    // a fight room starts
    /// float k = meter.Tick(dt, sprintHeld, moving);          // 0..1 of dt was sprint
    /// speed = moveSpeed * (1 + (sprintMultiplier - 1) * k);
    /// </code>
    /// </summary>
    public sealed class SprintMeter
    {
        float _staminaMax = 1.5f, _regenDelay = 0.4f, _regenPerSecond = 0.75f, _budgetMax = 8f, _restart = 0.25f;
        float _stamina, _budget, _sinceSprint;
        bool _sprinting, _exhausted, _staminaEmptied, _budgetEmptied;

        public SprintMeter() { Fill(); }

        /// <summary>Seconds of sprint the bar holds when full.</summary>
        public float StaminaMax => _staminaMax;
        public float RoomBudgetMax => _budgetMax;
        /// <summary>Seconds of sprint left in the bar.</summary>
        public float Stamina => _stamina;
        /// <summary>Seconds of sprint left for this room.</summary>
        public float RoomBudget => _budget;
        public float StaminaFraction => _staminaMax > 0f ? Clamp01(_stamina / _staminaMax) : 0f;
        public float BudgetFraction => _budgetMax > 0f ? Clamp01(_budget / _budgetMax) : 0f;
        /// <summary>Sprinting during the last tick.</summary>
        public bool IsSprinting => _sprinting;
        /// <summary>The bar ran dry and hasn't refilled to the restart fraction yet.</summary>
        public bool Exhausted => _exhausted;
        public bool BudgetEmpty => _budget <= 0f;
        /// <summary>A sprint would start now if asked.</summary>
        public bool CanSprint => !_exhausted && _stamina > 0f && _budget > 0f;
        /// <summary>The bar ran dry during the last tick (flash the meter, play a sound).</summary>
        public bool StaminaEmptiedThisTick => _staminaEmptied;
        /// <summary>The room's budget ran out during the last tick.</summary>
        public bool BudgetEmptiedThisTick => _budgetEmptied;

        /// <summary>Sets the tuning (scales come from upgrades; 1 = as in the data). Keeps the current amounts, clamped.</summary>
        public void Configure(SprintDef def, float staminaScale = 1f, float regenScale = 1f, float budgetScale = 1f)
        {
            if (def == null) def = new SprintDef();
            _staminaMax = Math.Max(0.05f, Safe(def.staminaSeconds, 1.5f) * Math.Max(0.05f, Safe(staminaScale, 1f)));
            _regenDelay = Math.Max(0f, Safe(def.regenDelay, 0.4f));
            float refill = Math.Max(0.05f, Safe(def.refillSeconds, 2f));
            _regenPerSecond = _staminaMax / refill * Math.Max(0.05f, Safe(regenScale, 1f));
            _budgetMax = Math.Max(0f, Safe(def.roomBudgetSeconds, 8f) * Math.Max(0f, Safe(budgetScale, 1f)));
            _restart = Clamp01(Safe(def.restartFraction, 0.25f));
            if (_stamina > _staminaMax) _stamina = _staminaMax;
            if (_budget > _budgetMax) _budget = _budgetMax;
        }

        /// <summary>Full bar and full room budget (a new run).</summary>
        public void Fill()
        {
            _stamina = _staminaMax;
            _budget = _budgetMax;
            _sinceSprint = _regenDelay;
            _sprinting = _exhausted = _staminaEmptied = _budgetEmptied = false;
        }

        /// <summary>A fight room starts: the room budget (and the bar) are full again.</summary>
        public void RefillRoom() => Fill();

        /// <summary>
        /// Advance by <paramref name="dt"/> seconds. <paramref name="wantSprint"/>: the sprint button is held;
        /// <paramref name="moving"/>: the player is steering (standing still never spends stamina).
        /// Returns the fraction of <paramref name="dt"/> spent sprinting (0..1).
        /// </summary>
        public float Tick(float dt, bool wantSprint, bool moving)
        {
            _staminaEmptied = _budgetEmptied = false;
            if (!(dt > 0f) || float.IsInfinity(dt))
            {
                _sprinting = false;
                return 0f;
            }
            float used = 0f;
            if (wantSprint && moving && CanSprint)
            {
                used = Math.Min(dt, Math.Min(_stamina, _budget));
                _stamina -= used;
                _budget -= used;
                if (_stamina <= 1e-6f)
                {
                    _stamina = 0f;
                    _exhausted = true;
                    _staminaEmptied = true;
                }
                if (_budget <= 1e-6f)
                {
                    if (_budget < 0f || used > 0f) _budgetEmptied = true;
                    _budget = 0f;
                }
                _sinceSprint = 0f;
            }
            _sprinting = used > 0f;

            // The rest of the frame recharges, after the delay since the last sprint.
            float rest = dt - used;
            if (rest > 0f && (!wantSprint || !moving || !CanSprint || _exhausted))
            {
                float before = _sinceSprint;
                _sinceSprint += rest;
                float charging = Math.Min(rest, Math.Max(0f, _sinceSprint - Math.Max(before, _regenDelay)));
                if (_sinceSprint > _regenDelay && charging > 0f)
                {
                    _stamina = Math.Min(_staminaMax, _stamina + _regenPerSecond * charging);
                    if (_exhausted && _stamina >= _staminaMax * _restart - 1e-6f) _exhausted = false;
                }
            }
            return used / dt;
        }

        static float Safe(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : v;
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
