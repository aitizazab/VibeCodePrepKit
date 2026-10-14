using JamKit.SkylineRepo.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the hostile-hue lint (docs/SKYLINE-STYLE.md rule 2, docs/SKYLINE-PLAN-FLOOR1.md Task S0): the hostile
    /// red-magenta #FF2E63 is reserved, so a scenery material or light in that band is a problem line, and the existing look
    /// library (the palette every scenery twin takes its colour from) is clean.
    /// </summary>
    public class SkylineHueLintTests
    {
        static Color Hex(int rgb) => new Color32((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), 255);

        [TestCase(0xFF2E63, true)]    // the hostile hue itself
        [TestCase(0xFF3366, true)]
        [TestCase(0xE8284F, true)]    // a bit redder, still in the band
        [TestCase(0xFF2EA0, true)]    // towards magenta, within 20 degrees
        [TestCase(0xE9EDF2, false)]   // plaster
        [TestCase(0xCDBB9C, false)]   // cream floor
        [TestCase(0xB36BFF, false)]   // the objective violet
        [TestCase(0x8FD3FF, false)]   // the wreck light blue
        [TestCase(0xFFD60A, false)]   // the weak-spot yellow
        [TestCase(0xF07A2A, false)]   // hazard orange
        [TestCase(0xF0D5DA, false)]   // blush plaster: right hue, too pale (saturation under 0.4)
        [TestCase(0xFF0000, true)]    // pure red (hue 0) is 15 degrees from the hostile hue
        public void TheBandIsTwentyDegreesAtOverFortyPercentSaturation(int rgb, bool inBand)
        {
            Assert.That(SkylineHueLint.InHostileBand(Hex(rgb)), Is.EqualTo(inBand), rgb.ToString("X6"));
        }

        [Test]
        public void TheHostileHueIsAboutThreeFortyFiveDegrees() =>
            Assert.That(SkylineHueLint.HostileHue, Is.EqualTo(344.8f).Within(1f));

        [Test]
        public void AnHdrEmissionIsJudgedByItsHue() =>
            Assert.That(SkylineHueLint.InHostileBand(new Color(4f, 0.74f, 1.55f)), Is.True);

        [Test]
        public void ABlackOrAnUnsaturatedColourIsNeverInTheBand()
        {
            Assert.That(SkylineHueLint.InHostileBand(Color.black), Is.False);
            Assert.That(SkylineHueLint.InHostileBand(Color.white), Is.False);
            Assert.That(SkylineHueLint.InHostileBand(Hex(0x8E8E8E)), Is.False);
        }

        [Test]
        public void ASyntheticHostileSignIsFlaggedAndAPlainOneIsNot()
        {
            var scene = SceneManager.GetActiveScene();
            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "S0_TestHostileSign";
            var fine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fine.name = "S0_TestPlainSign";
            var lightGo = new GameObject("S0_TestHostileLight");
            var shader = Shader.Find(SkylineInkRoles.ShaderName);
            var bad = new Material(shader) { name = "S0_TestBad" };
            bad.SetColor("_BaseColor", Hex(0xFF2E63));
            var good = new Material(shader) { name = "S0_TestGood" };
            good.SetColor("_BaseColor", Hex(0xE9EDF2));
            try
            {
                sign.GetComponent<Renderer>().sharedMaterial = bad;
                fine.GetComponent<Renderer>().sharedMaterial = good;
                var l = lightGo.AddComponent<Light>();
                l.color = Hex(0xFF2E63);

                var lines = SkylineHueLint.Run(scene);
                Assert.That(lines.Exists(s => s.Contains("S0_TestBad")), Is.True, "the #FF2E63 sign: " + string.Join(" | ", lines));
                Assert.That(lines.Exists(s => s.Contains("S0_TestGood")), Is.False, "the plaster sign");
                Assert.That(lines.Exists(s => s.Contains("S0_TestHostileLight")), Is.True, "the red-magenta light");
            }
            finally
            {
                Object.DestroyImmediate(sign);
                Object.DestroyImmediate(fine);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(bad);
                Object.DestroyImmediate(good);
            }
        }

        [Test]
        public void AGlowingEmissionInTheBandIsFlagged()
        {
            var m = new Material(Shader.Find(SkylineInkRoles.ShaderName)) { name = "S0_TestNeon" };
            try
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(3f, 0.55f, 1.2f));
                Assert.That(SkylineHueLint.CheckMaterial(m, "a shop sign"), Has.Count.EqualTo(1));
                m.SetColor("_EmissionColor", new Color(0.3f, 2.1f, 1.8f));   // teal neon: fine
                Assert.That(SkylineHueLint.CheckMaterial(m, "a shop sign"), Is.Empty);
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void RoleMaterialsAreExempt()
        {
            var m = new Material(Shader.Find(SkylineInkRoles.ShaderName));
            try
            {
                m.SetColor("_BaseColor", Hex(0xFF2E63));
                SkylineInkRoles.SetRole(m, SkylineInkRoles.Hostile);
                Assert.That(SkylineHueLint.CheckMaterial(m, "a bot"), Is.Empty, "a hostile bot's paint is meant to be that hue");
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void TheExistingLookLibraryIsClean()
        {
            // The scenery twins take their colour from the palette (SkylineInkPalette): every catalogue entry's row, and the tint a
            // look-library material starts from, must be out of the hostile band.
            foreach (var e in SkylineLookLibrary.Catalogue)
            {
                Assert.That(SkylineHueLint.InHostileBand(e.tint), Is.False, "catalogue tint of " + e.name);
                if (SkylineInkPalette.TryGet("SKL_" + e.name, out var row) && !row.keepTexture && !row.emissive)
                    Assert.That(SkylineHueLint.InHostileBand(row.color), Is.False, "palette row of " + e.name);
            }
            foreach (var family in SkylineLookLibrary.FacadeKinds)
                for (int band = 0; band <= 2; band++)
                    if (SkylineInkPalette.TryGet("SKL_Facade_" + family + "_Band" + band + "_x", out var row))
                        Assert.That(SkylineHueLint.InHostileBand(row.color), Is.False, "facade " + family + " band " + band);
        }
    }
}
