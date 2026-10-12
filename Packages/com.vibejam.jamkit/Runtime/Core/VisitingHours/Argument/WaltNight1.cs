using System;
using System.Collections.Generic;

namespace JamKit.Argument
{
    /// <summary>
    /// Night 1's argument tutorial (docs/NIGHTS-1-2.md, "The argument tutorial"): Walt, the gentle gatekeeper at the foot
    /// of the hill, won't let Nadia up ("Your brother's having a good day. Let him have it. Come back tomorrow."). He
    /// argues; he doesn't teach. <b>Mags teaches</b>, on the earpiece, a line or two the first time each thing comes up
    /// (<see cref="ArgumentCoach"/>: ask, challenge, concede, the first pressure phase, press, let it rest).
    ///
    /// Small and safe: three premises, gentle pressure (slower, plates only at first, no opening phase), and composure
    /// that can't run out (<see cref="ArgumentDef.heartCap"/> 4). The usual outcomes; Walt steps aside in all of them,
    /// and his stance is remembered (the ledger) for the ending. Convinced wins Walt's truth, which also breaks Lena's
    /// L3'' on Night 2; Heard out, his wrench.
    ///
    /// The map: claim "Let him have his good day. Come back tomorrow."
    ///   W1  fact TRUE   he's having a good day                ask: reveals W2; concede: reveals W3
    ///   W2  link FALSE  so nothing's wrong here: he's safe     KEY: Walt is dead (ded_walt_dead)
    ///   W3  link FALSE  come back tomorrow                     optional: the same Saturday (ded_same_saturday)
    /// Mags, one line at a time: ask (at the start), concede (once she's heard W1), challenge (once she can break
    /// something), the first pressure phase, press (the first misstep), the first break, let it rest.
    ///
    /// Every word is in Resources/ArgumentText/walt_night1.txt, an agent placeholder for the creator to rewrite. His
    /// drawings (Resources/ArgumentArt/walt_*.png): antagonistic (his opening, palm raised: "not today"), neutral, kind,
    /// rattled, softened, sad; the file's face lines map the game's "hurt" to sad and "angry" to antagonistic.
    /// </summary>
    public static class WaltNight1
    {
        public const string Id = "walt_night1";
        public const string ProseFile = Id;
        /// <summary>The key deduction: Walt died in 2015, so this is memory (the shop's memory + Day 1's memorial card).</summary>
        public const string WaltDead = "ded_walt_dead";
        /// <summary>The loop: he's living one Saturday over and over (also Lena's L3'' card on Night 2).</summary>
        public const string SameSaturday = "ded_same_saturday";
        /// <summary>The memory at the shop: Walt alive, waving (touches nothing on its own: a lesson).</summary>
        public const string WaltAlive = "mem_walt_alive";
        public const string TruthId = "truth_walt";
        public const string Wrench = "pers_wrench";

        /// <summary>Where the prose file's text comes from (the Unity side reads Resources; tests read the file).</summary>
        public static Func<string> ProseSource;

        public static ArgumentDef Build() => Build(ProseSource != null ? ProseSource() : null);

        public static ArgumentDef Build(string prose)
        {
            var d = Structure();
            ArgumentText.Apply(d, ArgumentText.Parse(prose));
            ArgumentText.ApplyVariants(d, prose);
            return d;
        }

        public static ArgumentDef Structure()
        {
            var d = new ArgumentDef
            {
                id = Id,
                arena = "larch_street_foot",
                truthId = TruthId,
                personalId = Wrench,
                placeholderLines = true,
                heartCap = ArgumentEngine.PanicStep - 1,
                pressureScale = 0.7f,
                openingPressure = false,
                calmShapes = new List<PressureShape> { PressureShape.Wall },
                hotShapes = new List<PressureShape> { PressureShape.Wall, PressureShape.Bloom },
            };
            foreach (var m in ArgumentCoach.Moments) d.coach[m] = "";

            d.evidence.Add(new EvidenceDef { id = WaltDead, style = EvidenceStyle.Pinned });
            d.evidence.Add(new EvidenceDef { id = SameSaturday, style = EvidenceStyle.Pinned });
            d.evidence.Add(new EvidenceDef { id = WaltAlive, style = EvidenceStyle.Doodled });

            d.premises.Add(new PremiseDef { id = "W1", label = "W1", kind = PremiseKind.Fact, truth = PremiseTruth.True, askReveals = { "W2" }, concedeReveals = { "W3" } });
            d.premises.Add(new PremiseDef
            {
                id = "W2", label = "W2", kind = PremiseKind.Link, truth = PremiseTruth.False, key = true, hidden = true, parent = "W1",
                challenges = { Challenge(WaltDead, "sad", "kind", "neutral") },
            });
            d.premises.Add(new PremiseDef
            {
                id = "W3", label = "W3", kind = PremiseKind.Link, truth = PremiseTruth.False, hidden = true, parent = "W1",
                challenges = { Challenge(SameSaturday, "rattled", "kind", "neutral") },
            });
            return d;
        }

        static ChallengeDef Challenge(string evidence, string sound, string beside, string leap)
        {
            var c = new ChallengeDef { evidence = evidence, effect = SoundEffect.Break };
            c.reasons.Add(new ReasonDef { kind = ReasonKind.Sound, expression = sound });
            c.reasons.Add(new ReasonDef { kind = ReasonKind.Beside, expression = beside });
            c.reasons.Add(new ReasonDef { kind = ReasonKind.NonSequitur, expression = leap });
            return c;
        }
    }
}
