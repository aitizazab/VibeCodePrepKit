using JamKit.SkylineRepo.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the comic-ink renderer feature (docs/SKYLINE-STYLE.md): it is gated by the SkylineInk override
    /// (so the shared PC renderer is untouched for the other templates), the override's defaults are the style sheet's, the
    /// composite shader compiles, and the setup installs the feature exactly once.
    /// </summary>
    public class SkylineInkFeatureTests
    {
        static SkylineInk MakeInk(bool on)
        {
            var ink = ScriptableObject.CreateInstance<SkylineInk>();
            ink.inkEnabled.Override(on);
            return ink;
        }

        [Test]
        public void ANullOverrideDoesNotRun() => Assert.That(SkylineInkFeature.ShouldRun(null), Is.False);

        [Test]
        public void AnInactiveOverrideDoesNotRun()
        {
            var ink = MakeInk(false);
            try { Assert.That(SkylineInkFeature.ShouldRun(ink), Is.False); }
            finally { Object.DestroyImmediate(ink); }
        }

        [Test]
        public void AnActiveOverrideRuns()
        {
            var ink = MakeInk(true);
            try { Assert.That(SkylineInkFeature.ShouldRun(ink), Is.True); }
            finally { Object.DestroyImmediate(ink); }
        }

        [Test]
        public void TheDefaultsAreTheStyleSheets()
        {
            var ink = ScriptableObject.CreateInstance<SkylineInk>();
            try
            {
                Assert.That(ink.inkEnabled.value, Is.False, "off by default");
                Assert.That(ink.lineWidthPx.value, Is.EqualTo(1.5f));
                Assert.That(ink.outlineWidthPx.value, Is.EqualTo(4f));
                Assert.That(ink.fadeStart.value, Is.EqualTo(20f));
                Assert.That(ink.fadeEnd.value, Is.EqualTo(140f));
                Assert.That(ink.farInk.value, Is.EqualTo(0.3f).Within(1e-5f));
                Assert.That(ink.depthThreshold.value, Is.EqualTo(0.03f).Within(1e-5f));
                Assert.That(ink.normalThreshold.value, Is.EqualTo(0.35f).Within(1e-5f));
                var c = (Color32)ink.inkColor.value;
                Assert.That(new[] { c.r, c.g, c.b }, Is.EqualTo(new byte[] { 0x16, 0x14, 0x1F }));
            }
            finally { Object.DestroyImmediate(ink); }
        }

        [Test]
        public void TheCompositeShaderIsSupported()
        {
            var shader = Shader.Find(SkylineInkSetup.CompositeShader);
            Assert.That(shader, Is.Not.Null, "Hidden/Skyline/InkComposite is not found (a compile error?)");
            Assert.That(shader.isSupported, Is.True);
        }

        [Test]
        public void InstallingTwiceLeavesExactlyOneFeature()
        {
            SkylineInkSetup.InstallFeature();
            SkylineInkSetup.InstallFeature();
            Assert.That(SkylineInkSetup.FeatureCount(), Is.EqualTo(1));
        }
    }
}
