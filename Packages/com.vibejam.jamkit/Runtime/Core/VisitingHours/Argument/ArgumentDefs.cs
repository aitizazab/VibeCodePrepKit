using System;
using System.Collections.Generic;

namespace JamKit.Argument
{
    // The data of an argument battle, concept A "Take it apart" (docs/ARGUMENT-BATTLES.md, section 8;
    // the prototype's notes: docs/ARGUMENT-PROTOTYPE.md). Plain serializable classes: content files build them
    // in code, tests build small ones.

    /// <summary>A fact is about the world (true or false); a link is the "so" that turns facts into the claim.</summary>
    public enum PremiseKind { Fact, Link }

    public enum PremiseTruth { True, False, Half }

    /// <summary>The three "because..." cards: one that follows, one true but beside the point, one that doesn't
    /// follow. <see cref="Antagonise"/> is the creator's fourth kind: a reply that dodges the question and attacks
    /// the person ("You're not even real"); it angers them and hardens their pressure.</summary>
    public enum ReasonKind { Sound, Beside, NonSequitur, Antagonise }

    /// <summary>What a sound challenge does: the premise comes out (Break); a weaker premise grows in its place
    /// (FallBack); or the premise under it appears (Deepen).</summary>
    public enum SoundEffect { Break, FallBack, Deepen }

    /// <summary>The evidence tray's styles (doc 3.1): waking facts stamped, dream memories doodled, deductions
    /// pinned, truths gold-leafed, personal entries tagged; <see cref="Quote"/>: her own words, caught as she said them
    /// (docs/redesign/battle.md, "Her own words").</summary>
    public enum EvidenceStyle { Stamped, Doodled, Pinned, Gold, Tagged, Quote }

    /// <summary>Per battle. Story: no hits, slips collect themselves. ScriptedButBeatable (the Long Night): Nadia
    /// starts at 124 bpm, every exchange is followed by a brutal pressure phase whose gaps are only just wide
    /// enough and only just reachable: technically winnable, near impossible.</summary>
    public enum ArgumentDifficulty { Normal, Story, ScriptedButBeatable }

    /// <summary>A pressure phase's shapes (their emotion, played on the arena's surface): walls of things sliding
    /// across with one gap (Lena's plates), rings blooming out with one gap (her flour), lobbed blobs aimed at
    /// Nadia (batter).</summary>
    public enum PressureShape { Wall, Bloom, Arc }

    [Serializable]
    public class EvidenceDef
    {
        public string id = "";
        public string name = "";
        public EvidenceStyle style = EvidenceStyle.Stamped;
        /// <summary>One line for the tray's tooltip ("Theo's chair, set every Sunday").</summary>
        public string note = "";
        /// <summary>The journal ids (memories, deductions, facts) that put this card in the tray, any one of them; the
        /// card's own id always does. Until one is found the card isn't there (unless the setup gives every card).</summary>
        public List<string> unlockedBy = new List<string>();
        /// <summary>A quote card (<see cref="EvidenceStyle.Quote"/>): the words that become it when Nadia catches them as
        /// the opponent says them, in any of their lines (plain words; case doesn't matter). Never in the tray until caught.</summary>
        public string words = "";

        public bool IsQuote => style == EvidenceStyle.Quote;

        /// <summary>True if any of <paramref name="found"/> unlocks this card.</summary>
        public bool UnlockedBy(ICollection<string> found)
        {
            if (found == null) return false;
            if (found.Contains(id)) return true;
            foreach (var u in unlockedBy) if (!string.IsNullOrEmpty(u) && found.Contains(u)) return true;
            return false;
        }
    }

    [Serializable]
    public class ReasonDef
    {
        /// <summary>Its id in the prose file ("L1b.sound", or "L3c.truth_walt.sound" for one piece of evidence): the reply's
        /// is this plus ".reply" (for conditional variants).</summary>
        public string id = "";
        /// <summary>The card, written as the second half of "You said X. But this shows Y, so...".</summary>
        public string text = "";
        public ReasonKind kind = ReasonKind.Sound;
        /// <summary>The opponent's answer to this reason.</summary>
        public string reply = "";
        public string expression = "";
    }

