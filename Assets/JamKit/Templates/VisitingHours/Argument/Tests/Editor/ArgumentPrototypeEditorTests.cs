using System.IO;
using System.Linq;
using JamKit.Argument.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Argument.Tests
{
    /// <summary>The argument battle without play mode: the stage builds on black with the opponent's sprite and the real
    /// fonts, every state of a battle renders, the dialogue box sizes to its text, every expression has a sprite (a placeholder until the art lands), the prose and the sounds load
    /// from their files, the one focus model keeps its rules, the transitions finish, and the scene is built.</summary>
    public class ArgumentPrototypeEditorTests
    {
        [SetUp]
        public void Files()
        {
            ArgumentContent.Ensure();
            ArgumentArt.Forget(); // no sprite (or placeholder) left over from another test
            ArgumentBoil.SetReacting(true);
        }

        [TearDown]
        public void BoilBack()
        {
            ArgumentBoil.SetReacting(true);
            ArgumentHost.OverrideExpression(null);
        }

        static BattleFlow Flow(ArgumentDifficulty d = ArgumentDifficulty.Story, ArgumentPresenter presenter = ArgumentPresenter.Debate) =>
            new BattleFlow(new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { difficulty = d, allEvidence = true, seed = 3 }), presenter);

        static void Run(ArgumentStage stage, BattleFlow flow, float seconds, BattlePilot pilot = null)
        {
            for (float t = 0f; t < seconds; t += 0.02f)
            {
                if (pilot != null) flow.Tick(0.02f, pilot.Steer(flow, 0.02f));
                stage.Render(flow, 0.02f, "hints", null, false, 1f);
            }
        }

        [Test]
        public void TheStageBuildsOnBlackWithTheSpriteAndRealFonts()
        {
            var parent = new VisualElement();
            var stage = new ArgumentStage(parent);
            Assert.That(parent.Q<VisualElement>(ArgumentStage.RootName), Is.Not.Null);
            var black = parent.Q<VisualElement>("argument-black");
            Assert.That(black, Is.Not.Null, "the scene behind is covered");
            Assert.That(black.style.backgroundColor.value.r, Is.LessThan(0.05f), "mostly black");
            Assert.That(ArgumentArt.Display, Is.Not.Null, "DM Serif Display is in Resources/ArgumentFonts");
            Assert.That(ArgumentArt.Body, Is.Not.Null, "Crimson Text is in Resources/ArgumentFonts");
            Assert.That(ArgumentArt.BodyItalic, Is.Not.Null);
            var claimText = parent.Q<Label>("argument-claim-text");
            Assert.That(claimText.style.unityFontDefinition.value.font, Is.EqualTo(ArgumentArt.Display), "the claim in the display font");

            var flow = Flow();
            stage.Render(flow, 0.02f, "hints", null, false, 1f);
            Assert.That(stage.ClaimText, Is.EqualTo(flow.Def.claim));
            Assert.That(stage.Face, Is.EqualTo(BattleFlow.OpeningFace), "she opens in her antagonistic pose");
            Assert.That(parent.Q<VisualElement>("argument-portrait").style.backgroundImage.value.texture != null
                || parent.Q<VisualElement>("argument-portrait").style.backgroundImage.value.renderTexture != null, Is.True, "her sprite is up");
            Assert.That(parent.Q<VisualElement>("argument-table"), Is.Not.Null, "the table");
            Assert.That(parent.Q<VisualElement>("argument-dialogue"), Is.Not.Null, "the dialogue box");
            Assert.That(parent.Query<Button>().ToList(), Is.Empty, "no UI Toolkit buttons");
            stage.Remove();
            Assert.That(parent.childCount, Is.EqualTo(0));
        }

        [Test]
        public void TheDialogueBoxTypesALineAndNeverHasAFixedHeight()
        {
            var parent = new VisualElement();
            var stage = new ArgumentStage(parent);
            var flow = Flow();
            Run(stage, flow, 0.3f, new BattlePilot());
            var line = parent.Q<Label>("argument-line");
            Assert.That(line.text, Does.Contain(Typewriter.HiddenOpen).Or.EqualTo(Typewriter.Spelled(flow.Delivery.Current.text)), "typed: the rest is there, unseen");
            var box = parent.Q<VisualElement>("argument-dialogue");
            Assert.That(box.style.height.value.value, Is.EqualTo(0f), "the box sizes to its text (no fixed height)");
            Assert.That(box.style.maxHeight.value.value, Is.EqualTo(0f), "and nothing caps it");
            Assert.That(line.style.overflow.value, Is.Not.EqualTo(Overflow.Hidden), "nothing clips the line");
            stage.Remove();
        }

        [Test]
        public void TheTransitionsAreTimedAndFinish()
        {
            var stage = new ArgumentStage(new VisualElement());
            var flow = Flow();
            bool inDone = false;
            stage.PlayIntro(() => inDone = true);
            Assert.That(stage.Busy, Is.True, "not a cut");
            Run(stage, flow, 0.5f);
            Assert.That(inDone, Is.False, "the intro takes over a second");
            Run(stage, flow, 1f);
            Assert.That(inDone && !stage.Busy, Is.True);
            stage.Remove();
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

            // Walt's six (b36a366), kind and sad among them.
            foreach (var e in new[] { "antagonistic", "neutral", "kind", "rattled", "softened", "sad" })
            {
                Assert.That(ArgumentArt.Expressions, Does.Contain(e));
                var t = ArgumentArt.Sprite("Walt", e, out bool placeholder);
                Assert.That(placeholder, Is.False, "walt_" + e);
                Assert.That(t.name, Is.EqualTo("walt_" + e), "his own drawing");
            }
            Assert.That(ArgumentArt.Sprite("Lena", "kind", out _).name, Is.EqualTo("lena_neutral"), "Lena has no kind drawing: her neutral");
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
            Assert.That(ArgumentBoil.Reacting, Is.True, "by default her mood picks the boil");
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

            // F10: each preset in turn, then back to her mood; names are found loosely.
            for (int i = 0; i < names.Length; i++) Assert.That(ArgumentBoil.Next(), Is.EqualTo(names[i]));
            Assert.That(ArgumentBoil.Next(), Is.EqualTo("by her mood"));
            Assert.That(ArgumentBoil.Reacting, Is.True);
            Assert.That(ArgumentBoil.Use("Slow-Heavy") && ArgumentBoil.Preset.name == "Slow and heavy" && !ArgumentBoil.Reacting, Is.True);
            Assert.That(ArgumentBoil.Use("strong") && ArgumentBoil.Preset.name == "Strong", Is.True);
            Assert.That(ArgumentBoil.Use("mood") && ArgumentBoil.Reacting, Is.True);
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
        public void TheBoilKeyIsOneNothingElseUses()
        {
            // Taken elsewhere: F1 (debug menu), F3/F4/F6/F7 (movement), F5/F9 (Story), F7/F8 (the dream house's argument
            // skips), F9 (the opening editor). F11 is a browser's fullscreen in the web build.
            var taken = new[] { UnityEngine.InputSystem.Key.F1, UnityEngine.InputSystem.Key.F3, UnityEngine.InputSystem.Key.F4,
                UnityEngine.InputSystem.Key.F5, UnityEngine.InputSystem.Key.F6, UnityEngine.InputSystem.Key.F7,
                UnityEngine.InputSystem.Key.F8, UnityEngine.InputSystem.Key.F9, UnityEngine.InputSystem.Key.F11,
                UnityEngine.InputSystem.Key.Backquote };
            Assert.That(System.Array.IndexOf(taken, ArgumentBoil.CycleKey), Is.EqualTo(-1), "a key nothing else uses");
            Assert.That(ArgumentBoil.CycleKey, Is.EqualTo(UnityEngine.InputSystem.Key.F10));
            Assert.That(ArgumentBoil.CycleKeyName, Is.EqualTo("F10"));
        }

        [Test]
        public void HerMoodPicksTheBoilUnlessF10Overrides()
        {
            Assert.That(ArgumentBoil.Reacting, Is.True, "on by default (the creator's pick)");
            Assert.That(ArgumentBoil.For("neutral").name, Is.EqualTo("Gentle"));
            Assert.That(ArgumentBoil.For("antagonistic").name, Is.EqualTo("Lily-equivalent"));
            Assert.That(ArgumentBoil.For("angry").name, Is.EqualTo("Strong"));
            Assert.That(ArgumentBoil.For("rattled").name, Is.EqualTo("Slow and heavy"));
            Assert.That(ArgumentBoil.For("softened").name, Is.EqualTo("Subtle"));
            Assert.That(ArgumentBoil.For("hurt").name, Is.EqualTo("Subtle"));
            Assert.That(ArgumentBoil.For("pleading").name, Is.EqualTo("Gentle"));
            foreach (var e in ArgumentArt.Expressions) Assert.That(ArgumentBoil.For(e).Visible, Is.True, e + " boils");
            ArgumentBoil.Next();
            Assert.That(ArgumentBoil.For("angry").name, Is.EqualTo("Gentle"), "F10 overrides her mood");
            ArgumentBoil.SetReacting(true);
            Assert.That(ArgumentBoil.For("angry").name, Is.EqualTo("Strong"), "Shift+F10: back to her mood");
        }

        [Test]
        public void TheStageShowsTheBoilAndItsLabel()
        {
            var parent = new VisualElement();
            var stage = new ArgumentStage(parent);
            var flow = Flow();
            stage.Render(flow, 0.02f, "", "angry", false, 1f);
            Assert.That(stage.Face, Is.EqualTo("angry"), "a capture can hold her on a face");
            Assert.That(stage.BoilLabel, Is.Empty, "hidden until F10");
            stage.ShowBoilLabel();
            Assert.That(stage.BoilLabel, Does.Contain("by her mood").And.Contain("Strong"), "angry: her mood's preset");
            ArgumentBoil.Next();
            ArgumentBoil.Next();
            stage.ShowBoilLabel();
            Assert.That(stage.BoilLabel, Does.Contain("Subtle").And.Contain("F10").And.Contain("Shift+F10"));
            for (int i = 0; i < 260; i++) stage.Render(flow, 0.02f, "", "angry", false, 1f);
            Assert.That(stage.BoilLabel, Is.Empty, "it goes after a few seconds");
            stage.ShowBoilLabel(true);
            for (int i = 0; i < 260; i++) stage.Render(flow, 0.02f, "", "angry", false, 1f);
            Assert.That(stage.BoilLabel, Does.Contain("Subtle"), "a capture keeps it");
            stage.Remove();
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
            Assert.That(ArgumentPrototype.Chapters, Has.Length.EqualTo(7));
            Assert.That(ArgumentPrototype.Chapters, Does.Contain(ArgumentPrototype.Debate), "the debate (milestone 1) behind its own chapter");
            Assert.That(ArgumentPrototype.SetupFor(ArgumentPrototype.Walt).evidence, Does.Contain(WaltNight1.WaltDead), "the tutorial with Night 1's deduction");
            Assert.That(ArgumentPrototype.SetupFor(ArgumentPrototype.LongNight).difficulty, Is.EqualTo(ArgumentDifficulty.ScriptedButBeatable));
            Assert.That(ArgumentPrototype.SetupFor(ArgumentPrototype.Story).difficulty, Is.EqualTo(ArgumentDifficulty.Story));
            var noWalt = ArgumentPrototype.SetupFor(ArgumentPrototype.NoWalt);
            Assert.That(noWalt.allEvidence, Is.False);
            Assert.That(noWalt.evidence, Does.Not.Contain(DreamLenaDemo.TruthWalt));
        }

        [Test]
        public void TheStageDrawsAWholeBattleWithoutAnError()
        {
            // Every state of a battle, rendered frame by frame (no panel: the layout isn't measured, the drawing still runs),
            // in both presenters: the sections (round 2) with her sky, and the debate (milestone 1).
            var parent = new VisualElement();
            var stage = new ArgumentStage(parent);
            BattleFlow flow = null;
            foreach (var presenter in new[] { ArgumentPresenter.Sections, ArgumentPresenter.Debate })
            {
                flow = Flow(ArgumentDifficulty.Story, presenter);
                var seen = new System.Collections.Generic.HashSet<BattleState>();
                bool sky = false;
                var pilot = new BattlePilot(false, BattlePilot.Mistake.WrongReason, BattlePilot.Mistake.Press);
                for (int i = 0; i < 60 * 400 && !flow.Over; i++)
                {
                    flow.Tick(1f / 60f, pilot.Steer(flow, 1f / 60f));
                    if (i % 3 == 0) stage.Render(flow, 3f / 60f, "hints", null, false, 1f);
                    seen.Add(flow.State);
                    sky |= flow.Sky;
                }
                Assert.That(flow.Over, Is.True, presenter + ": the battle ends");
                var states = presenter == ArgumentPresenter.Sections
                    ? new[] { BattleState.Argue, BattleState.Pressure, BattleState.Because, BattleState.Showdown, BattleState.Ending }
                    : new[] { BattleState.Debate, BattleState.Because, BattleState.Showdown, BattleState.Ending };
                foreach (var st in states) Assert.That(seen, Does.Contain(st), presenter + ": " + st);
                if (presenter == ArgumentPresenter.Sections) Assert.That(sky, Is.True, "the sections reached her sky");
            }
            stage.ShowCaseFile(flow, true);
            Assert.That(stage.CaseFileShown, Is.True);
            stage.ShowCaseFile(flow, false);
            stage.ShowEnd("Convinced", "Lena is unsure.", "", "Continue", true);
            Assert.That(stage.EndShown, Is.True);
            stage.Remove();
        }

        [Test]
        public void TheTableIsDrawnInTheSpritesStyle()
        {
            var kitchen = StageArt.Table("kitchen_table");
            Assert.That(kitchen, Is.Not.Null);
            Assert.That(kitchen.width, Is.EqualTo(StageArt.TableWidth));
            Assert.That(kitchen.height, Is.EqualTo(StageArt.TableHeight));
            Assert.That(StageArt.Table("larch_street_foot"), Is.Not.SameAs(kitchen), "Walt has his own step");
            Assert.That(StageArt.Table("kitchen_table"), Is.SameAs(kitchen), "drawn once");
            Assert.That(StageArt.Vignette(), Is.Not.Null);
            Assert.That(StageArt.Grain(), Is.Not.Null);
        }

        [Test]
        public void TheBattleCaptureFlagsParse()
        {
            Assert.That(ArgumentBattleShot.Parse(new[] { "VisitingHours.exe" }, out _), Is.False, "nothing without a flag");
            Assert.That(ArgumentBattleShot.Parse(new[] { "game.exe", "-battle-shot", "C:/out/stills" }, out var r), Is.True);
            Assert.That(r.record, Is.False);
            Assert.That(r.folder, Is.EqualTo("C:/out/stills"));
            Assert.That(ArgumentBattleShot.Parse(new[] { "game.exe", "-battle-record", "C:/out/run", "-battle-record-every", "0.25", "-battle-record-battle", "walt_night1" }, out r), Is.True);
            Assert.That(r.record, Is.True);
            Assert.That(r.every, Is.EqualTo(0.25f));
            Assert.That(r.battle, Is.EqualTo(WaltNight1.Id));
            Assert.That(ArgumentBattleShot.Parse(new[] { "game.exe", "-battle-record" }, out r), Is.True);
            Assert.That(r.every, Is.EqualTo(0.5f), "a frame every half second by default");
            Assert.That(r.battle, Is.EqualTo(ArgumentBattles.Lena));
        }
    }
}
