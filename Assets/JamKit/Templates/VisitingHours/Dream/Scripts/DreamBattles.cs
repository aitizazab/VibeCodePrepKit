using System;
using System.Collections.Generic;
using JamKit.Argument;
using JamKit.Shell;
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
            var host = ArgumentHost.BeginBattle(battleId, Context(evidence, battleId), result => done?.Invoke(OutcomeOf(result)));
            if (host != null) return;
            Debug.LogWarning("JAMKIT_DREAM no battle \"" + battleId + "\" could start: the night goes on, unsure");
            done?.Invoke(HouseOutcome.Unsure);
        }

        /// <summary>What the battle gets: the journal ids found (they gate its cards) and the hidden numbers the run keeps,
        /// from the chain's tally (JamKit.VisitingHours.Chain, the opening agent's): Force (the battles' presses so far),
        /// Despair (Lena condemning; the save's Despair flag is the maximum, 3).</summary>
        public static ArgumentContext Context(IReadOnlyList<string> evidence)
        {
            var save = GameRoot.Exists ? GameRoot.Save.Data : null;
            var c = new ArgumentContext
            {
                despair = save != null ? JamKit.VisitingHours.Chain.DespairTally(save) : 0,
                force = save != null ? JamKit.VisitingHours.Chain.ForceTally(save) : 0,
                seed = (ulong)Environment.TickCount,
            };
            if (evidence != null) c.found.AddRange(evidence);
            return c;
        }

        /// <summary>As <see cref="Context(IReadOnlyList{string})"/>, plus what the day's bedside changed for this battle
        /// (docs/redesign/day.md, read through Chain.HasDreamChange): for Lena, Day 2's "Lena" starts her anger a step
        /// higher and "just sit" a step gentler; Day 1's "the phone" means Nadia already said sorry, so the honest answer
        /// to "He called you first" is open from the start.</summary>
        public static ArgumentContext Context(IReadOnlyList<string> evidence, string battleId)
        {
            var c = Context(evidence);
            var save = GameRoot.Exists ? GameRoot.Save.Data : null;
            if (save == null || battleId != ArgumentBattles.Lena) return c;
            if (JamKit.VisitingHours.Chain.HasDreamChange(save, JamKit.VisitingHours.DreamChanges.LenaWary)) c.angerStart += 1;
            if (JamKit.VisitingHours.Chain.HasDreamChange(save, JamKit.VisitingHours.DreamChanges.LenaSofter)) c.angerStart -= 1;
            c.admitted = JamKit.VisitingHours.Chain.HasDreamChange(save, JamKit.VisitingHours.DreamChanges.LenaSorry);
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
