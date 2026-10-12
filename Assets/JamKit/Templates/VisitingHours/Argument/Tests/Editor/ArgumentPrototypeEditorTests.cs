using System.IO;
using System.Linq;
using JamKit.Argument.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Argument.Tests
{
    /// <summary>The argument prototype without play mode: the screen builds on black with the opponent's sprite and
    /// the real fonts, every expression has a sprite (a placeholder until the art lands), the prose and the sounds load
    /// from their files, the one focus model keeps its rules, the transitions finish, and the scene is built.</summary>
    public class ArgumentPrototypeEditorTests
    {
        [SetUp]
        public void Files()
        {
            ArgumentContent.Ensure();
            ArgumentArt.Forget(); // no sprite (or placeholder) left over from another test
            ArgumentBoil.Use("gentle");
            ArgumentBoil.SetReacting(false);
        }

        [TearDown]
        public void BoilBack()
        {
            ArgumentBoil.Use("gentle");
            ArgumentBoil.SetReacting(false);
            ArgumentHost.OverrideExpression(null);
        }

        static void Run(ArgumentView view, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.02f) view.Tick(0.02f);
        }

        [Test]
        public void TheScreenBuildsOnBlackWithTheSpriteAndRealFonts()
        {
            var parent = new VisualElement();
            var view = new ArgumentView(parent);
            Assert.That(parent.Q<VisualElement>(ArgumentView.RootName), Is.Not.Null);
            var black = parent.Q<VisualElement>("argument-black");
            Assert.That(black, Is.Not.Null, "the scene behind is covered");
            Assert.That(black.style.backgroundColor.value.r, Is.LessThan(0.05f), "mostly black");

            Assert.That(ArgumentArt.Display, Is.Not.Null, "DM Serif Display is in Resources/ArgumentFonts");
            Assert.That(ArgumentArt.Body, Is.Not.Null, "Crimson Text is in Resources/ArgumentFonts");
            Assert.That(ArgumentArt.BodyItalic, Is.Not.Null);
            var claimText = parent.Q<Label>("argument-claim-text");
            Assert.That(claimText.style.unityFontDefinition.value.font, Is.EqualTo(ArgumentArt.Display), "the claim in the display font");

            var def = DreamLenaDemo.Build();
            view.SetOpponent(def.opponent, "angry", "angry", "sprite placeholder");
            Assert.That(view.SpriteName, Is.EqualTo("Lena_angry"));
            Assert.That(parent.Q<VisualElement>("argument-sprite").style.backgroundImage.value.texture, Is.Not.Null);
            view.SetClaim("THE CLAIM", def.claim, "3 key premises");
            Assert.That(view.ClaimText, Is.EqualTo(def.claim));
            view.SetPremise(0, "L1", "FACT", "", "standing", def.Premise("L1").text, PremiseStatus.Standing);
            view.SetPremise(1, "?", "", "", "hidden", "", PremiseStatus.Hidden);
            Assert.That(view.PremiseCard(0).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            view.SetFocusPanel("prompt", "L1", def.Premise("L1").text, "She said:", def.Premise("L1").askReply);
            Assert.That(view.FocusText, Is.EqualTo(def.Premise("L1").text));
            foreach (var v in new[] { "ask", "challenge", "concede", "press", "rest", "transcript" }) Assert.That(view.Verb(v), Is.Not.Null, v);
            view.ShowEvidence("Which?", new[] { "the fourth chair", "Saturday off" }, new[] { "a", "b" });
            Assert.That(view.EvidenceCard(1), Is.Not.Null);
            view.ShowReasons("Because...", new[] { "a", "b", "c" });
            Assert.That(view.ReasonCard(2), Is.Not.Null);
            view.ShowEnd("Convinced", "Lena is amenable.", "Continue");
            Assert.That(view.EndShown, Is.True);
            Assert.That(view.ContinueButton, Is.Not.Null);
            Assert.That(parent.Query<Button>().ToList(), Is.Empty, "no UI Toolkit buttons: one focus model, never lost");
            view.Remove();
            Assert.That(parent.childCount, Is.EqualTo(0));
        }

        [Test]
        public void TheTransitionsAreTimedAndFinish()
        {
            var view = new ArgumentView(new VisualElement());
            bool inDone = false;
            view.PlayIntro(() => inDone = true);
            Assert.That(view.Busy, Is.True, "not a cut");
            Run(view, 0.5f);
            Assert.That(inDone, Is.False, "the intro takes about a second");
            Run(view, 1f);
            Assert.That(inDone && !view.Busy, Is.True);

            bool open = false;
            view.OpenBox(() => open = true);
            Run(view, 0.2f);
            Assert.That(view.BoxShown, Is.False, "the claims fold away first");
            Run(view, 0.6f);
            Assert.That(open && view.BoxShown, Is.True, "then the box opens");
            bool closed = false;
            view.CloseBox(() => closed = true);
            Run(view, 0.8f);
            Assert.That(closed && !view.BoxShown, Is.True, "the box collapses, the claims come back");
        }

        [Test]
        public void EveryExpressionHasASpriteUntilTheArtLands()
        {
            foreach (var e in ArgumentArt.Expressions)
            {
                var t = ArgumentArt.Sprite("Lena", e, out bool placeholder);
                Assert.That(t, Is.Not.Null, e);
                if (placeholder)
                {
                    Assert.That(t.width, Is.EqualTo(ArgumentArt.PlaceholderWidth));
                    Assert.That(t.height, Is.EqualTo(ArgumentArt.PlaceholderHeight));
                    Assert.That(t.name, Is.EqualTo("placeholder_" + e), "a different face per expression");
                }
            }
            Assert.That(ArgumentArt.Sprite("Lena", "unknown-mood", out _).name, Does.EndWith("neutral"), "an unknown expression falls back to neutral");
        }

        const string ArtDir = "Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentArt";

        [Test]
        public void EveryExpressionWithAFileLoadsTheRealArt()
        {
            ArgumentArt.Forget();
            int files = 0;
            foreach (var e in ArgumentArt.Expressions)
            {
                string path = ArtDir + "/lena_" + e + ".png";
                if (!File.Exists(path)) continue;
                files++;
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(ti, Is.Not.Null, path + " is imported as a texture");
                Assert.That(ti.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path + ": no power-of-two rescale (it stays 2:3)");
                Assert.That(ti.mipmapEnabled, Is.False, path + ": no mipmaps");
                Assert.That(ti.alphaIsTransparency, Is.True, path + ": alpha is transparency");
                Assert.That(ArgumentArt.HasFile("Lena", e), Is.True, path + " loads from Resources");
                var t = ArgumentArt.Sprite("Lena", e, out bool placeholder);
                Assert.That(placeholder, Is.False, path + ": the real art, not the code-drawn placeholder");
                Assert.That(t.name, Is.EqualTo("lena_" + e), path + ": its own file, not neutral's");
                Assert.That(t.width, Is.EqualTo(1024), path + " width");
                Assert.That(t.height, Is.EqualTo(1536), path + " height");
            }
            Assert.That(files, Is.GreaterThanOrEqualTo(7), "Lena's seven poses are in " + ArtDir + " (104f4ea, 98f02e0)");
        }

        [Test]
        public void SheStartsInTheAntagonisticPoseAndTreatmentsFallBack()
        {
            Assert.That(ArgumentArt.Opening, Is.EqualTo("antagonistic"));
            Assert.That(ArgumentArt.Expressions, Does.Contain(ArgumentArt.Opening));
            var open = ArgumentArt.Sprite("Lena", ArgumentArt.Opening, out bool placeholder);
            Assert.That(open, Is.Not.Null, "the opening pose has a sprite");
            bool drawn = Resources.Load<Texture2D>(ArgumentArt.ArtFolder + "lena_antagonistic") != null;
            if (!drawn && !placeholder) Assert.That(open.name, Is.EqualTo("lena_neutral"), "no lena_antagonistic.png yet: neutral stands in");
            if (drawn) Assert.That(open.name, Is.EqualTo("lena_antagonistic"));

            ArgumentArt.SetTreatment("bw");
            try
            {
                Assert.That(ArgumentArt.Treatment, Is.EqualTo("bw"));
                var t = ArgumentArt.Sprite("Lena", "angry", out _);
                bool bw = Resources.Load<Texture2D>(ArgumentArt.ArtFolder + "lena_bw_angry") != null;
                Assert.That(t.name, Is.EqualTo(bw ? "lena_bw_angry" : "lena_angry"), "a treatment without files falls back to the plain sprite");
            }
            finally { ArgumentArt.SetTreatment(""); }
        }

        [Test]
        public void TheProseAndTheSoundsLoadFromTheirFiles()
        {
            var def = DreamLenaDemo.Build();
            Assert.That(def.claim, Is.Not.Empty.And.Not.StartWith("["), "lena_night2.txt from Resources");
            Assert.That(ArgumentStrings.Required.All(k => ArgumentStrings.Shared.Has(k)), Is.True, "screen.txt from Resources");
            Assert.That(ArgumentAudio.Clip(ArgumentAudio.MusicFile), Is.Not.Null, "the battle loop (Resources/ArgumentAudio/argument_battle_loop)");
            foreach (var s in ArgumentAudio.Sounds) Assert.That(ArgumentAudio.Clip(s), Is.Not.Null, s);
        }

        [Test]
        public void TheBoilHasItsPresetsWithLilysFourVariables()
        {
            string[] names = { "Gentle", "Subtle", "Lily-equivalent", "Strong", "Slow and heavy", "Off" };
            Assert.That(ArgumentBoil.Presets.Select(x => x.name), Is.EqualTo(names));
            Assert.That(ArgumentBoil.Preset.name, Is.EqualTo("Gentle"), "the default");
            foreach (var p in ArgumentBoil.Presets.Where(x => x.name != "Off"))
            {
                Assert.That(p.style, Is.EqualTo(BoilStyle.Boil), p.name + " is a boil");
                Assert.That(p.strength, Is.GreaterThan(0f), p.name);
                Assert.That(p.freq, Is.InRange(0.002f, 0.08f), p.name + ": the range of Lily's slider");
                Assert.That(p.rate, Is.InRange(1f, 24f), p.name);
                Assert.That(p.Visible, Is.True, p.name);
            }
            Assert.That(ArgumentBoil.Presets[ArgumentBoil.Find("off")].Visible, Is.False);

            // Lily's 4 px / 0.02 / 8 per second, scaled by the two figures' heights on screen.
            var lily = ArgumentBoil.Presets[ArgumentBoil.Find("lily")];
            float scale = ArgumentBoil.LenaFigurePx / ArgumentBoil.LilyFigurePx;
            Assert.That(lily.strength, Is.EqualTo(ArgumentBoil.Lily.strength * scale).Within(1e-4f));
            Assert.That(lily.freq, Is.EqualTo(ArgumentBoil.Lily.freq / scale).Within(1e-6f));
            Assert.That(lily.rate, Is.EqualTo(8f));
            Assert.That(ArgumentBoil.Lily.strength, Is.EqualTo(4f));
            Assert.That(ArgumentBoil.Lily.freq, Is.EqualTo(0.02f));

            // Gentle is gentler than Lily's own, relative to the sprite; slow-heavy is slower and heavier than gentle.
            var gentle = ArgumentBoil.Presets[0];
            Assert.That(gentle.strength, Is.LessThan(lily.strength));
            var heavy = ArgumentBoil.Presets[ArgumentBoil.Find("slow-heavy")];
            Assert.That(heavy.strength, Is.GreaterThan(gentle.strength));
            Assert.That(heavy.rate, Is.LessThan(gentle.rate));
            Assert.That(ArgumentBoil.Presets[ArgumentBoil.Find("subtle")].strength, Is.LessThan(gentle.strength));

            // F8 cycles and wraps; names are found loosely.
            for (int i = 1; i <= ArgumentBoil.Presets.Length; i++) Assert.That(ArgumentBoil.Next().name, Is.EqualTo(names[i % names.Length]));
            Assert.That(ArgumentBoil.Use("Slow-Heavy") && ArgumentBoil.Preset.name == "Slow and heavy", Is.True);
            Assert.That(ArgumentBoil.Use("strong") && ArgumentBoil.Preset.name == "Strong", Is.True);
            Assert.That(ArgumentBoil.Use("no such boil"), Is.False);
            Assert.That(gentle.Describe(), Does.Contain("Gentle").And.Contain("4 px").And.Contain("8/s"));
        }

        [Test]
        public void TheBoilsPixelsBecomeTheShadersScale()
        {
            // Lena's 1024 x 1536 sprite fit into the 640 x 860 slot: 573 x 860 on a 1080p screen.
            var shown = ArgumentBoil.Shown(new Vector2(1024f, 1536f), new Vector2(640f, 860f));
            Assert.That(shown.y, Is.EqualTo(860f).Within(1e-3f));
            Assert.That(shown.x, Is.EqualTo(1024f * 860f / 1536f).Within(1e-3f));
            Assert.That(ArgumentBoil.Shown(new Vector2(1024f, 1536f), new Vector2(float.NaN, 0f)), Is.EqualTo(new Vector2(1024f, 1536f)), "no layout yet: the texture's own size");

            var s = new BoilSettings("t", BoilStyle.Boil, 4f, 0.01f, 8f, 0.6f);
            var k = ArgumentBoil.ShaderScale(s, new Vector2(1024f, 1536f), shown);
            Assert.That(k.x * shown.x, Is.EqualTo(4f).Within(1e-4f), "4 px across, as a share of the shown width");
            Assert.That(k.y * shown.y, Is.EqualTo(4f).Within(1e-4f), "and of its height");
            Assert.That(k.z, Is.EqualTo(0.01f * shown.x).Within(1e-4f), "noise cycles across the sprite");
            Assert.That(k.w, Is.EqualTo(0.01f * shown.y).Within(1e-4f));

            // Stepped, not smooth: a new seed rate times a second.
            Assert.That(ArgumentBoil.Step(0f, 8f), Is.EqualTo(0));
            Assert.That(ArgumentBoil.Step(0.124f, 8f), Is.EqualTo(0));
            Assert.That(ArgumentBoil.Step(0.126f, 8f), Is.EqualTo(1));
            Assert.That(ArgumentBoil.Step(1f, 8f), Is.EqualTo(8));
            Assert.That(ArgumentBoil.Step(1f, 3f), Is.EqualTo(3));
            var b = ArgumentBoil.BreatheFreq(s, 0f, 0f);
            Assert.That(b.x, Is.EqualTo(0.01f).Within(1e-6f));
            Assert.That(b.y, Is.EqualTo(0.0135f).Within(1e-5f));

            Assert.That(Resources.Load<Shader>(ArgumentBoilLayer.ShaderPath), Is.Not.Null, "Resources/ArgumentShaders/ArgumentBoil.shader (in builds too)");
        }

        [Test]
        public void HerMoodChangesTheBoilOnlyWhenReacting()
        {
            var gentle = ArgumentBoil.Preset;
            Assert.That(ArgumentBoil.Reacting, Is.False, "off by default");
            Assert.That(ArgumentBoil.For("angry").Same(gentle), Is.True);
            ArgumentBoil.SetReacting(true);
            Assert.That(ArgumentBoil.For("angry").strength, Is.GreaterThan(gentle.strength));
            Assert.That(ArgumentBoil.For("rattled").rate, Is.GreaterThan(gentle.rate));
            Assert.That(ArgumentBoil.For("softened").strength, Is.LessThan(gentle.strength));
            Assert.That(ArgumentBoil.For("neutral").Same(gentle), Is.True);
            Assert.That(ArgumentBoil.For("angry").name, Is.EqualTo(gentle.name), "the same preset");
        }

        [Test]
        public void TheViewShowsTheBoilAndItsLabel()
        {
            var parent = new VisualElement();
            var view = new ArgumentView(parent);
            view.SetOpponent("Lena", "angry", "angry", "");
            Assert.That(view.SpriteShown, Is.Not.Null, "the sprite, boiled where there's a graphics device");
            if (!ArgumentBoilLayer.Available) Assert.That(view.SpriteShown, Is.SameAs(ArgumentArt.Sprite("Lena", "angry", out _)), "no device: the plain sprite");
            view.Tick(0.2f);
            Assert.That(view.BoilLabel, Is.Empty, "hidden until F8");
            ArgumentBoil.Next();
            view.ShowBoilLabel();
            Assert.That(view.BoilLabel, Does.Contain("Subtle").And.Contain("F8"));
            view.Tick(5f);
            Assert.That(view.BoilLabel, Is.Empty, "it goes after a few seconds");
            view.ShowBoilLabel(true);
            view.Tick(5f);
            Assert.That(view.BoilLabel, Does.Contain("Subtle"), "a capture keeps it");
            view.Remove();
        }

        [Test]
        public void TheBoilCaptureFlagsParse()
        {
            Assert.That(ArgumentShot.Parse(new[] { "VisitingHours.exe" }, out _), Is.False, "nothing without the flag");
            Assert.That(ArgumentShot.Parse(new[] { "VisitingHours.exe", "-argument-shot", "C:/out/boil", "-argument-expression", "Angry", "-argument-boil", "slow-heavy",
                "-argument-react", "-argument-frames", "24", "-argument-fps", "24" }, out var r), Is.True);
            Assert.That(r.folder, Is.EqualTo("C:/out/boil"));
            Assert.That(r.expression, Is.EqualTo("angry"));
            Assert.That(r.preset, Is.EqualTo("slow-heavy"));
            Assert.That(r.react, Is.True);
            Assert.That(r.frames, Is.EqualTo(24));
            Assert.That(r.fps, Is.EqualTo(24f));
            Assert.That(ArgumentShot.Parse(new[] { "game.exe", "-argument-shot" }, out r), Is.True);
            Assert.That(r.expression, Is.EqualTo(ArgumentArt.Opening), "her opening pose by default");
            Assert.That(r.frames, Is.EqualTo(12));
            Assert.That(r.fps, Is.EqualTo(12f));
            Assert.That(r.preset, Is.Null, "the current preset");
            Assert.That(ArgumentShot.FrameName("Slow and heavy", "angry", 3), Is.EqualTo("boil_slow-and-heavy_angry_03.png"));
        }

        [Test]
        public void TheFocusModelMovesWithoutWrappingAndSkipsWhatsOff()
        {
            var nav = new ArgueNav();
            var a = new NavItem { id = "a", element = new VisualElement() };
            var b = new NavItem { id = "b", element = new VisualElement(), enabled = false };
            var c = new NavItem { id = "c", element = new VisualElement() };
            var d = new NavItem { id = "d", element = new VisualElement() };
            nav.AddLine(a, b, c);
            nav.AddLine(d);
            nav.FocusFirst();
            Assert.That(nav.FocusedId, Is.EqualTo("a"));
            Assert.That(nav.Move(-1, 0), Is.False, "no wrap-around");
            Assert.That(nav.Move(1, 0), Is.True, "right, past the disabled one");
            Assert.That(nav.FocusedId, Is.EqualTo("c"), "the disabled one is skipped");
            Assert.That(nav.Move(1, 0), Is.False, "no wrap-around at the end of the line");
            Assert.That(nav.Move(0, 1), Is.True, "down to the next line, even before layout (NaN widths)");
            Assert.That(nav.FocusedId, Is.EqualTo("d"), "the only item on the line below");
            Assert.That(nav.Move(0, 1), Is.False, "no wrap-around at the bottom");
            Assert.That(ArgueUi.IsFocused(d.element) && !ArgueUi.IsFocused(c.element), Is.True, "one item lit");
            Assert.That(nav.Focus("b"), Is.False, "a disabled item can't take focus");
        }

        [Test]
        public void ThePrototypeSceneIsBuiltAndInBuildSettings()
        {
            Assert.That(File.Exists(ArgumentPrototypeBuilder.ScenePath), Is.True, "run JamKit > Rebuild Shell Scenes");
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ArgumentPrototypeBuilder.ScenePath), Is.True);
            string text = File.ReadAllText(ArgumentPrototypeBuilder.ScenePath);
            Assert.That(text, Does.Contain("Argument prototype"), "the ArgumentPrototype object");
        }

        [Test]
        public void TheChaptersAreNamedForEverySetting()
        {
            Assert.That(ArgumentPrototype.Chapters, Has.Length.EqualTo(5));
            Assert.That(ArgumentPrototype.SetupFor(ArgumentPrototype.LongNight).difficulty, Is.EqualTo(ArgumentDifficulty.ScriptedButBeatable));
            Assert.That(ArgumentPrototype.SetupFor(ArgumentPrototype.Story).difficulty, Is.EqualTo(ArgumentDifficulty.Story));
            var noWalt = ArgumentPrototype.SetupFor(ArgumentPrototype.NoWalt);
            Assert.That(noWalt.allEvidence, Is.False);
            Assert.That(noWalt.evidence, Does.Not.Contain(DreamLenaDemo.TruthWalt));
        }
    }
}
