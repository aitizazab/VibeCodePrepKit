using NUnit.Framework;
using UnityEngine;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the <c>Skyline/InkLit</c> shader (docs/SKYLINE-STYLE.md): it exists and compiles for this machine,
    /// it has the six passes the renderer feature and URP look for (the mask and the objective's x-ray), and it carries every property the converter, the
    /// palette and the roles write. They run in the pass's stage A, so a shader compile error shows up there first.
    /// </summary>
    public class SkylineInkShaderTests
    {
        public const string ShaderName = "Skyline/InkLit";

        /// <summary>Pass names, as Material.FindPass takes them (the LightMode tags are checked in the shader itself).</summary>
        static readonly string[] Passes = { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals", "SkylineInkMask", "SkylineInkXray" };

        static readonly string[] Properties =
        {
            "_BaseColor", "_BaseMap", "_BumpMap", "_BumpScale", "_EmissionColor", "_EmissionMap",
            "_GradBottom", "_GradFloorY", "_GradHeight", "_Desaturate", "_HatchStrength", "_InkRole", "_InkWidth", "_RimStrength", "_Unlit",
        };

        const string ShaderPath = "Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLit.shader";

        /// <summary>The shader source from the pass named <paramref name="passName"/> to the next "Pass" (or the end).</summary>
        static string PassSource(string passName)
        {
            string src = System.IO.File.ReadAllText(ShaderPath);
            int at = src.IndexOf("Name \"" + passName + "\"", System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "pass " + passName + " not found in the shader source");
            int next = src.IndexOf("Pass\n", at, System.StringComparison.Ordinal);
            if (next < 0) next = src.IndexOf("Pass\r\n", at, System.StringComparison.Ordinal);
            return next < 0 ? src.Substring(at) : src.Substring(at, next - at);
        }

        [Test]
        public void TheXrayPassIsNotDepthTestedLikeTheMask()
        {
            // D4: the mask pass depth-tests (only the visible surface is outlined), the objective's x-ray pass draws only where a
            // nearer surface hides it.
            Assert.That(PassSource("SkylineInkMask"), Does.Contain("ZTest LEqual"));
            string xray = PassSource("SkylineInkXray");
            Assert.That(xray, Does.Contain("ZTest Greater"));
            Assert.That(xray, Does.Not.Contain("ZTest LEqual"));
            Assert.That(xray, Does.Contain("ZWrite Off"));
        }

        [Test]
        public void TheXrayPassDrawsOnlyTheObjectiveRoles()
        {
            var m = new Material(Shader.Find(ShaderName));
            try
            {
                SkylineInkRoles.SetRole(m, SkylineInkRoles.Hostile);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.ObjectiveXrayPass), Is.False);
                SkylineInkRoles.SetRole(m, SkylineInkRoles.Objective);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.ObjectiveXrayPass), Is.True);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.MaskPass), Is.True, "visible: the ordinary outline too");
                Assert.That(m.renderQueue, Is.EqualTo(SkylineInkRoles.ObjectiveQueue), "the x-ray list draws this queue only");
                SkylineInkRoles.SetRole(m, SkylineInkRoles.ObjectiveXray);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.ObjectiveXrayPass), Is.True);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.MaskPass), Is.False, "x-ray only: nothing where it is visible");
                SkylineInkRoles.SetRole(m, SkylineInkRoles.None);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.ObjectiveXrayPass), Is.False);
                Assert.That(m.GetShaderPassEnabled(SkylineInkRoles.MaskPass), Is.False);
                Assert.That(m.renderQueue, Is.Not.EqualTo(SkylineInkRoles.ObjectiveQueue), "back to the shader's own queue");
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void TheMaskPassCarriesTheStyleSheetsHues()
        {
            string mask = PassSource("SkylineInkMask") + PassSource("SkylineInkXray");
            Assert.That(mask, Does.Contain("#B36BFF"));
            Assert.That(mask, Does.Contain("#8FD3FF"));
            Assert.That(mask, Does.Contain("#FF2E63"));
            Assert.That(mask, Does.Contain("#FFD60A"));
            Assert.That(mask, Does.Not.Contain("#2EE6FF"), "the old cyan objective is gone");
            Assert.That(mask, Does.Not.Contain("#5CFF8A"), "the old green scrap is gone");
        }

        [Test]
        public void TheWreckWidthStepsGiveSharedMaterialsPerStep()
        {
            var src = new Material(Shader.Find(ShaderName));
            try
            {
                var full = SkylineInkRoles.FromLit(src, SkylineInkRoles.Scrap, JamKit.Skyline.SkylineWreckOutline.Steps);
                var thin = SkylineInkRoles.FromLit(src, SkylineInkRoles.Scrap, 1);
                Assert.That(full, Is.Not.SameAs(thin));
                Assert.That(SkylineInkRoles.FromLit(src, SkylineInkRoles.Scrap, 1), Is.SameAs(thin), "shared per source, role and step");
                Assert.That(full.GetFloat("_InkWidth"), Is.EqualTo(1f).Within(1e-4f));
                Assert.That(thin.GetFloat("_InkWidth"), Is.EqualTo(0.4f).Within(1e-4f));
                Assert.That(SkylineInkRoles.GetRole(thin), Is.EqualTo(SkylineInkRoles.Scrap));
                Assert.That(thin.GetShaderPassEnabled(SkylineInkRoles.MaskPass), Is.True);
                Assert.That(thin.GetShaderPassEnabled(SkylineInkRoles.ObjectiveXrayPass), Is.False);
                Object.DestroyImmediate(full);
                Object.DestroyImmediate(thin);
            }
            finally { Object.DestroyImmediate(src); }
        }

        [Test]
        public void TheShaderExistsAndIsSupported()
        {
            var shader = Shader.Find(ShaderName);
            Assert.That(shader, Is.Not.Null, ShaderName + " is not found (a compile error in InkLit.shader?)");
            Assert.That(shader.isSupported, Is.True, ShaderName + " is not supported on this GPU / pipeline");
        }

        [Test]
        public void ThePaintedSkyIsSupported()
        {
            var shader = Shader.Find("Skyline/InkSky");
            Assert.That(shader, Is.Not.Null, "Skyline/InkSky is not found (a compile error in InkSky.shader?)");
            Assert.That(shader.isSupported, Is.True);
        }

        [Test]
        public void ARealMaterialHasEveryPass()
        {
            var m = new Material(Shader.Find(ShaderName));
            try
            {
                foreach (var pass in Passes)
                    Assert.That(m.FindPass(pass), Is.GreaterThanOrEqualTo(0), "pass " + pass + " is missing");
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void ThePassesCarryTheLightModesURPAsksFor()
        {
            var m = new Material(Shader.Find(ShaderName));
            try
            {
                Assert.That(m.GetShaderPassEnabled("SkylineInkMask"), Is.True, "the mask pass starts enabled; the converter turns it off for role 0");
                Assert.That(m.GetShaderPassEnabled("ForwardLit"), Is.True);
                Assert.That(m.GetShaderPassEnabled("ShadowCaster"), Is.True);
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void ItHasEveryProperty()
        {
            var m = new Material(Shader.Find(ShaderName));
            try
            {
                foreach (var p in Properties)
                    Assert.That(m.HasProperty(p), Is.True, "property " + p + " is missing");
                Assert.That(m.GetFloat("_GradBottom"), Is.EqualTo(0.78f).Within(1e-4f));
                Assert.That(m.GetFloat("_GradHeight"), Is.EqualTo(6f).Within(1e-4f));
                Assert.That(m.GetFloat("_HatchStrength"), Is.EqualTo(0.08f).Within(1e-4f));
                Assert.That(m.GetFloat("_RimStrength"), Is.EqualTo(0.25f).Within(1e-4f));
                Assert.That(m.GetFloat("_InkRole"), Is.EqualTo(0f));
            }
            finally { Object.DestroyImmediate(m); }
        }
    }
}
