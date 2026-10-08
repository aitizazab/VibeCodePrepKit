using JamKit.Flow;
using NUnit.Framework;

namespace JamKit.Tests.Flow
{
    /// <summary>SaveData.entries: a template's own data (the bullet hell's remnants) as key + text.</summary>
    public class SaveEntryTests
    {
        [Test]
        public void SaveEntriesRoundTrip()
        {
            var save = new SaveData();
            Assert.That(save.GetEntry("bh_remnants"), Is.Null);
            save.SetEntry("bh_remnants", "{}");
            save.SetEntry("bh_remnants", "{\"a\":1}");
            Assert.That(save.entries.Count, Is.EqualTo(1));
            Assert.That(save.GetEntry("bh_remnants"), Is.EqualTo("{\"a\":1}"));
            save.SetEntry("bh_remnants", null);
            Assert.That(save.entries, Is.Empty);
            save.entries = null;
            Assert.That(save.Migrate(), Is.True);
            Assert.That(save.entries, Is.Not.Null);
        }
    }
}