    [Serializable]
    public class ChallengeDef
    {
        /// <summary>The evidence this challenge needs (an id from the tray).</summary>
        public string evidence = "";
        /// <summary>Usually three: one sound, one beside the point, one that doesn't follow (shown shuffled).</summary>
        public List<ReasonDef> reasons = new List<ReasonDef>();
        public SoundEffect effect = SoundEffect.Break;
        /// <summary>FallBack / Deepen: the premise that appears.</summary>
        public string effectTarget = "";
    }

    /// <summary>An antagonising reply to a premise that's a question: it dodges the question and attacks them.</summary>
    [Serializable]
    public class RetortDef
    {
        public string line = "";
        public string reply = "";
        public string expression = "angry";
    }

    /// <summary>A card that breaks nothing but gets an answer of its own when shown against a premise (Lena's star wheel
    /// against "You don't know where I am": she can't say where she went on Thursdays). It costs nothing.</summary>
    [Serializable]
    public class AsideDef
    {
        public string evidence = "";
        /// <summary>Nadia's words as she shows it.</summary>
        public string line = "";
        public string reply = "";
        public string expression = "";
    }

    [Serializable]
    public class PremiseDef
    {
        public string id = "";
        /// <summary>A short tag on the piece ("L1", "L3''").</summary>
        public string label = "";
        public PremiseKind kind = PremiseKind.Fact;
        public PremiseTruth truth = PremiseTruth.True;
        public string text = "";
        /// <summary>The phrase that glows as the statement flies in the debate (plain words, a part of <see cref="text"/>):
        /// a piece of evidence has to hit it. Empty, or not found in the text: the whole statement.</summary>
        public string weak = "";
        /// <summary>Key premises must all be broken for Convinced.</summary>
        public bool key;
        /// <summary>Starts unseen: revealed by Ask, a concession, a fall-back or deepen, a slip, or by script.</summary>
        public bool hidden;
        /// <summary>The piece it hangs from (layout only; "" = on the claim).</summary>
        public string parent = "";

        public string askReply = "";
        public List<string> askReveals = new List<string>();
        /// <summary>Asking the same premise a second time (the deepest premises: "ask twice in the right place").</summary>
        public string askAgainReply = "";
        public List<string> askAgainReveals = new List<string>();
        /// <summary>Has a second answer (<see cref="askAgainReply"/>) even when asking again reveals nothing (L3's points
        /// Nadia to Theo's drawing).</summary>
        public bool askAgain;

        /// <summary>Nadia's words when she concedes it ("He is happy.").</summary>
        public string concedeLine = "";
        public string concedeReply = "";
        public List<string> concedeReveals = new List<string>();
        /// <summary>Conceding something false: the line closes (a misplay, never a dead end).</summary>
        public string concedeFalseReply = "";

        /// <summary>Challenging a true premise: no reason cards, just this ("That's a true spoke. Leave it.").</summary>
        public string trueReply = "";
        /// <summary>Evidence that doesn't touch it ("" = the battle's generic line).</summary>
        public string noTouchReply = "";
        /// <summary>It can't be broken tonight: every challenge gets this reply, and that's the lesson.</summary>
        public bool unbreakable;
        public string unbreakableReply = "";
        public string pressLine = "";
        public string pressReply = "";

        /// <summary>Only a slip in a pressure phase (or asking twice) reveals it: what they aren't saying.</summary>
        public bool revealedBySlip;
        /// <summary>The slip's name on the surface ("a paper plane").</summary>
        public string slipName = "";
        /// <summary>Surfaces by script when any of these premises is resolved (broken, conceded or forced).</summary>
        public List<string> surfacesAfter = new List<string>();
        /// <summary>A painful premise: its surfacing brings the high pressure phase.</summary>
        public bool painful;

        /// <summary>A question they put to Nadia ("Then tell me where I am."); with a <see cref="retort"/> she can dodge it.</summary>
        public string question = "";
        public RetortDef retort;

