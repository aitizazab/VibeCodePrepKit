using System;
using System.IO;
using JamKit.Combat;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class SkylineGunsTests
    {
        /// <summary>A repo path, found by walking up from the test's folder (dotnet) or the project folder (Unity); null when not reachable.</summary>
        static string RepoPath(string relative)
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                for (int i = 0; dir != null && i < 10; i++, dir = dir.Parent)
                {
                    string p = Path.Combine(dir.FullName, relative);
                    if (Directory.Exists(p) || File.Exists(p)) return p;
                }
            }
            return null;
        }

        [Test]
        public void BothGunsAreComplete()
        {
            foreach (var g in SkylineGuns.All()) Assert.That(g.Validate(), Is.Null, g.id);
        }

        [Test]
        public void AimDownSightsNumbersAreInSaneRanges()
        {
            foreach (var g in SkylineGuns.All())
            {
                Assert.That(g.aimFovScale, Is.InRange(0.5f, 1f), g.id + " aimFovScale");
                Assert.That(g.aimLookScale, Is.InRange(0.3f, 1f), g.id + " aimLookScale");
                Assert.That(g.aimMoveScale, Is.InRange(0.5f, 1f), g.id + " aimMoveScale");
                Assert.That(g.aimSpreadScale, Is.InRange(0.5f, 1f), g.id + " aimSpreadScale");
                Assert.That(g.Validate(), Is.Null);
            }
            var writ = SkylineGuns.Writ();
            var breacher = SkylineGuns.Breacher();
            Assert.That(writ.aimFovScale, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(breacher.aimFovScale, Is.EqualTo(0.88f).Within(1e-5f));
            Assert.That(writ.aimLookScale, Is.EqualTo(0.65f).Within(1e-5f));
            Assert.That(breacher.aimLookScale, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(writ.aimMoveScale, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(breacher.aimMoveScale, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(breacher.aimSpreadScale, Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void AimNumbersOutOfRangeAreRefused()
        {
            var g = SkylineGuns.Breacher();
            g.aimFovScale = 0.2f;
            Assert.That(g.Validate(), Does.Contain("aimFovScale"));
            g = SkylineGuns.Breacher();
            g.aimSpreadScale = 1.5f;
            Assert.That(g.Validate(), Does.Contain("aimSpreadScale"));
        }

        [Test]
        public void EverySoundBelongsToItsGun()
        {
            foreach (var g in SkylineGuns.All())
                foreach (var s in ForgeExport.Sounds(g)) Assert.That(s, Does.StartWith(g.id + "/"));
        }

        [Test]
        public void TheWritIsAPreciseSidearmWithRoomToBreathe()
        {
            var w = SkylineGuns.Writ();
            Assert.That(w.pellets, Is.EqualTo(1));
            Assert.That(w.spreadDeg, Is.EqualTo(0f));
            Assert.That(w.weapon.fireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(60f / w.weapon.fireInterval, Is.LessThanOrEqualTo(300f), "an RPM low enough that the visuals breathe (2.3)");
            Assert.That(w.weakPointMultiplier, Is.GreaterThan(1f));
        }

        [Test]
        public void TheBreacherIsAKnockbackShotgun()
        {
            var b = SkylineGuns.Breacher();
            var w = SkylineGuns.Writ();
            Assert.That(b.pellets, Is.GreaterThanOrEqualTo(6));
            Assert.That(b.spreadDeg, Is.GreaterThan(0f));
            Assert.That(b.enemyKnockback, Is.GreaterThan(w.enemyKnockback * 4f));
            Assert.That(b.range, Is.LessThan(w.range));
            Assert.That(b.kick.pitchDeg, Is.GreaterThan(w.kick.pitchDeg));
        }

        [Test]
        public void AReloadFromEmptyTakesLongerAndDoesMore()
        {
            foreach (var g in SkylineGuns.All())
            {
                var empty = g.Clip(HandlingClipKind.ReloadEmpty);
                var partial = g.Clip(HandlingClipKind.ReloadPartial);
                Assert.That(empty.seconds, Is.GreaterThan(partial.seconds), g.id);
                Assert.That(empty.events.Length, Is.GreaterThan(partial.events.Length), g.id + ": the bolt or pump");
            }
        }

        [Test]
        public void TheWritsReloadIsTheQuillWritingOnPaperWithTheHandsStillAndTheGunTiltedUp()
        {
            var w = SkylineGuns.Writ();
            foreach (var kind in new[] { HandlingClipKind.ReloadPartial, HandlingClipKind.ReloadEmpty })
            {
                var c = w.Clip(kind);
                int Index(string id) { for (int i = 0; i < c.events.Length; i++) if (c.events[i].id == id) return i; return -1; }
                int lift = Index("lift"), ret = Index("return"), seat = Index("seat");
                Assert.That(new[] { lift, ret, seat }, Is.All.GreaterThanOrEqualTo(0), kind.ToString());
                Assert.That(lift, Is.LessThan(ret)); Assert.That(ret, Is.LessThan(seat));
                // the gavel, the drum and the bolt are gone (the creator, 2026-10-14)
                foreach (var gone in new[] { "windup", "smack", "mag_out", "cell_in", "bolt", "cell_out" })
                    Assert.That(Index(gone), Is.EqualTo(-1), kind + " has no " + gone);
                Assert.That(c.events[seat].sound, Is.EqualTo("writ/seat"), "the ink is full: a brass click");
                Assert.That(ForgeExport.Sounds(w), Does.Not.Contain("writ/smack"));
                float writing = (c.events[ret].at - c.events[lift].at) * c.seconds;
                Assert.That(writing, Is.InRange(0.6f, 1.5f), "time to leave the well, write three strokes and come back");
                float seatGap = (c.events[seat].at - c.events[ret].at) * c.seconds;
                Assert.That(seatGap, Is.InRange(0.03f, 0.15f), "the ink seats a moment after the quill is back");
                // the hands stay still: nothing is carried (every frame's magOffset is zero), and the gun pitches UP, not down
                foreach (var f in c.pose) Assert.That(f.magOffset.Length(), Is.LessThan(1e-6f), kind + " frame " + f.at);
                float up = 0f;
                foreach (var f in c.pose)
                {
                    Assert.That(f.gunEuler.X, Is.LessThanOrEqualTo(0f), kind + " frame " + f.at + ": the muzzle is never pitched down");
                    Assert.That(Math.Abs(f.gunEuler.Z), Is.LessThan(5f), "no roll: it is not turned over to show a drum");
                    up = Math.Min(up, f.gunEuler.X);
                }
                Assert.That(up, Is.InRange(-30f, -8f), "slightly up");
                // up for the reload's duration, then settled back
                for (float p = 0.15f; p < c.events[seat].at; p += 0.02f)
                    Assert.That(c.SamplePose(p).gunEuler.X, Is.LessThan(-8f), kind + " at " + p);
                var end = c.SamplePose(1f);
                Assert.That(end.gunEuler.Length(), Is.LessThan(1e-4f));
                Assert.That(end.gunOffset.Length(), Is.LessThan(1e-6f));
            }
            Assert.That(w.Clip(HandlingClipKind.ReloadPartial).seconds, Is.EqualTo(1.3f), "the reload's total time is unchanged");
            Assert.That(w.Clip(HandlingClipKind.ReloadEmpty).seconds, Is.EqualTo(1.75f));
            Assert.That(w.Clip(HandlingClipKind.ReloadEmpty).events[w.Clip(HandlingClipKind.ReloadEmpty).events.Length - 1].id, Is.EqualTo("flourish"),
                "from empty it ends with a flourish of the quill");
        }

        [Test]
        public void TheWritsShotIsASingleQuietThwipWithNoHydraulicReturn()
        {
            var fire = SkylineGuns.Writ().Clip(HandlingClipKind.Fire);
            Assert.That(ForgeExport.Sounds(SkylineGuns.Writ()), Does.Not.Contain("writ/shot_mech"));
            int sounds = 0;
            foreach (var e in fire.events) if (!string.IsNullOrEmpty(e.sound)) sounds++;
            Assert.That(sounds, Is.EqualTo(1), "the thwip alone");
        }

        [Test]
        public void FindReturnsAFreshCopy()
        {
            var a = SkylineGuns.Find("writ");
            a.damage = 1f;
            Assert.That(SkylineGuns.Find("writ").damage, Is.Not.EqualTo(1f));
            Assert.That(SkylineGuns.Find("nope"), Is.Null);
        }

        [Test]
        public void AClipBecomesAForgeTimeline()
        {
            var clip = new HandlingClip(HandlingClipKind.Fire, 0.22f,
                new[] { new HandlingEvent(0f, "shot", "writ/shot"), new HandlingEvent(0.3f, "anim", null), new HandlingEvent(0.45f, "mech", "writ/shot_mech") }, null);
            Assert.That(ForgeExport.TimelineJson(clip), Is.EqualTo(
                "{\n  \"duration\": 0.22,\n  \"events\": [\n" +
                "    {\"at\": 0, \"recipe\": \"recipes/shot.json\"},\n" +
                "    {\"at\": 0.45, \"recipe\": \"recipes/shot_mech.json\"}\n  ]\n}\n"));
            Assert.That(ForgeExport.FileName(clip), Is.EqualTo("Fire.timeline.json"));
        }

        [Test]
        public void TheForgeFilesInTheRepoMatchTheGunData()
        {
            string guns = RepoPath("tools/skyline/guns");
            if (guns == null) Assert.Ignore("tools/skyline/guns isn't reachable from here");
            foreach (var g in SkylineGuns.All())
            {
                foreach (var c in g.clips)
                {
                    string path = Path.Combine(guns, g.id, ForgeExport.FileName(c));
                    Assert.That(File.Exists(path), Is.True, path + " (run WriteTheForgeTimelines)");
                    Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(ForgeExport.TimelineJson(c)), path + " is stale (run WriteTheForgeTimelines)");
                }
                foreach (var s in ForgeExport.Sounds(g))
                    Assert.That(File.Exists(Path.Combine(guns, g.id, "recipes", ForgeExport.Name(s) + ".json")), Is.True, "no recipe for " + s);
            }
        }

        [Test, Explicit("writes tools/skyline/guns/<gun>/<Clip>.timeline.json from the gun data; run after changing a clip")]
        public void WriteTheForgeTimelines()
        {
            string guns = RepoPath("tools/skyline/guns");
            Assert.That(guns, Is.Not.Null, "create tools/skyline/guns first (firstcut.py)");
            foreach (var g in SkylineGuns.All())
            {
                string dir = Path.Combine(guns, g.id);
                Directory.CreateDirectory(dir);
                foreach (var c in g.clips) File.WriteAllText(Path.Combine(dir, ForgeExport.FileName(c)), ForgeExport.TimelineJson(c));
            }
        }
    }
}
