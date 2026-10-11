using System.Collections.Generic;

namespace JamKit.Argument
{
    /// <summary>
    /// Plays an argument's honest line from its data (tests, the smoke run, the prototype's "watch it play" chapter):
    /// ask where asking reveals something, concede what's true, break key premises with the sound reason, ask again
    /// for what's under them, and let it rest when nothing's left. Pressure phases are dodged by
    /// <see cref="PressurePilot"/>. One verb per <see cref="Step"/>.
    /// </summary>
    public sealed class ArgumentPilot
    {
        readonly List<ReasonCard> _cards = new List<ReasonCard>(4);
        string _lastAction = "";

        /// <summary>The last thing the pilot did, as a short sentence (the view can show it in attract mode).</summary>
        public string LastAction => _lastAction;

        /// <summary>Takes one verb. False when there's nothing sensible left to do (or it's over, or a phase is up).</summary>
        public bool Step(ArgumentEngine e)
        {
            if (e == null || e.Ended || e.InPressure || !e.Started) return false;
            var visible = new List<PremiseDef>(e.VisiblePremises);

            // 1. Ask what hides something.
            foreach (var p in visible)
                if (e.Status(p.id) == PremiseStatus.Standing && e.TimesAsked(p.id) == 0 && HidesSomething(e, p.askReveals) && e.Ask(p.id))
                    return Did("asked about " + p.label);
            // 2. Concede what's true (key premises are never true: they're there to break).
            foreach (var p in visible)
                if (e.Status(p.id) == PremiseStatus.Standing && p.truth != PremiseTruth.False && !p.key && e.Concede(p.id))
                    return Did("conceded " + p.label);
            // 3. Break what can be broken, with the sound reason.
            foreach (var p in visible)
            {
                if (e.Status(p.id) != PremiseStatus.Standing || p.unbreakable) continue;
                foreach (var c in p.challenges)
                {
                    if (c == null || !e.HasEvidence(c.evidence)) continue;
                    var sound = Sound(c);
                    if (sound == null) continue;
                    if (e.BeginChallenge(p.id, c.evidence, _cards) != ChallengeStep.Reasons) continue;
                    if (e.ResolveChallenge(p.id, c.evidence, sound)) return Did("broke " + p.label + " with " + c.evidence);
                }
            }
            // 4. Ask again where that reveals the deepest one.
            foreach (var p in visible)
            {
                var s = e.Status(p.id);
                if ((s == PremiseStatus.Standing || s == PremiseStatus.Conceded) && e.TimesAsked(p.id) == 1 && HidesSomething(e, p.askAgainReveals) && e.Ask(p.id))
                    return Did("asked " + p.label + " again");
            }
            // 5. Hear out anything not yet heard.
            foreach (var p in visible)
                if (!e.IsHeard(p.id) && e.Ask(p.id)) return Did("asked about " + p.label);
            // 6. Nothing left to argue.
            if (e.CanLetItRest && e.LetItRest()) return Did("let it rest");
            return false;
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

        static ReasonDef Sound(ChallengeDef c)
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

        bool Did(string what)
        {
            _lastAction = what;
            return true;
        }
    }
}