        public List<ChallengeDef> challenges = new List<ChallengeDef>();
        /// <summary>Cards that get an answer of their own here without breaking it (<see cref="AsideDef"/>).</summary>
        public List<AsideDef> asides = new List<AsideDef>();

        public AsideDef AsideFor(string evidenceId)
        {
            foreach (var a in asides)
                if (a != null && a.evidence == evidenceId) return a;
            return null;
        }

        public ChallengeDef ChallengeFor(string evidenceId)
        {
            foreach (var c in challenges)
                if (c != null && c.evidence == evidenceId) return c;
            return null;
        }
    }

    /// <summary>A supporter in the room (the creator's idea: Lena and Theo in the back seats backing Nadia). Each can
    /// add a line (when a premise appears, or at the start) and a card (evidence for the tray, or an extra reason on
    /// one challenge). A slot only: no content yet.</summary>
    [Serializable]
    public class SupporterDef
    {
        public string id = "";
        public string speaker = "";
        /// <summary>When the line is said: the premise whose appearance prompts it ("" = the opening).</summary>
        public string premise = "";
        public string line = "";
        /// <summary>An evidence card they add to the tray (null = none).</summary>
        public EvidenceDef card;
        /// <summary>An extra reason card on the challenge of <see cref="reasonPremise"/> with <see cref="reasonEvidence"/>.</summary>
        public ReasonDef reason;
        public string reasonPremise = "";
        public string reasonEvidence = "";
    }

    /// <summary>
    /// A line that replaces another while a condition holds (the prose file's <c>id[condition]: text</c>). The condition
    /// reads the battle's meters (<see cref="ArgumentCondition"/>): Lena's anger, the Force tally, Despair...
    /// The first variant in file order whose condition holds wins; none: the plain line.
    /// </summary>
    [Serializable]
    public class LineVariant
    {
        public string id = "";
        public string condition = "";
        public string text = "";
        [NonSerialized] public ArgumentCondition parsed;
    }

    /// <summary>How a battle's end becomes the opponent's stance.</summary>
    public enum StanceRule
    {
        /// <summary><see cref="ArgumentEngine.StanceFor"/>: amenable, unsure or condemning, by outcome, anger and presses.</summary>
        Graded,
        /// <summary>The creator's two endings (Lena, Night 2): Convinced, Heard out or Yielded leave them unsure; Pressed,
        /// Composure lost or any antagonising that landed (a card or a retort) make them condemn Nadia.</summary>
        TwoEndings,
    }

    [Serializable]
    public class ArgumentDef
    {
        public string id = "";
        public string opponent = "";
        public string arena = "";
        public string claim = "";
        /// <summary>The opponent's first line (their feeling as they make the claim).</summary>
        public string opening = "";
        public List<PremiseDef> premises = new List<PremiseDef>();
        /// <summary>Every card that can be in the tray for this battle (the setup says which ones Nadia has).</summary>
        public List<EvidenceDef> evidence = new List<EvidenceDef>();
        /// <summary>Evidence that doesn't touch a premise: their line (in their voice).</summary>
        public string noTouchReply = "That doesn't touch what I said.";
        public string pressClaimLine = "";
        public string pressClaimReply = "";
        public string concedeClaimLine = "";
        public string concedeClaimReply = "";
        public string letItRestLine = "I hear you.";
        public string letItRestReply = "";

        /// <summary>Convinced gives their revised claim as a truth (an entry id and its words).</summary>
        public string truthId = "";
        public string truthText = "";
        /// <summary>Heard out gives a personal entry (an object of theirs).</summary>
        public string personalId = "";
        public string personalText = "";
        public string convincedReply = "";
        public string heardOutReply = "";
        public string pressedReply = "";
        public string composureLostLine = "";
        /// <summary>Their answer as Nadia leaves the table (composure lost): the cost, in their voice.</summary>
        public string composureLostReply = "";
        public string composureLostExpression = "angry";

