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
