using JamKit.SkylineRepo.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the comic-ink conversion (docs/SKYLINE-PLAN-STYLE.md Task 3): the palette's lookup, what a twin keeps
    /// of its Lit original (normal map, emission), what is skipped (transparent), and the ink roles with their mask pass and
    /// render queues (5 over 6 over 1).
    /// </summary>
    public class SkylineInkConvertTests
    {
        static Color32 Q(Color c) => c;

        static void AssertHex(Color actual, byte r, byte g, byte b)
        {
            var q = Q(actual);
            Assert.That(new[] { q.r, q.g, q.b }, Is.EqualTo(new[] { r, g, b }));
        }

        [Test]
        public void TheLongestPrefixWins()
        {
            Assert.That(SkylineInkPalette.TryGet("SKL_Plaster_White__Mint", out var mint), Is.True);
            Assert.That(mint.color.g, Is.GreaterThan(mint.color.r), "mint (green over red), not the plain white");
            Assert.That(SkylineInkPalette.TryGet("SKL_Plaster_White", out var white), Is.True);
            AssertHex(white.color, 0xE9, 0xED, 0xF2);
            Assert.That(SkylineInkPalette.TryGet("SKL_Ground_Hazed", out var ground), Is.True);   // a prefix row: the library's ground family
        }

        [Test]
        public void FloorsStayFlatAndPropsKeepTheirTexture()
        {
            Assert.That(SkylineInkPalette.TryGet("SKL_Marble_Cream", out var marble), Is.True);
            Assert.That(marble.gradBottom, Is.EqualTo(1f));
            Assert.That(SkylineInkPalette.TryGet("SKL_Prop_Fern", out var fern), Is.True);
            Assert.That(fern.keepTexture, Is.True);
            Assert.That(SkylineInkPalette.TryGet("SK_Emissive_Signs", out var sign), Is.True);
            Assert.That(sign.keepTexture && sign.emissive, Is.True);
            Assert.That(SkylineInkPalette.TryGet("SKL_Something_Else", out _), Is.False);
        }

        [Test]
        public void FacadesFadeTowardsTheHorizonByBandAndHaze()
        {
            Assert.That(SkylineInkPalette.TryGet("SKL_Facade_Blue_Band0_Sun", out var b0), Is.True);
            AssertHex(b0.color, 0x7F, 0xA6, 0xC9);                          // band 0: the family colour as it is
            Assert.That(SkylineInkPalette.TryGet("SKL_Facade_Blue_Band2_Shade", out var b2), Is.True);
            Assert.That(b2.color.r, Is.GreaterThan(b0.color.r));            // lighter, towards #CFE0EE
            Assert.That(SkylineInkPalette.TryGet("SKL_Facade_Blue_Haze3", out var h3), Is.True);
            Assert.That(h3.color.r, Is.GreaterThan(b2.color.r));            // haze 3 (0.9) is the furthest
            Assert.That(h3.hatch, Is.EqualTo(0f));
        }

        [Test]
        public void ATwinKeepsTheNormalMapAndTheEmission()
        {
            var lit = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "TestInkLit" };
            var dst = new Material(Shader.Find(SkylineInkRoles.ShaderName));
            var bump = new Texture2D(2, 2);
            try
            {
                lit.SetTexture("_BumpMap", bump);
                lit.EnableKeyword("_NORMALMAP");
                lit.SetColor("_EmissionColor", new Color(0.4f, 0.2f, 0.1f));
                lit.EnableKeyword("_EMISSION");
                SkylineInkConvert.Fill(lit, dst, false);
                Assert.That(dst.IsKeywordEnabled("_NORMALMAP"), Is.True, "normal map keyword");
                Assert.That(dst.GetTexture("_BumpMap"), Is.SameAs(bump));
                Assert.That(dst.IsKeywordEnabled("_EMISSION"), Is.True, "emission keyword");
                Assert.That(dst.GetColor("_EmissionColor").r, Is.EqualTo(0.4f).Within(1e-4f));
                Assert.That(dst.GetFloat("_Unlit"), Is.EqualTo(0f));
                Assert.That(SkylineInkRoles.GetRole(dst), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(lit);
                Object.DestroyImmediate(dst);
                Object.DestroyImmediate(bump);
            }
        }

        [Test]
        public void AnUnlitSignBecomesAGlowWithItsColour()
        {
            var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "SK_Emissive_TestSign" };
            var dst = new Material(Shader.Find(SkylineInkRoles.ShaderName));
            try
            {
                unlit.SetColor("_BaseColor", new Color(1.6f, 1.1f, 0.5f));
                SkylineInkConvert.Fill(unlit, dst, false);
                Assert.That(dst.GetFloat("_Unlit"), Is.EqualTo(1f));
                Assert.That(dst.GetColor("_BaseColor").r, Is.EqualTo(1.6f).Within(1e-4f), "the HDR colour is kept");
                Assert.That(dst.GetFloat("_RimStrength"), Is.EqualTo(0f));
            }
            finally { Object.DestroyImmediate(unlit); Object.DestroyImmediate(dst); }
        }

        [Test]
        public void TransparentMaterialsAreSkipped()
        {
            var glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SK_Glass_Test" };
            var clear = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SomethingClear" };
            var opaque = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SomethingSolid" };
            try
            {
                clear.SetFloat("_Surface", 1f);
                Assert.That(SkylineInkConvert.IsConvertible(glass), Is.False, "SK_Glass_ by name");
                Assert.That(SkylineInkConvert.IsConvertible(clear), Is.False, "surface type transparent");
                Assert.That(SkylineInkConvert.IsConvertible(opaque), Is.True);
            }
            finally { Object.DestroyImmediate(glass); Object.DestroyImmediate(clear); Object.DestroyImmediate(opaque); }
        }

        [Test]
        public void TheRoleSwitchesTheMaskPass()
        {
            var m = new Material(Shader.Find(SkylineInkRoles.ShaderName));
            try
            {
                SkylineInkConvert.SetRole(m, 0);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.MaskPass), Is.False);
                SkylineInkConvert.SetRole(m, 1);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.MaskPass), Is.True);
                Assert.That(SkylineInkRoles.GetRole(m), Is.EqualTo(1));
                SkylineInkConvert.SetRole(m, 0);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.MaskPass), Is.False);
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void WeakPointsDrawLaterThanTheirBodies()
        {
            var m = new Material(Shader.Find(SkylineInkRoles.ShaderName));
            try
            {
                int ordinary = m.renderQueue;
                SkylineInkRoles.SetRole(m, SkylineInkRoles.BotWeakPoint);
                Assert.That(m.renderQueue, Is.EqualTo(SkylineInkRoles.BotWeakQueue));
                SkylineInkRoles.SetRole(m, SkylineInkRoles.WeakSpot);
                Assert.That(m.renderQueue, Is.EqualTo(SkylineInkRoles.WeakQueue));
                Assert.That(SkylineInkRoles.WeakQueue, Is.GreaterThan(SkylineInkRoles.BotWeakQueue), "5 over 6");
                Assert.That(SkylineInkRoles.BotWeakQueue, Is.GreaterThan(ordinary), "6 over 1");
                SkylineInkRoles.SetRole(m, SkylineInkRoles.Hostile);
                Assert.That(m.renderQueue, Is.EqualTo(ordinary), "back to the shader's own queue");
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void TheRoleHuesAreTheStyleSheets()
        {
            AssertHex(SkylineInkRoles.Hue(SkylineInkRoles.Hostile), 0xFF, 0x2E, 0x63);
            AssertHex(SkylineInkRoles.Hue(SkylineInkRoles.Objective), 0xB3, 0x6B, 0xFF);
            AssertHex(SkylineInkRoles.Hue(SkylineInkRoles.ObjectiveXray), 0xB3, 0x6B, 0xFF);
            AssertHex(SkylineInkRoles.Hue(SkylineInkRoles.Scrap), 0x8F, 0xD3, 0xFF);
            AssertHex(SkylineInkRoles.Hue(SkylineInkRoles.WeakSpot), 0xFF, 0xD6, 0x0A);
        }

        [Test]
        public void TheRoleNumbersAreTheStyleSheets()
        {
            Assert.That(new[] { SkylineInkRoles.None, SkylineInkRoles.Hostile, SkylineInkRoles.Objective, SkylineInkRoles.Scrap,
                SkylineInkRoles.Viewmodel, SkylineInkRoles.WeakSpot, SkylineInkRoles.BotWeakPoint, SkylineInkRoles.ObjectiveXray },
                Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
            Assert.That(SkylineInkRoles.ObjectiveQueue, Is.LessThan(SkylineInkRoles.BotWeakQueue), "6 and 5 still draw after the objective");
        }

        [Test]
        public void TheObjectiveXrayRoleDrawsOnlyThroughWalls()
        {
            var m = new Material(Shader.Find(SkylineInkRoles.ShaderName));
            try
            {
                SkylineInkConvert.SetRole(m, SkylineInkRoles.ObjectiveXray);
                Assert.That(SkylineInkRoles.GetRole(m), Is.EqualTo(SkylineInkRoles.ObjectiveXray));
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.MaskPass), Is.False, "no depth-tested outline: it would draw it where it is visible too");
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.ObjectiveXrayPass), Is.True);
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void TheRoleMaterialsLeftOnDiskDrawNoXray()
        {
            // EnsureResourceMaterials saves the four templates and the bot paint with a role below the objective's: their x-ray
            // pass is off, so a run-time clone that is not an objective never enters the x-ray list.
            SkylineInkConvert.EnsureResourceMaterials();
            foreach (var name in new[] { SkylineInkRoles.TemplatePlain, SkylineInkRoles.TemplateNormal, SkylineInkRoles.TemplateEmission,
                SkylineInkRoles.TemplateNormalEmission, "SK_Bot_Paint", "SK_Fx_Acid", "SK_Emissive_FxGlow" })
            {
                var m = Resources.Load<Material>(SkylineInkRoles.TemplateFolder + name);
                Assert.That(m, Is.Not.Null, name);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.ObjectiveXrayPass), Is.False, name);
            }
        }
    }
}
