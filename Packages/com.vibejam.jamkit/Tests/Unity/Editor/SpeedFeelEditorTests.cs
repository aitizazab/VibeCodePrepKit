using JamKit.EditorTools;
using JamKit.Flow;
using JamKit.Movement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JamKit.Tests.Editor
{
    /// <summary>The speed visuals' shaders and comfort rules in the editor (docs/SPEED-FEEL.md "Speed feel (built)"):
    /// every shader imports without errors and is supported here; "reduce effects" is off by default, and on, it
    /// turns off the edge blur (V6) and the chromatic pulse (V1) and keeps the streaks and lines subtle; the movement
    /// tuning carries the tiers and the speed-feel numbers. The scenes' wiring is tested by each template.</summary>
    public class SpeedFeelEditorTests
    {
        static readonly string[] Shaders =
        {
            SpeedFeelSetup.ScreenShader,      // V1 ring + chromatic pulse, V6 edge blur, V7 grade
            SpeedFeelSetup.MotesShader,       // V2 streaks, V5 puffs and footprints, V7 flowers
            "JamKit/Movement/SpeedLines",     // V3 contrast-proof speed lines
        };

        [Test]
        public void EverySpeedShaderCompilesWithoutErrors()
        {
            foreach (var name in Shaders)
            {
                var shader = Shader.Find(name);
                Assert.That(shader, Is.Not.Null, name + " found");
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, name + " has no compile errors (see its inspector)");
                Assert.That(shader.isSupported, Is.True, name + " is supported on this graphics API");
                int messages = ShaderUtil.GetShaderMessageCount(shader);
                var all = ShaderUtil.GetShaderMessages(shader);
                for (int i = 0; i < messages && i < all.Length; i++)
                    Assert.That(all[i].severity, Is.Not.EqualTo(UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), name + ": " + all[i].message);
            }
        }

        [Test]
        public void ReduceEffectsIsOffByDefaultAndTurnsOffTheBlurAndTheChromaticPulse()
        {
            Assert.That(new Settings().reduceEffects, Is.False, "off by default");
            const float dt = 1f / 60f;
            var normal = new SpeedFeelState(new SpeedFeelDef(), new SpeedTierDef());
            var reduced = new SpeedFeelState(new SpeedFeelDef(), new SpeedTierDef());
            for (int i = 0; i < 60; i++) { normal.Tick(10f, dt, 1f, false); reduced.Tick(10f, dt, 1f, true); }
            var a = normal.Tick(16f, dt, 1f, false);
            var b = reduced.Tick(16f, dt, 1f, true);
            Assert.That(a.chroma, Is.GreaterThan(0f), "the pulse normally");
            Assert.That(b.boom, Is.True);
            Assert.That(b.chroma, Is.EqualTo(0f), "no chromatic pulse with reduce effects");
            for (int i = 0; i < 120; i++) { a = normal.Tick(16f, dt, 1f, false); b = reduced.Tick(16f, dt, 1f, true); }
            Assert.That(a.edgeBlur, Is.GreaterThan(0f), "the edge blur at flying normally");
            Assert.That(b.edgeBlur, Is.EqualTo(0f), "no edge blur with reduce effects");
            Assert.That(b.streakRate, Is.LessThan(a.streakRate * 0.5f), "streaks subtle");
            Assert.That(b.linesShare, Is.LessThan(0.5f), "speed lines subtle");
        }

        [Test]
        public void TheMovementTuningCarriesTheTiersAndTheSpeedFeel()
        {
            var t = ScriptableObject.CreateInstance<MovementTuning>();
            try
            {
                Assert.That(t.speedTiers, Is.Not.Null);
                Assert.That(t.speedTiers.fastEnter, Is.EqualTo(8f));
                Assert.That(t.speedTiers.flyingEnter, Is.EqualTo(13f));
                Assert.That(t.speedFeel, Is.Not.Null);
                Assert.That(t.speedFeel.boomFov, Is.EqualTo(6f));
            }
            finally
            {
                Object.DestroyImmediate(t);
            }
        }
    }
}
