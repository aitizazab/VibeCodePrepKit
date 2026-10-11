using System.Numerics;
using JamKit.Stealth;
using NUnit.Framework;

namespace JamKit.Tests.Stealth
{
    /// <summary>Being caught (docs/STEALTH.md "Being caught"): checkpoints are taken only when no guard is chasing, a
    /// restart puts back only what was picked up since the checkpoint, the caught beat runs beat, fade out, restart at
    /// black, fade in (or beat, then the fail), and the yard's checkpoints are safe places to come back to.</summary>
    public class StealthCheckpointTests
    {
        static CheckpointLog TwoPoints()
        {
            var log = new CheckpointLog();
            log.points.Add(new StealthCheckpoint { name = "Start", center = new Vector3(0f, 1.5f, 0f), spawn = Vector3.Zero });
            log.points.Add(new StealthCheckpoint { name = "Far", center = new Vector3(20f, 1.5f, 0f), spawn = new Vector3(20f, 0f, 0f), yaw = 90f });
            return log;
        }

        static CollectibleSet Three()
        {
            var items = new CollectibleSet();
            items.Register("a");
            items.Register("b");
            items.Register("c");
            return items;
        }

        [Test]
        public void ACheckpointIsTakenOnlyWhenNotChasedAndBanksWhatYouHave()
        {
            var log = TwoPoints();
            var items = Three();
            log.Reset(items);
            Assert.That(log.Current, Is.EqualTo(0));
            items.Collect(0);
            var far = new Vector3(20f, 0f, 1f);
            Assert.That(log.Tick(far, true, items), Is.EqualTo(-1), "never mid-chase");
            Assert.That(log.Current, Is.EqualTo(0));
            Assert.That(log.Tick(far, false, items), Is.EqualTo(1));
            Assert.That(log.Tick(far, false, items), Is.EqualTo(-1), "once, not every frame inside it");
            Assert.That(log.CurrentPoint.yaw, Is.EqualTo(90f));
            Assert.That(log.Banked(0), Is.True);
            Assert.That(log.Banked(1), Is.False);
            Assert.That(log.Tick(new Vector3(20f, 4f, 0f), false, items), Is.EqualTo(-1), "above the box isn't in it");
        }

        [Test]
        public void ARestartPutsBackOnlyWhatWasPickedUpSinceTheCheckpoint()
        {
            var log = TwoPoints();
            var items = Three();
            log.Reset(items);
            items.Collect(0);
            log.Tick(new Vector3(20f, 0f, 0f), false, items);
            items.Collect(1);
            items.Collect(2);
            int all = 0;
            items.AllCollected += () => all++;
            Assert.That(log.Restore(items), Is.EqualTo(2));
            Assert.That(items.IsCollected(0), Is.True, "banked at the checkpoint");
            Assert.That(items.IsCollected(1) || items.IsCollected(2), Is.False);
            Assert.That(items.Remaining, Is.EqualTo(2));
            items.Collect(1);
            items.Collect(2);
            Assert.That(all, Is.EqualTo(1), "collecting them again still announces the last");
            log.Reset(items);
            Assert.That(log.Current, Is.EqualTo(0));
            Assert.That(log.Restore(items), Is.EqualTo(0));
        }

        [Test]
        public void TheCaughtBeatFreezesFadesRestartsAtBlackAndFadesBack()
        {
            var beat = new CaughtBeat { beat = 0.8f, fadeOut = 0.4f, fadeIn = 0.4f };
            beat.Start(CaughtOutcome.RestartCheckpoint);
            float t = 0f;
            int black = -1, done = -1, frame = 0;
            float fadeAtBlack = 0f, fadeDuringBeat = 0f;
            while (beat.Active && frame < 1000)
            {
                var step = beat.Tick(0.05f);
                t += 0.05f;
                frame++;
                if (t < 0.75f) fadeDuringBeat = System.Math.Max(fadeDuringBeat, beat.Fade);
                if (step == CaughtStep.Black) { black = frame; fadeAtBlack = beat.Fade; }
                if (step == CaughtStep.Done) done = frame;
                Assert.That(step, Is.Not.EqualTo(CaughtStep.Failed));
            }
            Assert.That(fadeDuringBeat, Is.EqualTo(0f), "the beat itself is clear: you see who caught you");
            Assert.That(black * 0.05f, Is.EqualTo(1.2f).Within(0.051f), "the restart at full black, after the beat and the fade out");
            Assert.That(fadeAtBlack, Is.GreaterThan(0.95f));
            Assert.That((done - black) * 0.05f, Is.EqualTo(0.4f).Within(0.051f), "then the fade back in");
            Assert.That(beat.Fade, Is.EqualTo(0f));
        }

        [Test]
        public void WhenTheSubquestFailsTheBeatEndsInTheFailWithNoFade()
        {
            var beat = new CaughtBeat();
            beat.Start(CaughtOutcome.FailSubquest);
            var last = CaughtStep.None;
            float maxFade = 0f;
            for (int i = 0; i < 200 && beat.Active; i++)
            {
                last = beat.Tick(1f / 60f);
                maxFade = System.Math.Max(maxFade, beat.Fade);
            }
            Assert.That(last, Is.EqualTo(CaughtStep.Failed));
            Assert.That(maxFade, Is.EqualTo(0f));
            beat.Start(CaughtOutcome.RestartCheckpoint);
            beat.Cancel();
            Assert.That(beat.Tick(1f), Is.EqualTo(CaughtStep.None), "cancelled (a manual restart)");
        }

        [Test]
        public void TheYardsCheckpointsAreOpenGroundNoGuardSeesFromHome()
        {
            var yard = StealthYard.Build();
            var world = yard.World();
            var def = new StealthDef();
            Assert.That(yard.checkpoints.Count, Is.EqualTo(3));
            Assert.That(yard.checkpoints[0].spawn, Is.EqualTo(yard.playerStart), "the first is the start");
            var homes = new[] { yard.seekerStart, yard.guard2Start, yard.guard3Start };
            foreach (var c in yard.checkpoints)
            {
                Assert.That(c.Contains(c.spawn), Is.True, c.name + ": its spawn is inside its own box");
                foreach (var p in yard.pieces)
                {
                    if (!p.Solid || p.kind == YardPieceKind.Floor) continue;
                    var chest = c.spawn + new Vector3(0f, 1f, 0f);
                    bool inside = chest.X > p.Min.X && chest.X < p.Max.X && chest.Y > p.Min.Y && chest.Y < p.Max.Y && chest.Z > p.Min.Z && chest.Z < p.Max.Z;
                    Assert.That(inside, Is.False, c.name + " is inside " + p.name);
                }
                foreach (var home in homes)
                {
                    var eye = home + new Vector3(0f, def.eyeHeight, 0f);
                    var chest = c.spawn + new Vector3(0f, 1f, 0f);
                    bool seen = Vector3.Distance(eye, chest) <= def.sightRange && world.LineClear(eye, chest);
                    Assert.That(seen, Is.False, c.name + " is in sight of a guard's home at " + home);
                }
            }
        }
    }
}
