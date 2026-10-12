using System.Collections.Generic;

namespace JamKit.Argument
{
    /// <summary>What the pilot means to do next: a verb on a premise (or the claim), with the evidence and reason for a
    /// challenge.</summary>
    public enum PilotVerb { None, Ask, Concede, Challenge, Rest, Yield, Press }

    public struct PilotPlan
    {
        public PilotVerb verb;
        public string premise;
        public string evidence;
        public ReasonDef reason;
        public string what;
    }

    /// <summary>
    /// Plays an argument's honest line from its data (tests, the smoke run, the prototype's "watch it play" chapter):
    /// ask where asking reveals something, break what the evidence in hand can break (with the sound reason), concede
    /// what's true, ask again, hear out the rest, and let it rest when nothing's left. Pressure phases are dodged by
    /// <see cref="PressurePilot"/>. One verb per <see cref="Step"/>. <see cref="Choose"/> only decides (the real-time
    /// battle's autopilot, <see cref="BattlePilot"/>, then plays the choice out in the debate).
    /// </summary>
    public sealed class ArgumentPilot
    {
        readonly List<ReasonCard> _cards = new List<ReasonCard>(4);
        readonly List<PremiseDef> _visible = new List<PremiseDef>(16);
        string _lastAction = "";

        /// <summary>The last thing the pilot did, as a short sentence (the view can show it in attract mode).</summary>
        public string LastAction => _lastAction;

        /// <summary>Decides the next verb without doing it. <paramref name="skip"/>: premises to leave alone for now
        /// (the real-time pilot can't reach them yet). False when there's nothing sensible left.</summary>
        public bool Choose(ArgumentEngine e, out PilotPlan plan, ICollection<string> skip = null)
        {
            plan = default;
            if (e == null || e.Ended || !e.Started) return false;
            _visible.Clear();
            _visible.AddRange(e.VisiblePremises);
            bool Skip(string id) => skip != null && skip.Contains(id);

            // 1. Ask what hides something.
            foreach (var p in _visible)
                if (!Skip(p.id) && e.Status(p.id) == PremiseStatus.Standing && e.TimesAsked(p.id) == 0 && HidesSomething(e, p.askReveals))
                    return Plan(out plan, PilotVerb.Ask, p.id, "asked about " + p.label);
            // 2. Break what can be broken, with the sound reason.
            foreach (var p in _visible)
            {
                if (Skip(p.id) || e.Status(p.id) != PremiseStatus.Standing || p.unbreakable) continue;
                foreach (var c in p.challenges)
                {
                    if (c == null || !e.HasEvidence(c.evidence)) continue;
                    var sound = Sound(c);
                    if (sound == null) continue;
                    plan = new PilotPlan { verb = PilotVerb.Challenge, premise = p.id, evidence = c.evidence, reason = sound, what = "broke " + p.label + " with " + c.evidence };
                    return true;
                }
            }
            // 3. Concede what's true (key premises are never true: they're there to break).
            foreach (var p in _visible)
                if (!Skip(p.id) && e.Status(p.id) == PremiseStatus.Standing && p.truth != PremiseTruth.False && !p.key)
                    return Plan(out plan, PilotVerb.Concede, p.id, "conceded " + p.label);
            // 4. Ask again where that reveals the deepest one.
            foreach (var p in _visible)
            {
                if (Skip(p.id)) continue;
                var s = e.Status(p.id);
                if ((s == PremiseStatus.Standing || s == PremiseStatus.Conceded) && e.TimesAsked(p.id) == 1 && HidesSomething(e, p.askAgainReveals))
                    return Plan(out plan, PilotVerb.Ask, p.id, "asked " + p.label + " again");
            }
            // 5. Hear out anything not yet heard.
            foreach (var p in _visible)
                if (!Skip(p.id) && !e.IsHeard(p.id) && e.CanAsk(p.id)) return Plan(out plan, PilotVerb.Ask, p.id, "asked about " + p.label);
            // 6. Nothing left to argue.
            if (e.CanLetItRest) return Plan(out plan, PilotVerb.Rest, ArgumentEngine.ClaimId, "let it rest");
            if (skip != null && skip.Count > 0) return false; // something's only out of reach for now: wait
            // 7. Stuck: the cards that would bring the rest out aren't in the tray (they're found in the night, and this
            // battle started without them), so nothing can be broken or heard out. Give in on the claim rather than stall:
            // Yielded, which leaves them unsure.
            if (e.CanConcedeClaim) return Plan(out plan, PilotVerb.Yield, ArgumentEngine.ClaimId, "gave in on the claim (nothing left it could do)");
            return false;
        }

        /// <summary>Takes one verb. False when there's nothing sensible left to do (or it's over, or a phase is up).</summary>
        public bool Step(ArgumentEngine e)
        {
            if (e == null || e.Ended || e.InPressure || !e.Started) return false;
            if (!Choose(e, out var plan)) return false;
            bool ok;
            switch (plan.verb)
            {
                case PilotVerb.Ask: ok = e.Ask(plan.premise); break;
                case PilotVerb.Concede: ok = e.Concede(plan.premise); break;
                case PilotVerb.Rest: ok = e.LetItRest(); break;
                case PilotVerb.Yield: ok = e.ConcedeClaim(); break;
                case PilotVerb.Challenge:
                    ok = e.BeginChallenge(plan.premise, plan.evidence, _cards) == ChallengeStep.Reasons && e.ResolveChallenge(plan.premise, plan.evidence, plan.reason);
                    break;
                default: ok = false; break;
            }
            if (ok) _lastAction = plan.what;
            return ok;
        }

        /// <summary>Plays to the end (or until stuck), dodging with <see cref="PressurePilot"/>. Returns the outcome.</summary>
        public ArgumentOutcome Play(ArgumentEngine e, float dt = 1f / 60f, int maxTicks = 200000)
        {
            if (!e.Started) e.Start();
            for (int i = 0; i < maxTicks && !e.Ended; i++)
            {
                if (e.InPressure)
                {
                    e.Tick(dt, e.Pressure != null ? PressurePilot.Steer(e.Pressure) : default);
                    continue;
                }
                if (!Step(e)) break;
            }
            return e.Outcome;
        }

        static bool Plan(out PilotPlan plan, PilotVerb verb, string premise, string what)
        {
            plan = new PilotPlan { verb = verb, premise = premise, what = what };
            return true;
        }

        /// <summary>The challenge's sound reason (null if it has none).</summary>
        public static ReasonDef Sound(ChallengeDef c)
        {
            foreach (var r in c.reasons)
                if (r != null && r.kind == ReasonKind.Sound) return r;
            return null;
        }

        static bool HidesSomething(ArgumentEngine e, List<string> ids)
        {
            foreach (var id in ids)
                if (e.Status(id) == PremiseStatus.Hidden) return true;
            return false;
        }
    }
}
