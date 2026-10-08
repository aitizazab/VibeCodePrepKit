using JamKit.Flow;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class SettingsAndSaveTests
    {
        [Test]
        public void ClampRepairsBadValues()
        {
            var s = new Settings
            {
                masterVolume = 3f,
                musicVolume = -1f,
                sfxVolume = float.NaN,
                mouseSensitivity = 0f,
                screenShake = float.PositiveInfinity,
                difficulty = (Difficulty)42,
                damageTakenScale = 0f,
                extraLives = -3,
                aimAssist = 2f,
            };
            Assert.That(s.Clamp(), Is.True);
            Assert.That(s.masterVolume, Is.EqualTo(1f));
            Assert.That(s.musicVolume, Is.EqualTo(0f));
            Assert.That(s.sfxVolume, Is.EqualTo(new Settings().sfxVolume), "NaN goes back to the default");
            Assert.That(s.mouseSensitivity, Is.EqualTo(Settings.MinSensitivity));
            Assert.That(s.screenShake, Is.EqualTo(1f));
            Assert.That(s.difficulty, Is.EqualTo(Difficulty.Normal));
            Assert.That(s.damageTakenScale, Is.EqualTo(1f), "unknown difficulty re-applies the Normal preset");
            Assert.That(s.extraLives, Is.EqualTo(1));
            Assert.That(s.aimAssist, Is.InRange(0f, 1f));
            Assert.That(s.Clamp(), Is.False, "already clean");
        }

        [Test]
        public void ClampKeepsCustomAssists()
        {
            var s = new Settings { damageTakenScale = 0.25f, extraLives = 0 };
            Assert.That(s.Clamp(), Is.False);
            Assert.That(s.damageTakenScale, Is.EqualTo(0.25f));
        }

        [Test]
        public void DifficultyPresetsOrderSensibly()
        {
            Settings With(Difficulty d) { var s = new Settings(); s.SetDifficulty(d); return s; }
            var story = With(Difficulty.Story);
            var normal = With(Difficulty.Normal);
            var hard = With(Difficulty.Hard);
            Assert.That(story.enemyBulletSpeedScale, Is.LessThan(normal.enemyBulletSpeedScale));
            Assert.That(hard.enemyBulletSpeedScale, Is.GreaterThan(normal.enemyBulletSpeedScale));
            Assert.That(story.enemyDensityScale, Is.LessThan(normal.enemyDensityScale));
            Assert.That(story.damageTakenScale, Is.LessThan(hard.damageTakenScale));
            Assert.That(story.extraLives, Is.GreaterThan(hard.extraLives));
            Assert.That(story.aimAssist, Is.GreaterThan(hard.aimAssist));
            Assert.That(hard.aimAssist, Is.EqualTo(0f));

            var fresh = new Settings();
            var applied = fresh.Clone();
            applied.ApplyDifficulty();
            Assert.That(applied.damageTakenScale, Is.EqualTo(fresh.damageTakenScale), "defaults match the Normal preset");
            Assert.That(applied.extraLives, Is.EqualTo(fresh.extraLives));
            Assert.That(applied.aimAssist, Is.EqualTo(fresh.aimAssist));
        }

        [Test]
        public void MigrateUpgradesAnUnversionedSave()
        {
            // What a pre-versioning file looks like after loading: version 0, assists missing (0), list missing
            var save = new SaveData { version = 0, unlocked = null, runsStarted = 4 };
            save.settings = new Settings
            {
                difficulty = Difficulty.Story,
                enemyBulletSpeedScale = 0f, enemyDensityScale = 0f, damageTakenScale = 0f, extraLives = 0, aimAssist = 0f,
            };
            Assert.That(save.Migrate(), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(save.unlocked, Is.Not.Null);
            Assert.That(save.settings.extraLives, Is.EqualTo(3), "assists derived from the saved difficulty");
            Assert.That(save.settings.damageTakenScale, Is.EqualTo(0.5f));
            Assert.That(save.runsStarted, Is.EqualTo(4));
            Assert.That(save.Migrate(), Is.False, "second run is a no-op");
        }

        [Test]
        public void MigrateRepairsNullSettingsAndKeepsFutureVersion()
        {
            var save = new SaveData { version = 99, settings = null, bestFloor = -2 };
            Assert.That(save.Migrate(), Is.True);
            Assert.That(save.version, Is.EqualTo(99));
            Assert.That(save.settings, Is.Not.Null);
            Assert.That(save.bestFloor, Is.EqualTo(0));
        }

        [Test]
        public void RecordsAndUnlocks()
        {
            var save = new SaveData();
            Assert.That(save.Unlock("hat"), Is.True);
            Assert.That(save.Unlock("hat"), Is.False);
            Assert.That(save.IsUnlocked("hat"), Is.True);
            save.RecordRunStarted();
            Assert.That(save.RecordRunEnded(false, 2, 100f), Is.False);
            Assert.That(save.RecordRunEnded(true, 3, 300f), Is.True);
            Assert.That(save.RecordRunEnded(true, 3, 400f), Is.False);
            Assert.That(save.bestTime, Is.EqualTo(300f));
            Assert.That(save.bestFloor, Is.EqualTo(3));
            Assert.That(save.runsWon, Is.EqualTo(2));
            Assert.That(save.runsStarted, Is.EqualTo(1));
        }
    }
}
