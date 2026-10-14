using NUnit.Framework;
using UnityEngine;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the <c>Skyline/InkLit</c> shader (docs/SKYLINE-STYLE.md): it exists and compiles for this machine,
    /// it has the five passes the renderer feature and URP look for, and it carries every property the converter, the
    /// palette and the roles write. They run in the pass's stage A, so a shader compile error shows up there first.
    /// </summary>
    public class SkylineInkShaderTests
    {
        public const string ShaderName = "Skyline/InkLit";

        /// <summary>Pass names, as Material.FindPass takes them (the LightMode tags are checked in the shader itself).</summary>
        static readonly string[] Passes = { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals", "SkylineInkMask" };

        static readonly string[] Properties =
        {
            "_BaseColor", "_BaseMap", "_BumpMap", "_BumpScale", "_EmissionColor", "_EmissionMap",
            "_GradBottom", "_GradFloorY", "_GradHeight", "_Desaturate", "_HatchStrength", "_InkRole", "_RimStrength", "_Unlit",
        };

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
                Assert.That(m.GetFloat("_HatchStrength"), Is.EqualTo(0.15f).Within(1e-4f));
                Assert.That(m.GetFloat("_RimStrength"), Is.EqualTo(0.25f).Within(1e-4f));
                Assert.That(m.GetFloat("_InkRole"), Is.EqualTo(0f));
            }
            finally { Object.DestroyImmediate(m); }
        }
    }
}
