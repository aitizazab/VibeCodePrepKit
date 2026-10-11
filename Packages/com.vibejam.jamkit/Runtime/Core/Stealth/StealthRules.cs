using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Stealth
{
    /// <summary>
    /// Which answers to "the movement trivializes stealth" are on (docs/STEALTH.md "When the movement outruns the
    /// guards"). All off is the original kit. A game turns on what it wants; the Stealth Lab switches each live.
    /// <list type="bullet">
    /// <item><b>A, loud movement:</b> walking is a little noise, sprinting louder with speed, double jumps and wallruns
    /// heard well beyond sight; guards who hear turn wary (any noise does that), and repeated noise escalates.</item>
    /// <item><b>B, the dream pushes back:</b> a guard who becomes suspicious calls the others near him to the spot, and
    /// the faster you move the farther and quicker the guards notice you.</item>
    /// <item><b>C1, daytime:</b> the section uses the daytime movement profile (no dream powers).</item>
    /// <item><b>C2, being watched dims her lucidity:</b> near a guard (or a wary one farther off) the dream powers fade
    /// (no double jump, wallrun or slide; a lower speed cap).</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class StealthRules
    {
        public bool loudMovement;
        public bool dreamPushesBack;
        public bool daytimeSection;
        public bool lucidityFade;

        static StealthRules _shared = new StealthRules();

        /// <summary>The rules a scene's seekers and player share (Unity components read this).</summary>
        public static StealthRules Shared => _shared;
        public static void ResetShared() => _shared = new StealthRules();

        public bool Any => loudMovement || dreamPushesBack || daytimeSection || lucidityFade;

        /// <summary>Sight range and meter rate scale for a target moving at <paramref name="speed"/> (answer B).</summary>
        public float NoticeScale(StealthDef def, float speed) =>
            dreamPushesBack ? 1f + def.noticePerSpeed * Math.Max(0f, speed - def.noticeFromSpeed) : 1f;
    }

    /// <summary>
    /// The guards' call (answer B): when one becomes suspicious, starts searching or finds you, every other guard within
    /// <see cref="StealthDef.callRadius"/> of him turns wary and comes to the spot. Call <see cref="Propagate"/> after
    /// ticking every guard. No allocation.
    /// </summary>
    public static class SeekerSquad
    {
        public static int Propagate(IList<SeekerBrain> guards, StealthRules rules)
        {
            if (rules == null || !rules.dreamPushesBack) return 0;
            int heard = 0;
            for (int i = 0; i < guards.Count; i++)
            {
                var caller = guards[i];
                if (caller == null || !caller.Events.called) continue;
                for (int j = 0; j < guards.Count; j++)
                {
                    var other = guards[j];
                    if (j == i || other == null) continue;
                    if (Vector3.Distance(other.Position, caller.Position) > caller.def.callRadius) continue;
                    if (other.HearCall(caller.CallSpot)) heard++;
                }
            }
            return heard;
        }
    }

    /// <summary>
    /// Answer C2: being watched dims her lucidity. 1 = lucid (the dream's powers), 0 = watched (they're gone). It falls
    /// over <see cref="StealthDef.lucidityFadeTime"/> while a guard watches (within <see cref="StealthDef.lucidityRadius"/>,
    /// or <see cref="StealthDef.lucidityAlertRadius"/> of a wary or alert one) and comes back over
    /// <see cref="StealthDef.lucidityRecoverTime"/>. <see cref="Profile"/> is the movement to use: null (the dream) while
    /// lucid, the watched profile once it has faded below half.
    /// </summary>
    public sealed class LucidityFade
    {
        float _value = 1f;
        bool _watched;
        readonly MoveProfile _watchedProfile = new MoveProfile();

        public float Value => _value;
        public bool Watched => _watched;
        public bool Faded => _value < 0.5f;

        /// <summary>The movement profile for now: null (everything) while lucid, the watched one once faded.</summary>
        public MoveProfile Profile => Faded ? _watchedProfile : null;

        public static bool IsWatched(StealthDef def, Vector3 feet, IList<SeekerBrain> guards)
        {
            for (int i = 0; i < guards.Count; i++)
            {
                var g = guards[i];
                if (g == null || g.Counting) continue;
                float d = Vector3.Distance(new Vector3(feet.X, 0f, feet.Z), new Vector3(g.Position.X, 0f, g.Position.Z));
                bool alert = g.State != AlertState.Unaware || g.Wary;
                if (d <= def.lucidityRadius || (alert && d <= def.lucidityAlertRadius)) return true;
            }
            return false;
        }

        public void Tick(StealthDef def, bool watched, float dt)
        {
            _watched = watched;
            _watchedProfile.doubleJump = false;
            _watchedProfile.wallrun = false;
            _watchedProfile.slide = false;
            _watchedProfile.airStrafeScale = 0.5f;
            _watchedProfile.speedCap = def.watchedSpeedCap;
            if (watched) _value = Math.Max(0f, _value - dt / Math.Max(0.01f, def.lucidityFadeTime));
            else _value = Math.Min(1f, _value + dt / Math.Max(0.01f, def.lucidityRecoverTime));
        }

        public void Reset() { _value = 1f; _watched = false; }
    }
}
