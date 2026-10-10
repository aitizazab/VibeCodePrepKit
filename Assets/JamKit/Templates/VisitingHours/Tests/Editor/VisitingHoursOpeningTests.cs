using System.IO;
using JamKit.Shell;
using JamKit.VisitingHours.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>The opening and the title table as built (docs/OPENING-AND-TITLE.md): the boot asset is
    /// wired (cues, recordings, the style-E material, the models), the cues resolve in order and inside
    /// the real recordings, the licences are recorded, Nadia's model imports for the opening, and the
    /// sets build with every title object wired to its action.</summary>
    public class VisitingHoursOpeningTests
    {
        static VisitingHoursBoot Boot()
        {
            var b = AssetDatabase.LoadAssetAtPath<VisitingHoursBoot>(VisitingHoursContent.BootPath);
            Assert.That(b, Is.Not.Null, "Run JamKit > Rebuild Shell Scenes");
            return b;
        }

        [Test]
        public void TheBootAssetIsWired()
        {
            var b = Boot();
            Assert.That(b.voicemail, Is.Not.Null, "Audio/vo_elias_voicemail.wav");
            Assert.That(b.newsAnchor, Is.Not.Null, "Audio/vo_news_anchor.wav");
            Assert.That(b.wakingMaterial, Is.Not.Null);
            Assert.That(b.wakingMaterial.shader.name, Is.EqualTo(VisitingHoursOpeningContent.WakingShader));
            Assert.That(b.headline, Does.StartWith("ROUTE 9"));
            Assert.That(b.headline, Does.Not.Contain("WeWork").IgnoreCase, "the corporation is Larpaca");
            Assert.That(b.dataVersion, Is.EqualTo(VisitingHoursOpeningContent.DataVersion));
            foreach (var (slot, file) in VisitingHoursOpeningContent.Models)
                Assert.That(b.Model(slot), Is.Not.Null, slot + " (" + file + ".fbx)");
            int real = 0;
            foreach (var (slot, _) in VisitingHoursOpeningContent.ArtSlots) if (b.Art(slot) != null) real++;
            TestContext.WriteLine("Opening art: " + real + " of " + VisitingHoursOpeningContent.ArtSlots.Length + " pictures from Art/, the rest drawn in code; news frames: " + (b.Art("news_0") != null));
        }

        [Test]
        public void TheCuesAreOrderedAndInsideTheRecordings()
        {
            var b = Boot();
            var problems = OpeningScript.Problems(b.cues, b.tracks, b.ClipLength);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            var cues = b.Resolve(null);
            Assert.That(cues.Count, Is.GreaterThanOrEqualTo(b.cues.Count));
            for (int i = 1; i < cues.Count; i++) Assert.That(cues[i].at, Is.GreaterThanOrEqualTo(cues[i - 1].at), "cue " + i);
            foreach (var tr in b.tracks)
            {
                float len = b.ClipLength(tr.clip);
                Assert.That(len, Is.GreaterThan(0f), tr.clip + " is in Audio/");
                foreach (var m in tr.markers) Assert.That(m.at * OpeningScript.Stretch(tr, b.ClipLength), Is.LessThan(len), tr.name + ": " + m.name);
            }
            float news = 0f, interactive = -1f;
            foreach (var c in cues)
            {
                if (c.kind == OpeningCueKind.Voice && c.text == "news") news = c.at;
                if (c.kind == OpeningCueKind.Interactive) interactive = c.at;
            }
            Assert.That(interactive, Is.EqualTo(news + b.newsAnchor.length).Within(0.05f), "the title takes input when the report ends");
            foreach (var c in cues)
                if (c.kind == OpeningCueKind.Show) Assert.That(SliceContent.TablePieces, Does.Contain(c.text));
        }

        [Test]
        public void EveryDownloadIsCredited()
        {
            string third = VisitingHoursBuilder.Root + "/ThirdParty";
            Assert.That(File.Exists(third + "/LICENSES.md"), Is.True);
            Assert.That(File.Exists(VisitingHoursOpeningContent.KenneyDir + "/License.txt"), Is.True);
            string licences = File.ReadAllText(third + "/LICENSES.md");
            foreach (var (_, file) in VisitingHoursOpeningContent.Models) Assert.That(licences, Does.Contain(file), file + " is listed");
        }

        [Test]
        public void NadiasModelImportsForTheOpening()
        {
            if (!File.Exists(VisitingHoursOpeningContent.NadiaPath))
            {
                Assert.Ignore("Nadia's model isn't in the project yet (the stand-in is used).");
                return;
            }
            var b = Boot();
            Assert.That(b.nadiaModel, Is.Not.Null);
            Assert.That(b.nadiaTexture, Is.Not.Null, "Color.jpg next to her FBX");
            var imp = (ModelImporter)AssetImporter.GetAtPath(VisitingHoursOpeningContent.NadiaPath);
            Assert.That(imp.globalScale, Is.EqualTo(VisitingHoursOpeningContent.NadiaScale).Within(1e-3f));
            Assert.That(imp.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
            foreach (var bone in new[] { "Hips", "Head", "RightArm", "RightForeArm", "RightHand" })
                Assert.That(VhModelNadia.Bone(b.nadiaModel.transform, bone), Is.Not.Null, bone);
        }

        [Test]
        public void TheSetsBuildWithEveryTitleObjectWired()
        {
            var b = Boot();
            var stage = VhStageBuilder.Build(b, false);
            try
            {
                Assert.That(stage.cam, Is.Not.Null);
                foreach (var pose in SliceContent.CameraPoses) Assert.That(stage.PoseIndex(pose), Is.GreaterThanOrEqualTo(0), "camera pose " + pose);
                foreach (var piece in SliceContent.TablePieces) Assert.That(stage.table.Find(piece), Is.Not.Null, piece);
                Assert.That(stage.table.Find("tablet").action, Is.EqualTo(VhTitleAction.Play));
                Assert.That(stage.table.Find("mug").action, Is.EqualTo(VhTitleAction.Quit));
                Assert.That(stage.table.Find("badge").action, Is.EqualTo(VhTitleAction.Settings));
                Assert.That(stage.table.Find("notepad").action, Is.EqualTo(VhTitleAction.Credits));
                Assert.That(stage.table.Find("pen").Group, Is.EqualTo("notepad"), "the pen goes with the notepad");
                Assert.That(stage.table.Find("table").action, Is.EqualTo(VhTitleAction.None));
                foreach (var id in new[] { "tablet", "mug", "badge", "notepad", "pen" })
                {
                    var p = stage.table.Find(id);
                    Assert.That(p.renderers.Length, Is.GreaterThan(0), id);
                    Assert.That(p.GetComponentInChildren<Collider>(), Is.Not.Null, id + " can be clicked");
                }
                Assert.That(stage.phone, Is.Not.Null);
                Assert.That(stage.phoneScreenMat, Is.Not.Null);
                Assert.That(stage.tabletScreenMat, Is.Not.Null);
                Assert.That(stage.street, Is.Not.Null);
                Assert.That(stage.moon, Is.Not.Null);
                Assert.That(stage.tableKey, Is.Not.Null);
                Assert.That(stage.blanket, Is.Not.Null);
                Assert.That(stage.blanket.sharedMesh.blendShapeCount, Is.EqualTo(1), "she breathes under the blanket");
                Assert.That(stage.nadia, Is.Not.Null);
                Assert.That(stage.nadia.IsModel, Is.EqualTo(b.nadiaModel != null), "her model when it's there, the stand-in otherwise");
                Assert.That(stage.voiceClean, Is.Not.Null);
                Assert.That(stage.voicePhone.GetComponent<AudioHighPassFilter>(), Is.Not.Null, "the phone's small speaker");
                Assert.That(stage.voicePhone.GetComponent<AudioLowPassFilter>(), Is.Not.Null);
                Assert.That(stage.voicePhone.GetComponent<AudioDistortionFilter>(), Is.Not.Null);
                Assert.That(stage.MoveCamera("table", 0f), Is.True);
                Assert.That(stage.InBedroom, Is.False);
                Assert.That(stage.MoveCamera("nowhere", 0f), Is.False);
                TestContext.WriteLine("Missing (drawn in code): " + string.Join(", ", stage.missingArt));
            }
            finally
            {
                stage.DisposeOwned();
                Object.DestroyImmediate(stage.gameObject);
            }
        }

        [Test]
        public void TheOpeningsSoundsAreMade()
        {
            foreach (var id in new[] { "vh_phone_ring", "vh_vibrate", "vh_rustle", "vh_rustle_big", "vh_clunk", "vh_tap", "vh_breath" })
            {
                Assert.That(ProceduralAudio.Knows(id), Is.True, id);
                Assert.That(ProceduralAudio.Get(id), Is.Not.Null, id);
            }
            Assert.That(VhStage.BreathAt(0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(VhStage.BreathAt(1.8f), Is.EqualTo(1f).Within(1e-4f), "full at the top of the breath");
            Assert.That(VhStage.BreathAt(4.5f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void TheHeadlineComesFromTheScript()
        {
            string line = VisitingHoursOpeningContent.ReadHeadline();
            if (string.IsNullOrEmpty(line)) Assert.Ignore("script/opening-script.md has no headline section");
            Assert.That(line, Does.Contain("ROUTE 9").IgnoreCase);
            Assert.That(SliceContent.Headline(line), Does.Not.Contain("WeWork").IgnoreCase);
        }
    }
}
