using System.Collections.Generic;
using JamKit.Movement;
using UnityEngine;

namespace JamKit.Stealth
{
    /// <summary>
    /// The player's side of the answers to fast movement (docs/STEALTH.md "When the movement outruns the guards"), from
    /// <see cref="StealthRules.Shared"/>:
    /// <list type="bullet">
    /// <item><b>C1, daytime:</b> the movement's daytime profile for the section (no dream powers: Nadia is just a nurse
    /// sneaking through St. Brigid's).</item>
    /// <item><b>C2, being watched dims her lucidity:</b> near a guard, or farther from a wary or alert one, her dream
    /// powers fade (core <see cref="LucidityFade"/>: no double jump, wallrun or slide, a 6.5 m/s cap); away from them
    /// they come back. <see cref="Lucidity"/> drives the HUD's bar and the screen's dimming.</item>
    /// </list>
    /// Runs after the seekers. No allocation per frame.
    /// </summary>
    [DefaultExecutionOrder(65)]
    public sealed class StealthPlayerRules : MonoBehaviour
    {
        public MovementController body;
        public StealthTuning tuning;

        static StealthPlayerRules _current;
        readonly LucidityFade _fade = new LucidityFade();
        readonly List<SeekerBrain> _guards = new List<SeekerBrain>(8);
        readonly StealthDef _fallback = new StealthDef();
        bool _wasFaded, _wasDaytime, _applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static StealthPlayerRules Current => _current;
        public StealthDef Def => tuning != null ? tuning.def : _fallback;
        /// <summary>1 lucid (the dream's powers) .. 0 watched (gone). 1 when the lucidity rule is off.</summary>
        public float Lucidity => StealthRules.Shared.lucidityFade && !StealthRules.Shared.daytimeSection ? _fade.Value : 1f;
        public bool Faded => StealthRules.Shared.lucidityFade && !StealthRules.Shared.daytimeSection && _fade.Faded;
        public bool Daytime => StealthRules.Shared.daytimeSection;

        void Awake()
        {
            if (body == null) body = GetComponent<MovementController>();
        }

        void OnEnable() => _current = this;
        void OnDisable() { if (_current == this) _current = null; }

        void Update()
        {
            if (body == null) return;
            var rules = StealthRules.Shared;
            // C1: the daytime section.
            if (rules.daytimeSection != _wasDaytime || !_applied)
            {
                _wasDaytime = rules.daytimeSection;
                body.SetProfile(rules.daytimeSection ? MoveProfileKind.Daytime : MoveProfileKind.Dream);
                _wasFaded = false;
                _applied = true;
            }
            if (rules.daytimeSection) return;
            // C2: lucidity.
            if (!rules.lucidityFade)
            {
                if (_wasFaded) { body.OverrideProfile(null); _wasFaded = false; }
                _fade.Reset();
                return;
            }
            _guards.Clear();
            var all = SeekerAgent.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].isActiveAndEnabled && all[i].Brain != null) _guards.Add(all[i].Brain);
            var p = transform.position;
            bool watched = LucidityFade.IsWatched(Def, new System.Numerics.Vector3(p.x, p.y, p.z), _guards);
            _fade.Tick(Def, watched, Time.deltaTime);
            if (_fade.Faded != _wasFaded)
            {
                _wasFaded = _fade.Faded;
                body.OverrideProfile(_fade.Profile);
            }
        }

        /// <summary>Back to lucid and the dream's movement (a restart).</summary>
        public void ResetFade()
        {
            _fade.Reset();
            _wasFaded = false;
            _applied = false;
            if (body != null) body.OverrideProfile(null);
        }
    }
}
