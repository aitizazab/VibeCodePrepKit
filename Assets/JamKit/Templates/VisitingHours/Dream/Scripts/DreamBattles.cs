using System;
using System.Collections.Generic;
using JamKit.Argument;
using UnityEngine;
using HouseOutcome = JamKit.DreamEscape.ArgumentOutcome;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The argument's battles, plugged into the dream (docs/NIGHTS-1-2.md, "Who builds what"; the argument agent's file).
    /// <see cref="DreamHouseGame.StartBattle"/> is set to <see cref="Start"/> as the game loads, so the house's argument
    /// with Lena is the real battle (lena_night2) with the cards for what she found. Night 1 calls the same shape for
    /// Walt: <c>DreamBattles.Start(WaltNight1.Id, evidence, done)</c>.
    ///
    /// How it ended comes back as the house's two endings: Condemns, or Unsure (Lena: unsure after Convinced, Heard out or
    /// Yielded; condemning after Pressed, Composure lost or antagonising that landed. Walt: Condemns only on his own
    /// condemning stance). The full result (truth, keepsake, Force, Despair) is in the ledger, or use
    /// <see cref="ArgumentHost.BeginBattle"/> directly for it.
    /// </summary>
    public static class DreamBattles
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Plug() => DreamHouseGame.StartBattle = Start;

        /// <summary>Starts battle <paramref name="battleId"/> with the journal ids she carries; <paramref name="done"/> gets
        /// Unsure or Condemns when its end card is closed (Unsure too if it couldn't start, so the night goes on).</summary>
        public static void Start(string battleId, IReadOnlyList<string> evidence, Action<HouseOutcome> done)
        {
            var host = ArgumentHost.BeginBattle(battleId, Context(evidence), result => done?.Invoke(OutcomeOf(result)));
            if (host != null) return;
            Debug.LogWarning("JAMKIT_DREAM no battle \"" + battleId + "\" could start: the night goes on, unsure");
            done?.Invoke(HouseOutcome.Unsure);
        }

        /// <summary>What the battle gets: the journal ids found (they gate its cards) and the hidden numbers the dream
        /// keeps. Despair: the save's Despair flag is the maximum (3), else 0. Force: no game-wide tally yet (0).</summary>
        public static ArgumentContext Context(IReadOnlyList<string> evidence)
        {
            var c = new ArgumentContext
            {
                despair = DreamEscapeGame.Despair ? ArgumentMeters.DespairMax : 0,
                force = 0,
                seed = (ulong)Environment.TickCount,
            };
            if (evidence != null) c.found.AddRange(evidence);
            return c;
        }

        /// <summary>The house's two endings from a battle's result (None: closed before it ended).</summary>
        public static HouseOutcome OutcomeOf(ArgumentResult result)
        {
            if (result == null || result.outcome == JamKit.Argument.ArgumentOutcome.None) return HouseOutcome.None;
            return result.Condemns ? HouseOutcome.Condemns : HouseOutcome.Unsure;
        }
    }
}
