using System.Collections.Generic;
using System.Linq;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task M1: the section table, the sequencer and the lift-ride plan (docs/SKYLINE-PLAN-FLOOR1.md,
    /// docs/SKYLINE-MUSIC.md; the creator's numbers of 2026-10-14: C, 122.5 BPM, a one-bar riser, a 6-beat pre-drop).</summary>
    public class MusicSequencerTests
    {
        const double Beat = 60.0 / 122.5, Bar = 4 * Beat;
        const double Tol = 1e-9;

        static MusicSequencer Muzak(double start = 0)
        {
            var s = new MusicSequencer(MusicSections.Placeholders());
            s.Start(MusicSections.Muzak, start);
            return s;
        }

        static List<MusicChange> Drain(MusicSequencer s, double now)
        {
            var all = new List<MusicChange>();
            MusicChange c;
            while (s.Tick(now, out c)) all.Add(c);
            return all;
        }

        // ---- the table is the creator's ----

        [Test]
        public void EverySectionRunsAt122AndAHalfBpmInC()
        {
            var defs = MusicSections.Placeholders();
            Assert.That(defs.Select(d => d.id), Is.EqualTo(new[] { "muzak", "riser", "pre-drop", "drop", "main", "calm" }));
            foreach (var d in defs)
            {
                Assert.That(d.bpm, Is.EqualTo(122.5), d.id);
                Assert.That(d.beatsPerBar, Is.EqualTo(4), d.id);
            }
            Assert.That(defs.Where(d => d.loops).Select(d => d.id), Is.EqualTo(new[] { "muzak", "main", "calm" }),
                "the lobby muzak, the main song and the calm loop loop; the set-piece does not");
            Assert.That(defs.Single(d => d.id == "muzak").Seconds, Is.EqualTo(8 * Bar).Within(Tol));
        }

        [Test]
        public void TheRiserIsOneBarOfTheSixStepWalkUpOnTheSixteenths()
        {
            var riser = MusicSections.Placeholders().Single(d => d.id == MusicSections.Riser);
            Assert.That(riser.Seconds, Is.EqualTo(Bar).Within(Tol));
            var cues = MusicSections.Cues(MusicSections.Riser);
            Assert.That(cues.Select(c => c.beat * 4), Is.EqualTo(new double[] { 0, 4, 8, 11, 14, 16 }), "sixteenths 0, 4, 8, 11, 14, 16");
            Assert.That(cues.Select(c => c.midi), Is.EqualTo(new[] { 60, 64, 67, 69, 70, 72 }), "C E G A Bb C");
            Assert.That(cues.Select(c => c.midi % 12), Is.EqualTo(new[] { 0, 4, 7, 9, 10, 0 }));
            Assert.That(cues.Last().kind, Is.EqualTo("landing"));
            Assert.That(cues.Last().beat, Is.EqualTo(riser.Beats), "the landing is where the riser ends");
        }

        [Test]
        public void ThePreDropIsFourDottedQuartersWithTheLastHalfBeatEmpty()
        {
            var pre = MusicSections.Placeholders().Single(d => d.id == MusicSections.PreDrop);
            Assert.That(pre.Beats, Is.EqualTo(6.0));
            Assert.That(pre.Seconds, Is.EqualTo(6 * Beat).Within(Tol));
            var cues = MusicSections.Cues(MusicSections.PreDrop);
            var hits = cues.Where(c => c.kind != "gap").ToArray();
            Assert.That(hits.Select(c => c.beat), Is.EqualTo(new[] { 0.0, 1.5, 3.0, 4.5 }), "dotted quarters");
            Assert.That(hits.Take(3).Select(c => c.kind), Is.All.EqualTo("wub"), "three held wub notes");
            Assert.That(hits[3].kind, Is.EqualTo("dive"), "the fourth dives");
            Assert.That(hits[3].midi, Is.LessThan(hits[0].midi));
            Assert.That(hits[3].beat + hits[3].lengthBeats, Is.EqualTo(5.5), "it stops half a beat short");
            var gap = cues.Single(c => c.kind == "gap");
            Assert.That(gap.beat, Is.EqualTo(5.5));
            Assert.That(gap.beat + gap.lengthBeats, Is.EqualTo(pre.Beats), "the gap is the last half-beat, then the drop");
        }

        [Test]
        public void TheSectionChainRunsRiserPreDropDropMain()
        {
            var defs = MusicSections.Placeholders().ToDictionary(d => d.id);
            Assert.That(defs["riser"].next, Is.EqualTo("pre-drop"));
            Assert.That(defs["pre-drop"].next, Is.EqualTo("drop"));
            Assert.That(defs["drop"].next, Is.EqualTo("main"));
            Assert.That(defs["drop"].Seconds, Is.EqualTo(Bar).Within(Tol));
        }

        // ---- requests land on bar lines ----

        [Test]
        public void ARequestMidBarGoesToTheNextBarLine()
        {
            var s = Muzak(1000.0);
            double now = 1000.0 + 3 * Bar + 0.3 * Bar;
            Assert.That(s.Request(MusicSections.MainLoop, now), Is.EqualTo(1000.0 + 4 * Bar).Within(Tol));
            Assert.That(s.Pending, Is.EqualTo("main"));
            Assert.That(s.PendingAt, Is.EqualTo(1000.0 + 4 * Bar).Within(Tol));
        }

        [Test]
        public void ARequestOnABarLineGoesToTheNextOneOnlyInsideTheMinimumLead()
        {
            var s = Muzak();
            // exactly on a bar line: the line is now, inside the lead, so the next one
            Assert.That(s.Request(MusicSections.CalmLoop, 3 * Bar), Is.EqualTo(4 * Bar).Within(Tol));
            // a bar line a full lead away is still that bar line
            Assert.That(s.Request(MusicSections.CalmLoop, 4 * Bar - MusicSequencer.MinLead), Is.EqualTo(4 * Bar).Within(Tol));
            // a hair inside the lead goes to the next
            Assert.That(s.Request(MusicSections.CalmLoop, 4 * Bar - MusicSequencer.MinLead + 0.001), Is.EqualTo(5 * Bar).Within(Tol));
        }

        [Test]
        public void ARequestNearTheLoopsEndLandsOnTheNextPassGrid()
        {
            var s = Muzak();
            double loop = 8 * Bar;
            Assert.That(s.Request(MusicSections.MainLoop, loop - 0.1), Is.EqualTo(loop + Bar).Within(Tol));
            Assert.That(s.Request(MusicSections.MainLoop, loop - 0.5), Is.EqualTo(loop).Within(Tol));
        }

        [Test]
        public void ALaterRequestReplacesAPendingOne()
        {
            var s = Muzak();
            s.Request(MusicSections.MainLoop, 0.5 * Bar);
            double at = s.Request(MusicSections.CalmLoop, 0.6 * Bar);
            Assert.That(s.Pending, Is.EqualTo("calm"));
            Assert.That(s.PendingAt, Is.EqualTo(at));
            var changes = Drain(s, 100.0);
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].to, Is.EqualTo("calm"));
        }

        [Test]
        public void UnknownSectionsAndAnUnstartedSequencerAreRefused()
        {
            var s = new MusicSequencer(MusicSections.Placeholders());
            Assert.Throws<System.InvalidOperationException>(() => s.Request("main", 0));
            s.Start("muzak", 0);
            Assert.Throws<System.ArgumentException>(() => s.Request("nope", 0));
            Assert.Throws<System.ArgumentException>(() => s.Start("nope", 0));
            Assert.Throws<System.ArgumentException>(() => new MusicSequencer(new[] { new MusicSectionDef { id = "a", bars = 1, next = "ghost" } }));
        }

        // ---- no drift ----

        [Test]
        public void AHundredLoopsOfTheMuzakDoNotDrift()
        {
            double start = 36000.123456789;   // hours into the audio clock
            var s = Muzak(start);
            double len = 8 * Bar;
            // ticked every 0.5 s through 100 passes: the pass start is start + 100 x length
            for (double t = start; t <= start + 100 * len + 0.25; t += 0.5)
            {
                MusicChange c;
                Assert.That(s.Tick(t, out c), Is.False);
            }
            Assert.That(s.CurrentStart, Is.EqualTo(start + 100 * len).Within(1e-9));
            Assert.That(s.SectionStart, Is.EqualTo(start), "the section itself began once");
            // a request after all those loops still lands exactly on the grid
            double now = start + 100 * len + 2.5 * Bar;
            Assert.That(s.Request(MusicSections.MainLoop, now), Is.EqualTo(start + 100 * len + 3 * Bar).Within(1e-9));
        }

        // ---- tempo per section ----

        [Test]
        public void ARequestQuantisesToTheSectionItIsMadeIn()
        {
            var defs = new[]
            {
                new MusicSectionDef { id = "slow", bpm = 86, bars = 2, loops = true },
                new MusicSectionDef { id = "fast", bpm = 122.5, bars = 2, loops = true },
            };
            var s = new MusicSequencer(defs);
            s.Start("slow", 10.0);
            double slowBar = 4 * 60.0 / 86;
            Assert.That(s.Request("fast", 10.0 + 0.4 * slowBar), Is.EqualTo(10.0 + slowBar).Within(Tol));
            Drain(s, 10.0 + slowBar);
            Assert.That(s.Current, Is.EqualTo("fast"));
            double fastStart = 10.0 + slowBar;
            Assert.That(s.Request("slow", fastStart + 0.4 * Bar), Is.EqualTo(fastStart + Bar).Within(Tol), "now on the 122.5 grid");
        }

        // ---- the lift ride ----

        [Test]
        public void TheLiftPlanForFiftyTriggerTimesAcrossABar()
        {
            for (int i = 0; i < 50; i++)
            {
                double start = 5000.0;
                var s = Muzak(start);
                double now = start + 2 * Bar + i * Bar / 50.0;
                var p = s.PlanLiftRide(now);
                var riser = s.Def(MusicSections.Riser);
                var pre = s.Def(MusicSections.PreDrop);
                Assert.That(p.preDropAt, Is.EqualTo(p.riserAt + riser.Seconds).Within(Tol));
                Assert.That(p.dropAt, Is.EqualTo(p.riserAt + riser.Seconds + pre.Seconds).Within(Tol), "exactly riser + pre-drop");
                Assert.That(p.dropAt - p.riserAt, Is.EqualTo(10 * Beat).Within(Tol), "1 bar + 6 beats");
                Assert.That(p.doorsStartOpeningAt, Is.EqualTo(p.dropAt - MusicSequencer.DoorLead).Within(Tol));
                Assert.That(p.doorsClosedBy, Is.EqualTo(p.riserAt));
                Assert.That(p.mainAt, Is.EqualTo(p.dropAt + Bar).Within(Tol));
                // the riser starts on a bar line of the muzak, within a bar (plus the lead) of stepping in
                double bars = (p.riserAt - start) / Bar;
                Assert.That(bars, Is.EqualTo(System.Math.Round(bars)).Within(1e-6), "on the muzak's bar line, i=" + i);
                Assert.That(p.riserAt - now, Is.GreaterThanOrEqualTo(MusicSequencer.MinLead - Tol));
                Assert.That(p.riserAt - now, Is.LessThan(Bar + MusicSequencer.MinLead), "the wait is one bar at most (plus the lead)");
            }
        }

        [Test]
        public void TheDropDoesNotLandOnTheRisersDownbeatGrid()
        {
            var s = Muzak();
            var p = s.PlanLiftRide(1.0);
            double barsFromRiser = (p.dropAt - p.riserAt) / Bar;
            Assert.That(barsFromRiser, Is.EqualTo(2.5).Within(1e-9), "two bars and a half: the main song re-bars from the drop");
            Assert.That(p.dropAt - p.riserAt, Is.Not.EqualTo(System.Math.Round(barsFromRiser) * Bar).Within(0.1));
        }

        [Test]
        public void PlanningDoesNotChangeTheSequencer()
        {
            var s = Muzak();
            s.PlanLiftRide(1.0);
            Assert.That(s.Pending, Is.Null);
            Assert.That(s.Current, Is.EqualTo("muzak"));
        }

        [Test]
        public void TheLiftRidePlaysRiserPreDropDropMainAtThePlannedTimes()
        {
            var s = Muzak(100.0);
            double now = 100.0 + 1.7 * Bar;
            var p = s.StartLiftRide(now);
            Assert.That(s.Pending, Is.EqualTo("riser"));
            Assert.That(s.PendingAt, Is.EqualTo(p.riserAt));
            // before the bar line nothing changes, the muzak keeps looping
            Assert.That(Drain(s, p.riserAt - 0.01), Is.Empty);
            Assert.That(s.Current, Is.EqualTo("muzak"));
            var all = Drain(s, p.mainAt + 0.001);
            Assert.That(all.Select(c => c.to), Is.EqualTo(new[] { "riser", "pre-drop", "drop", "main" }));
            Assert.That(all.Select(c => c.from), Is.EqualTo(new[] { "muzak", "riser", "pre-drop", "drop" }));
            Assert.That(all[0].at, Is.EqualTo(p.riserAt).Within(Tol));
            Assert.That(all[1].at, Is.EqualTo(p.preDropAt).Within(Tol));
            Assert.That(all[2].at, Is.EqualTo(p.dropAt).Within(Tol));
            Assert.That(all[3].at, Is.EqualTo(p.mainAt).Within(Tol));
            Assert.That(s.Pending, Is.Null);
        }

        [Test]
        public void TheMainSongBarsAreCountedFromTheDrop()
        {
            var s = Muzak(100.0);
            var p = s.StartLiftRide(100.0 + 0.3 * Bar);
            Drain(s, p.mainAt + 0.001);
            Assert.That(s.Current, Is.EqualTo("main"));
            Assert.That(s.SectionStart, Is.EqualTo(p.mainAt).Within(Tol));
            // a request in the main loop snaps to a grid that starts at the drop's end, not the riser's
            double now = p.mainAt + 2.4 * Bar;
            Assert.That(s.Request(MusicSections.CalmLoop, now), Is.EqualTo(p.mainAt + 3 * Bar).Within(Tol));
            Assert.That((s.PendingAt - p.riserAt) / Bar, Is.Not.EqualTo(System.Math.Round((s.PendingAt - p.riserAt) / Bar)).Within(1e-6),
                "not a bar line of the riser's grid (the drop re-bars the song)");
        }

        [Test]
        public void ARequestInsideASetPieceWaitsForItsEnd()
        {
            var s = Muzak();
            var p = s.StartLiftRide(0.5);
            Drain(s, p.riserAt + 0.01);
            Assert.That(s.Current, Is.EqualTo("riser"));
            // wrecked mid-riser: the muzak is asked for; the riser plays through, then the muzak (the chain is replaced)
            double at = s.Request(MusicSections.Muzak, p.riserAt + 0.3 * Bar);
            Assert.That(at, Is.EqualTo(p.preDropAt).Within(Tol));
            var all = Drain(s, p.preDropAt + 0.001);
            Assert.That(all.Select(c => c.to), Is.EqualTo(new[] { "muzak" }));
            Assert.That(s.Current, Is.EqualTo("muzak"));
        }

        [Test]
        public void ARequestInsideTheLeadOfASetPiecesEndLandsAfterTheSectionItHandsOverTo()
        {
            var s = Muzak();
            var p = s.StartLiftRide(0.5);
            Drain(s, p.riserAt + 0.01);
            // 0.1 s before the riser ends: the pre-drop is committed; the request lands on the pre-drop's end
            double at = s.Request(MusicSections.MainLoop, p.preDropAt - 0.1);
            Assert.That(at, Is.EqualTo(p.dropAt).Within(Tol));
            var all = Drain(s, p.dropAt + 0.001);
            Assert.That(all.Select(c => c.to), Is.EqualTo(new[] { "pre-drop", "main" }));
        }

        [Test]
        public void StartResetsAWreckBackToTheMuzak()
        {
            var s = Muzak();
            var p = s.StartLiftRide(0.5);
            Drain(s, p.dropAt + 0.01);
            s.Start(MusicSections.Muzak, p.dropAt + 1.0);
            Assert.That(s.Current, Is.EqualTo("muzak"));
            Assert.That(s.Pending, Is.Null);
            Assert.That(Drain(s, p.dropAt + 100.0), Is.Empty);
        }

        // ---- Tick ----

        [Test]
        public void TickReportsEachChangeOnceAndIsMonotonic()
        {
            var s = Muzak();
            var p = s.StartLiftRide(0.5);
            var seen = new List<MusicChange>();
            for (double t = 0; t < p.mainAt + 5; t += 0.05)
            {
                MusicChange c;
                while (s.Tick(t, out c)) seen.Add(c);
            }
            Assert.That(seen.Select(c => c.to), Is.EqualTo(new[] { "riser", "pre-drop", "drop", "main" }), "each once");
            for (int i = 1; i < seen.Count; i++) Assert.That(seen[i].at, Is.GreaterThan(seen[i - 1].at));
            // asking again, even with an earlier time, reports nothing
            MusicChange none;
            Assert.That(s.Tick(p.mainAt + 5, out none), Is.False);
            Assert.That(s.Tick(0, out none), Is.False);
        }

        [Test]
        public void AHitchThatSkipsTheWholeOpeningStillReportsEveryChangeInOrder()
        {
            var s = Muzak();
            var p = s.StartLiftRide(0.5);
            var all = Drain(s, p.mainAt + 60);
            Assert.That(all.Select(c => c.to), Is.EqualTo(new[] { "riser", "pre-drop", "drop", "main" }));
            Assert.That(s.Current, Is.EqualTo("main"));
        }

        [Test]
        public void UpcomingNamesTheNextChangeSoTheAudioCanBeScheduledAhead()
        {
            var s = Muzak();
            MusicChange up;
            Assert.That(s.Upcoming(out up), Is.False, "a looping section with nothing asked for has nothing coming");
            var p = s.StartLiftRide(0.5);
            Assert.That(s.Upcoming(out up), Is.True);
            Assert.That(up.to, Is.EqualTo("riser"));
            Assert.That(up.at, Is.EqualTo(p.riserAt));
            Drain(s, p.riserAt + 0.01);
            Assert.That(s.Upcoming(out up), Is.True);
            Assert.That(up.to, Is.EqualTo("pre-drop"), "the chain's hand-over");
            Assert.That(up.at, Is.EqualTo(p.preDropAt).Within(Tol));
        }

        [Test]
        public void BarIndexCountsBarsSinceTheSectionStarted()
        {
            var s = Muzak(50.0);
            Assert.That(s.BarIndex(50.0), Is.EqualTo(0));
            Assert.That(s.BarIndex(50.0 + 2.5 * Bar), Is.EqualTo(2));
            Assert.That(s.BarIndex(50.0 + 9.1 * Bar), Is.EqualTo(9), "loops are counted on");
            Assert.That(s.BarIndex(10.0), Is.EqualTo(0));
        }
    }
}
