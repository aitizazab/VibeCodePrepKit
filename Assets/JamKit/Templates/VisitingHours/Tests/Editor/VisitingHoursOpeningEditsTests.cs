using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>The opening editor's saved overrides (docs/OPENING-EDITOR.md): they round-trip through JSON, the builder
    /// applies them by stable ids every time it builds the sets (unknown ids are skipped with a warning), what follows a
    /// moved thing follows it, and with none the opening is exactly as built.</summary>
    public class VisitingHoursOpeningEditsTests
    {
        static VisitingHoursBoot Boot()
        {
            var b = AssetDatabase.LoadAssetAtPath<VisitingHoursBoot>(EditorTools.VisitingHoursContent.BootPath);
            Assert.That(b, Is.Not.Null, "Run JamKit > Rebuild Shell Scenes");
            return b;
        }

        readonly List<VhStage> _built = new List<VhStage>();

        VhStage Build(VhSceneEditsData edits)
        {
            VhSceneEdits.Source = () => edits;
            var s = VhStageBuilder.Build(Boot(), false);
            _built.Add(s);
            return s;
        }

        [TearDown]
        public void TearDown()
        {
            VhSceneEdits.Source = null;
            foreach (var s in _built)
                if (s != null)
                {
                    s.DisposeOwned();
                    Object.DestroyImmediate(s.gameObject);
                }
            _built.Clear();
        }

        /// <summary>Which ids differ, and how (for the messages).</summary>
        static string Describe(VhSceneEditsData d)
        {
            var parts = new List<string>();
            foreach (var o in d.objects) parts.Add(o.id + " (position " + o.position.ToString("F4") + ", turn " + o.rotation.ToString("F2") + ", size " + o.scale.ToString("F3") + ")");
            foreach (var o in d.shots) parts.Add(o.id + " (position " + o.position.ToString("F4") + ", turn " + o.rotation.ToString("F2") + ", fov " + o.fov.ToString("F2") + ")");
            return parts.Count == 0 ? "none" : string.Join("; ", parts);
        }

        [Test]
        public void EditsRoundTripThroughJson()
        {
            var d = new VhSceneEditsData();
            d.objects.Add(new VhObjectEdit { id = "phone", position = new Vector3(1f, 2f, 3f), rotation = new Vector3(0f, 45f, 0f), scale = new Vector3(1f, 1.5f, 1f) });
            d.shots.Add(new VhShotEdit { id = "shot:table", position = new Vector3(0f, 1.2f, -0.5f), rotation = new Vector3(20f, 0f, 0f), fov = 40f });
            var back = VhSceneEditsData.FromJson(d.ToJson());
            Assert.That(back.objects.Count, Is.EqualTo(1));
            Assert.That(back.Object("phone").position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(back.Object("phone").scale, Is.EqualTo(new Vector3(1f, 1.5f, 1f)));
            Assert.That(back.Shot("shot:table").fov, Is.EqualTo(40f));
            Assert.That(VhSceneEditsData.FromJson("").IsEmpty, Is.True);
        }

        [Test]
        public void WithNoEditsTheSetsAreAsBuiltAndTheIdsAreStable()
        {
            var s = Build(new VhSceneEditsData());
            Assert.That(s.edits, Is.Not.Null);
            var differs = s.edits.Capture();
            Assert.That(differs.IsEmpty, Is.True, "nothing differs from the built scene, but these do: " + Describe(differs));
            var ids = new HashSet<string>();
            foreach (var e in s.edits.All)
            {
                Assert.That(ids.Add(e.id), Is.True, "unique id " + e.id);
                Assert.That(e.label, Is.Not.Empty, e.id);
            }
            foreach (var id in new[] { "bed", "nightstand", "lamp", "phone", "photo-frame", "streetlight", "moon", "title-table", "title-room",
                         "title-tablet", "title-mug", "title-notepad", "title-pen", "title-badge", "title-lamp",
                         "shot:pillow", "shot:nightstand", "shot:away", "shot:overhead", "shot:table" })
                Assert.That(ids.Contains(id), Is.True, "editable: " + id);
            if (s.nadia != null) Assert.That(ids.Contains("nadia"), Is.True);
            // A second build has the same ids, in the same order.
            var again = Build(new VhSceneEditsData());
            Assert.That(again.edits.All.Count, Is.EqualTo(s.edits.All.Count));
            for (int i = 0; i < s.edits.All.Count; i++) Assert.That(again.edits.All[i].id, Is.EqualTo(s.edits.All[i].id));
        }

        [Test]
        public void SavedEditsApplyWhenTheSetsAreBuilt()
        {
            var plain = Build(new VhSceneEditsData());
            var phone = plain.edits.Find("phone");
            Vector3 restBefore = plain.phoneRestPos;
            var d = new VhSceneEditsData();
            d.objects.Add(new VhObjectEdit { id = "phone", position = phone.defaultPosition + new Vector3(0f, 0f, 0.05f), rotation = phone.defaultRotation.eulerAngles, scale = Vector3.one });
            var shot = plain.edits.Find("shot:table");
            d.shots.Add(new VhShotEdit { id = "shot:table", position = shot.defaultPosition + new Vector3(0f, 0.1f, 0f), rotation = shot.defaultRotation.eulerAngles, fov = 40f });
            d.objects.Add(new VhObjectEdit { id = "no-such-thing", position = Vector3.one });
            LogAssert.Expect(LogType.Warning, new Regex("no-such-thing"));

            var s = Build(d);
            Assert.That(Vector3.Distance(s.phoneRestPos, restBefore + new Vector3(0f, 0f, 0.05f)), Is.LessThan(1e-3f), "the phone rests where it was put");
            Assert.That(Vector3.Distance(s.phone.position, s.phoneRestPos), Is.LessThan(1e-4f), "and lies there");
            int t = s.PoseIndex("table");
            Assert.That(s.poseFov[t], Is.EqualTo(40f).Within(1e-3f), "the title shot's field of view");
            Assert.That(s.posePositions[t].y - plain.posePositions[plain.PoseIndex("table")].y, Is.EqualTo(0.1f).Within(1e-3f), "the title shot moved up");
            // What it saves is what it was given (the unknown id dropped).
            var back = s.edits.Capture();
            Assert.That(back.objects.Count, Is.EqualTo(1), "only the phone differs: " + Describe(back));
            Assert.That(Vector3.Distance(back.Object("phone").position, d.Object("phone").position), Is.LessThan(1e-4f));
            Assert.That(back.Shot("shot:table").fov, Is.EqualTo(40f).Within(1e-3f));
        }

        [Test]
        public void EditsApplyAtRuntimeAndResetToTheBuiltScene()
        {
            var s = Build(new VhSceneEditsData());
            var phone = s.edits.Find("phone");
            Vector3 rest = s.phoneRestPos;
            var d = new VhSceneEditsData();
            d.objects.Add(new VhObjectEdit { id = "phone", position = phone.defaultPosition + new Vector3(0.02f, 0f, 0f), rotation = phone.defaultRotation.eulerAngles, scale = Vector3.one });
            s.edits.ApplyAll(d);
            Assert.That(Vector3.Distance(s.phoneRestPos, rest), Is.EqualTo(0.02f).Within(1e-3f));
            s.edits.ApplyAll(new VhSceneEditsData());
            Assert.That(Vector3.Distance(s.phoneRestPos, rest), Is.LessThan(1e-4f), "reset puts it back");
            Assert.That(s.edits.Capture().IsEmpty, Is.True);
        }

        [Test]
        public void MovingThingsCarriesWhatFollowsThem()
        {
            var s = Build(new VhSceneEditsData());
            // The nightstand carries the phone (its resting place) and the lamp.
            Vector3 rest = s.phoneRestPos;
            var lamp = s.edits.Find("lamp");
            Vector3 lampAt = lamp.target.position;
            s.edits.MoveBy(s.edits.Find("nightstand"), Matrix4x4.Translate(new Vector3(0f, 0f, -0.1f)));
            Assert.That(Vector3.Distance(s.phoneRestPos, rest + new Vector3(0f, 0f, -0.1f)), Is.LessThan(1e-4f), "the phone went with the nightstand");
            Assert.That(Vector3.Distance(lamp.target.position, lampAt + new Vector3(0f, 0f, -0.1f)), Is.LessThan(1e-4f), "the lamp too");
            // The bed carries Nadia's shoulder and the shots from her eyes.
            Vector3 shoulder = s.shoulder;
            int pillow = s.PoseIndex("pillow");
            Vector3 eye = s.posePositions[pillow];
            s.edits.MoveBy(s.edits.Find("bed"), Matrix4x4.Translate(new Vector3(0.1f, 0f, 0f)));
            Assert.That(Vector3.Distance(s.shoulder, shoulder + new Vector3(0.1f, 0f, 0f)), Is.LessThan(1e-4f), "her shoulder went with the bed");
            Assert.That(Vector3.Distance(s.posePositions[pillow], eye + new Vector3(0.1f, 0f, 0f)), Is.LessThan(1e-4f), "and the shot from her eyes");
            var saved = s.edits.Capture();
            Assert.That(saved.Object("bed"), Is.Not.Null);
            Assert.That(saved.Shot("shot:pillow"), Is.Not.Null, "the carried shot is saved too");
        }
    }
}
