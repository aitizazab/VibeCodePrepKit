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
