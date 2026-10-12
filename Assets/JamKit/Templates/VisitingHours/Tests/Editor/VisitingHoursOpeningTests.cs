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

        /// <summary>The world bounds of everything drawn under <paramref name="t"/>.</summary>
        static Bounds RendererBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            Assert.That(rs.Length, Is.GreaterThan(0), t.name + " has something drawn");
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
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
            Assert.That(b.headline, Does.Not.Contain("WeWork").IgnoreCase, "Larpaca is the gang (no WeWork placeholder)");
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
                Assert.That(stage.table.Find("room").action, Is.EqualTo(VhTitleAction.None));
                // The framed photo and the phone stand on the nightstand: on its top, inside its edges.
                // (Under the opening editor's handle, "Edit: the nightstand".)
                Transform stand = null;
                foreach (var t in stage.bedroom.GetComponentsInChildren<Transform>(true))
                    if (stand == null && (t.name == "nightstand" || t.name == "Nightstand")) stand = t;
                Assert.That(stand, Is.Not.Null);
                var sb = RendererBounds(stand);
                foreach (var name in new[] { "Framed photo", "Phone" })
                {
                    var thing = stage.bedroom.Find(name);
                    Assert.That(thing, Is.Not.Null, name);
                    var tb = RendererBounds(thing);
                    Assert.That(tb.min.y, Is.EqualTo(sb.max.y).Within(0.006f), name + " sits on the nightstand's top");
                    // The photo all on it; the phone mostly (its end over the bed-side edge, to be taken by it).
                    float allow = name == "Phone" ? VhStageBuilder.PhoneOverhang + 0.01f : 0f;
                    Assert.That(tb.min.x >= sb.min.x && tb.max.x <= sb.max.x + allow && tb.min.z >= sb.min.z && tb.max.z <= sb.max.z, Is.True, name + " is on the nightstand");
                    Assert.That(tb.center.x < sb.max.x - 0.02f, Is.True, name + " rests on it (its middle over the wood)");
                }
                // The photo faces her pillow (not its back).
                var photo = stage.bedroom.Find("Framed photo/Photo");
                Assert.That(photo, Is.Not.Null);
                Vector3 eyes = stage.bedroom.TransformPoint(VhStageBuilder.Eyes);
                Assert.That(Vector3.Dot(photo.rotation * Vector3.back, eyes - photo.position), Is.GreaterThan(0f), "the photo faces her");
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

        static readonly System.Reflection.MethodInfo StageLateUpdate =
            typeof(VhStage).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// When she holds the phone up, her hand is there: the hand bone at the grip, in the camera's view, and her skinned
        /// mesh round it (the capture once showed the phone floating with no hand). Logs what it measures (JAMKIT_VH_HAND).
        /// </summary>
        [Test]
        public void HerHandHoldsThePhoneInView()
        {
            var stage = VhStageBuilder.Build(Boot(), false);
            try
            {
                var model = stage.nadia as VhModelNadia;
                if (model == null) Assert.Ignore("Nadia's model isn't imported: the stand-in arm is used");
                stage.ResetForOpening();
                stage.MoveCamera("nightstand", 0f);
                stage.PlayArm("lift", 0f);
                for (int i = 0; i < 3; i++) StageLateUpdate.Invoke(stage, null);
                stage.GripPose(1f, out var wrist, out _);
                var cam = stage.cam;
                Vector3 hand = model.rHand.position;
                Vector3 view = cam.WorldToViewportPoint(hand);
                var log = new System.Text.StringBuilder();
                log.Append("JAMKIT_VH_HAND wrist target ").Append(wrist.ToString("F3")).Append(" hand bone ").Append(hand.ToString("F3"))
                    .Append(" fore ").Append(model.rFore.position.ToString("F3")).Append(" arm ").Append(model.rArm.position.ToString("F3"))
                    .Append(" shoulder ").Append(stage.shoulder.ToString("F3")).Append(" phone ").Append(stage.phone.position.ToString("F3"))
                    .Append(" cam ").Append(cam.transform.position.ToString("F3")).Append(" near ").Append(cam.nearClipPlane)
                    .Append(" viewport ").Append(view.ToString("F3"));
                // Her skinned mesh as drawn now: how much of it is round the hand bone, and where it all is.
                var baked = new Mesh();
                var verts = new System.Collections.Generic.List<Vector3>();
                int near = 0;
                float closest = float.MaxValue;
                var all = new Bounds();
                bool first = true;
                foreach (var smr in model.model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.BakeMesh(baked, true);
                    baked.GetVertices(verts);
                    var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    foreach (var v in verts)
                    {
                        Vector3 p = m.MultiplyPoint3x4(v);
                        if (first) { all = new Bounds(p, Vector3.zero); first = false; }
                        else all.Encapsulate(p);
                        float d = Vector3.Distance(p, hand);
                        if (d < closest) closest = d;
                        if (d < 0.08f) near++;
                    }
                    log.Append(" | smr ").Append(smr.name).Append(" enabled ").Append(smr.enabled).Append(" active ").Append(smr.gameObject.activeInHierarchy)
                        .Append(" bones ").Append(smr.bones.Length).Append(" rootBone ").Append(smr.rootBone != null ? smr.rootBone.name : "none")
                        .Append(" handInBones ").Append(System.Array.IndexOf(smr.bones, model.rHand) >= 0);
                }
                Object.DestroyImmediate(baked);
                log.Append(" | vertices within 8 cm of the hand ").Append(near).Append(" closest ").Append(closest.ToString("F3")).Append(" mesh bounds ").Append(all.ToString("F3"));
                Debug.Log(log.ToString());
                TestContext.WriteLine(log.ToString());
                // The grip moves her wrist off and along the phone from the stage's grip point (SeatedWrist): she reaches it.
                Assert.That(Vector3.Distance(hand, model.SeatedWrist), Is.LessThan(0.015f), "the hand reaches the grip");
                Assert.That(Vector3.Distance(hand, wrist), Is.LessThan(0.15f), "the hand is near the stage's grip point");
                Assert.That(view.z > cam.nearClipPlane && view.x > 0f && view.x < 1f && view.y > 0f && view.y < 1f, Is.True, "the hand is in view");
                Assert.That(near, Is.GreaterThan(50), "her skinned hand is round the hand bone");
                // Nothing of her hand inside the phone: the knuckles, the finger joints and their tips are all outside it.
                Vector3 half = stage.phoneSize * 0.5f;
                Vector3 centre = stage.phone.position + stage.phone.rotation * new Vector3(0f, half.y, 0f);
                foreach (var t in model.model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf("RightHand", System.StringComparison.Ordinal) < 0) continue;
                    Vector3 q = Quaternion.Inverse(stage.phone.rotation) * (t.position - centre);
                    bool inside = Mathf.Abs(q.x) < half.x && Mathf.Abs(q.y) < half.y && Mathf.Abs(q.z) < half.z;
                    Assert.That(inside, Is.False, t.name + " is inside the phone");
                }
                // Her hand is behind the phone (on the side away from her eyes), the fingertips past its far edge and the
                // thumb beside its near edge: no bone of her hand, the thumb's included, lies over the screen on the
                // camera's side (only a tap puts the thumb there).
                Vector3 toEye = cam.transform.position - centre;
                Vector3 normal = stage.phone.rotation * Vector3.up;
                if (Vector3.Dot(normal, toEye) < 0f) normal = -normal;
                Vector3 side = stage.phone.rotation * Vector3.right, length = stage.phone.rotation * Vector3.forward;
                foreach (var t in model.model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf("RightHand", System.StringComparison.Ordinal) < 0) continue;
                    Vector3 d = t.position - centre;
                    bool overScreen = Mathf.Abs(Vector3.Dot(d, side)) < half.x - 0.004f && Mathf.Abs(Vector3.Dot(d, length)) < half.z - 0.004f;
                    bool cameraSide = Vector3.Dot(d, normal) > half.y + 0.004f;
                    Assert.That(overScreen && cameraSide, Is.False, t.name + " lies over the screen on her side of the phone (the hand should be behind it)");
                }
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