        /// <summary>The words they cut back with when Nadia presses (the showdown: she cuts through each as it reaches her).</summary>
        public List<string> showdown = new List<string>();
        /// <summary>Short lines the debate calls for (<see cref="ArgumentBarks"/>), by id.</summary>
        public Dictionary<string, string> barks = new Dictionary<string, string>();
        /// <summary>The words written along their walls of pressure ("Sit down", "My kitchen").</summary>
        public List<string> pressureWords = new List<string>();
        /// <summary>The painful premise's phase is the three rings (a phone at the table's edge; standing still hurts).</summary>
        public bool painfulCall;
        /// <summary>How hard the debate's own pressure is, as a share of a full phase's density (1 = as dense).</summary>
        public float debateDensity = 0.55f;
        /// <summary>The sections presenter (docs/redesign/battle.md): once the painful phase has played, the ceiling lifts and
        /// the rest is played under her sky (SELECTION.md 7): her case is a constellation, the light moves along its lines.</summary>
        public bool sky;
        /// <summary>She takes notes on you (SELECTION.md 4): from the second full phase her pencil follows the light's path,
        /// inking where it has been, and after it she says how Nadia moved.</summary>
        public bool notes;
        /// <summary>Her care (SELECTION.md 3, "hush"): a soft reply brings a ring that closes in on the light and passes
        /// harmlessly if Nadia holds still; her questions bring a wave that hurts only a light standing still.</summary>
        public bool hush;

        /// <summary>Pressure shapes by heat 0..3 (and the slips' look: the premises' slipName).</summary>
        public List<PressureShape> calmShapes = new List<PressureShape> { PressureShape.Bloom };
        public List<PressureShape> hotShapes = new List<PressureShape> { PressureShape.Wall, PressureShape.Bloom, PressureShape.Arc };

        public List<SupporterDef> supporters = new List<SupporterDef>();

        /// <summary>Every line is a placeholder for the creator's writing pass (the view says so on screen).</summary>
        public bool placeholderLines;

        /// <summary>Lines that replace others under a condition (<see cref="LineVariant"/>), from the prose file.</summary>
        public List<LineVariant> variants = new List<LineVariant>();
        /// <summary>How the end becomes their stance.</summary>
        public StanceRule stanceRule = StanceRule.Graded;
        /// <summary>Pressing a premise also angers them (Lena: antagonising, retorting and pressing raise her anger).</summary>
        public bool pressAngers;
        /// <summary>The highest heart step her composure can reach (5 = 160 bpm, panic). The tutorial caps it at 4, so
        /// it can't be lost.</summary>
        public int heartCap = ArgumentEngine.PanicStep;
        /// <summary>Scales every pressure phase's speed (1 = as set; the tutorial's are gentler).</summary>
        public float pressureScale = 1f;
        /// <summary>The battle opens with a low pressure phase (the tutorial doesn't: its first comes with the first misstep).</summary>
        public bool openingPressure = true;
        /// <summary>Which of this opponent's drawings stands for one of the game's moods (the prose file's
        /// <c>face.hurt: sad</c>): Walt has no "hurt" or "angry" drawing. A mood with no entry is used as it is.</summary>
        public Dictionary<string, string> faces = new Dictionary<string, string>();

        /// <summary>The drawing for <paramref name="mood"/> (through <see cref="faces"/>).</summary>
        public string Face(string mood) => !string.IsNullOrEmpty(mood) && faces.TryGetValue(mood, out var f) && !string.IsNullOrEmpty(f) ? f : mood ?? "";

        /// <summary>A coach on the earpiece (Mags in the Night 1 tutorial): a line the first time each moment comes
        /// (<see cref="ArgumentCoach"/>), by moment id. Empty: no coach.</summary>
        public Dictionary<string, string> coach = new Dictionary<string, string>();
        public string coachName = "";

        /// <summary>This battle's screen words, or null for the shared screen file (ArgumentStrings.Shared).</summary>
        [NonSerialized] public ArgumentStrings strings;

        public PremiseDef Premise(string id)
        {
            foreach (var p in premises)
                if (p != null && p.id == id) return p;
            return null;
        }

        public EvidenceDef Evidence(string id)
        {
            foreach (var e in evidence)
                if (e != null && e.id == id) return e;
            foreach (var s in supporters)
                if (s != null && s.card != null && s.card.id == id) return s.card;
            return null;
        }
    }
}
