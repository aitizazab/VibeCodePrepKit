using System;
using System.Collections.Generic;

namespace JamKit.Argument
{
    /// <summary>
    /// What the game hands a battle (docs/ARGUMENT-PROTOTYPE.md, "Starting a battle from the game"): what Nadia has
    /// found, and the game's hidden numbers. The Unity side starts it with <c>ArgumentHost.BeginBattle(id, context, done)</c>.
    /// </summary>
    public sealed class ArgumentContext
    {
        /// <summary>Every journal id found so far: memories, deductions, facts, truths (extra ids are ignored). A battle's
        /// card is in the tray only once its id (or one of its <see cref="EvidenceDef.unlockedBy"/>) is here.</summary>
        public readonly List<string> found = new List<string>();
        /// <summary>The game-wide Force tally before the battle.</summary>
        public int force;
        /// <summary>The hidden Despair value, 0..3 (3 = max).</summary>
        public int despair;
        public ArgumentDifficulty difficulty = ArgumentDifficulty.Normal;
        /// <summary>Settings' pressure speed (0.5..1).</summary>
        public float pressureSpeed = 1f;
        /// <summary>Debug and the prototype: every card, found or not.</summary>
        public bool allEvidence;
        public ulong seed = 1;
        /// <summary>The day's bedside (docs/redesign/day.md): +1 her anger starts a step higher ("Lena"), -1 a step gentler
        /// ("just sit").</summary>
        public int angerStart;
        /// <summary>Nadia already said sorry at his bedside (Day 1's "the phone"): the honest answer to "He called you
        /// first" is open from the start.</summary>
        public bool admitted;
    }

    /// <summary>What a battle hands back to the game.</summary>
    public sealed class ArgumentResult
    {
        public string battleId = "";
        /// <summary>Convinced, HeardOut, Pressed, ComposureLost or Yielded (None: closed before it ended).</summary>
        public ArgumentOutcome outcome;
        /// <summary>Where they stand. Lena: Unsure or Condemns (the creator's two endings). Walt: Amenable, Unsure or Condemns.</summary>
        public ArgumentStance stance;
        /// <summary>Presses this battle: add them to the game's Force tally.</summary>
        public int forceAdded;
        /// <summary>Add to Despair: <see cref="ArgumentBattles.DespairOnCondemn"/> when Lena condemns Nadia, else 0.</summary>
        public int despairAdded;
        /// <summary>The journal ids won: a truth (Convinced) or a keepsake (Heard out); "" if none.</summary>
        public string truth = "", keepsake = "";
        /// <summary>The ledger's record (already saved by the host).</summary>
        public ArgumentRecord record;

        public bool Condemns => stance == ArgumentStance.Condemns;
    }

    /// <summary>
    /// The battles the game can start by id, and the hand-off between the game and a battle: the setup from the game's
    /// <see cref="ArgumentContext"/>, the <see cref="ArgumentResult"/> from the battle's record.
    /// </summary>
    public static class ArgumentBattles
    {
        public const string Lena = DreamLenaDemo.Id;
        public const string Walt = WaltNight1.Id;
        public static readonly string[] Ids = { Walt, Lena };

        /// <summary>How much Despair rises when Lena condemns Nadia (out of 3).</summary>
        public const int DespairOnCondemn = 1;

        /// <summary>The save's flag for Lena's watch being Nadia's (the same text as JamKit.VisitingHours.Chain's
        /// KeepsakeWatchFlag, which the nights and the ward read).</summary>
        public const string KeepsakeFlag = "vh_lena_watch";

        /// <summary>Heard out with Lena: her star-shaped watch is Nadia's from now on. Sets the save's keepsake flag (the
        /// host calls this as it saves a battle's result). True if it gave the watch.</summary>
        public static bool RecordKeepsake(JamKit.Flow.SaveData save, ArgumentRecord record)
        {
            if (save == null || record == null || record.outcome != ArgumentOutcome.HeardOut || record.personal != DreamLenaDemo.Watch) return false;
            save.SetFlag(KeepsakeFlag);
            return true;
        }

        /// <summary>Nadia has Lena's watch (the flag, or the ledger's Heard out with it).</summary>
        public static bool OwnsKeepsake(JamKit.Flow.SaveData save, ArgumentLedger ledger = null)
        {
            if (save == null) return false;
            if (save.HasFlag(KeepsakeFlag)) return true;
            var r = ledger?.Get(DreamLenaDemo.Id);
            return r != null && r.personal == DreamLenaDemo.Watch;
        }

        /// <summary>The battle with its prose file's lines, or null for an unknown id.</summary>
        public static ArgumentDef Build(string id)
        {
            switch (id)
            {
                case Lena: return DreamLenaDemo.Build();
                case Walt: return WaltNight1.Build();
                default: return null;
            }
        }

        /// <summary>The engine's setup for what the game passed in.</summary>
        public static ArgumentSetup Setup(ArgumentContext context, ArgumentLedger ledger = null)
        {
            context = context ?? new ArgumentContext();
            var setup = new ArgumentSetup
            {
                difficulty = context.difficulty,
                allEvidence = context.allEvidence,
                pressureSpeed = context.pressureSpeed,
                seed = context.seed == 0 ? 1UL : context.seed,
                force = context.force,
                despair = context.despair < 0 ? 0 : context.despair > ArgumentMeters.DespairMax ? ArgumentMeters.DespairMax : context.despair,
                ledger = ledger,
                angerStart = Math.Max(-1, Math.Min(1, context.angerStart)),
                admitted = context.admitted,
            };
            setup.evidence.AddRange(context.found);
            return setup;
        }

        /// <summary>The result for the game from a battle's record (null record: closed before it ended).</summary>
        public static ArgumentResult Result(ArgumentDef def, ArgumentRecord record)
        {
            var r = new ArgumentResult { battleId = def != null ? def.id : "", record = record };
            if (record == null) return r;
            r.outcome = record.outcome;
            r.stance = record.stance;
            r.forceAdded = record.presses;
            r.truth = record.truth ?? "";
            r.keepsake = record.personal ?? "";
            r.despairAdded = def != null && def.stanceRule == StanceRule.TwoEndings && record.stance == ArgumentStance.Condemns ? DespairOnCondemn : 0;
            return r;
        }
    }
}
