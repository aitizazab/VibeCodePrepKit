using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>The title table laid out to the creator's sketch (art/review/2026-10-12/title-screen/creator-sketch.png), seen
    /// from straight above: the tablet upper left running off the left edge, the quit piece upper right, the notepad cut by
    /// the bottom, the badge bottom right; the quit piece is the mug, or on the Despair route the "Give Up" pill bottle; the
    /// title-shot flags parse.</summary>
    public class VisitingHoursTitleLayoutTests
    {
        VhStage _stage;

        VhStage Build(string variant)
        {
            var boot = AssetDatabase.LoadAssetAtPath<VisitingHoursBoot>(EditorTools.VisitingHoursContent.BootPath);
            Assert.That(boot, Is.Not.Null, "Run JamKit > Rebuild Shell Scenes");
            VhSceneEdits.Source = () => new VhSceneEditsData();
            VhStageBuilder.QuitVariantOverride = variant;
            _stage = VhStageBuilder.Build(boot, false);
            return _stage;
        }

        [TearDown]
        public void TearDown()
        {
            VhSceneEdits.Source = null;
            VhStageBuilder.QuitVariantOverride = null;
            if (_stage != null)
            {
                _stage.DisposeOwned();
                Object.DestroyImmediate(_stage.gameObject);
            }
            _stage = null;
        }

        /// <summary>Where a world point lands in the title shot (0..1 from the left and from the BOTTOM).</summary>
        static Vector3 InShot(VhStage s, Vector3 world)
        {
            int i = s.PoseIndex("table");
            var go = new GameObject("probe camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.aspect = 16f / 9f;
                cam.fieldOfView = s.poseFov[i];
                cam.transform.SetPositionAndRotation(s.posePositions[i], s.poseRotations[i]);
                return cam.WorldToViewportPoint(world);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [Test]
        public void TheTableIsLaidOutLikeTheSketch()
        {
            var s = Build("normal");
            int i = s.PoseIndex("table");
            Assert.That(Vector3.Dot(s.poseRotations[i] * Vector3.forward, Vector3.down), Is.GreaterThan(0.99f), "the title looks straight down");
            var tablet = s.table.Find("tablet").transform;
            var tb = BoundsOf(tablet);
            Vector3 tc = InShot(s, tb.center);
            Assert.That(tc.x < 0.45f && tc.y > 0.5f, Is.True, "the tablet is upper left: " + tc);
            Assert.That(InShot(s, new Vector3(tb.min.x, tb.center.y, tb.center.z)).x, Is.LessThan(0.02f), "it runs off the left edge");
            Assert.That(InShot(s, new Vector3(tb.max.x, tb.center.y, tb.center.z)).x, Is.InRange(0.6f, 0.8f), "its right side about 70% across");
            Vector3 mug = InShot(s, s.table.Find("mug").transform.position);
            Assert.That(mug.x > 0.75f && mug.y > 0.5f, Is.True, "the quit piece is upper right: " + mug);
            Assert.That(InShot(s, BoundsOf(s.table.Find("mug").transform).max).x, Is.GreaterThan(1f), "the mug's handle is cut by the right edge");
            var pad = BoundsOf(s.table.Find("notepad").transform);
            Assert.That(InShot(s, new Vector3(pad.center.x, pad.max.y, pad.min.z)).y, Is.LessThan(0f), "the notepad runs off the bottom");
            Assert.That(InShot(s, new Vector3(pad.center.x, pad.max.y, pad.max.z)).y, Is.InRange(0.05f, 0.35f), "its top shows at the bottom");
            Vector3 badge = InShot(s, s.table.Find("badge").transform.position);
            Assert.That(badge.x > 0.75f && badge.y < 0.35f, Is.True, "the badge is bottom right: " + badge);
            // The coffee's "Quit" reads straight: the coffee isn't turned in the shot.
            var coffee = s.table.Find("mug").transform.Find("Coffee");
            Assert.That(coffee, Is.Not.Null);
            Assert.That(Quaternion.Angle(coffee.rotation, Quaternion.identity), Is.LessThan(1f), "\"Quit\" reads straight from above");
        }

        [Test]
        public void TheDespairRouteMakesTheQuitPieceAGiveUpPillBottle()
        {
            var normal = Build("normal");
            var mug = normal.table.Find("mug");
            Assert.That(mug.transform.Find("Coffee"), Is.Not.Null, "the normal route: the mug");
            Assert.That(mug.action, Is.EqualTo(VhTitleTable.QuitAvailable ? VhTitleAction.Quit : VhTitleAction.None));
            TearDown();
            var despair = Build("despair");
            var bottle = despair.table.Find("mug");
            Assert.That(bottle.transform.Find("Bottle"), Is.Not.Null, "the Despair route: the pill bottle");
            Assert.That(bottle.transform.Find("Cap"), Is.Not.Null);
            Assert.That(bottle.transform.Find("Label"), Is.Not.Null, "labelled Give Up");
            Assert.That(bottle.transform.Find("Coffee"), Is.Null);
            Assert.That(bottle.action, Is.EqualTo(VhTitleTable.QuitAvailable ? VhTitleAction.Quit : VhTitleAction.None), "it still quits");
            Assert.That(bottle.GetComponentInChildren<Collider>(), Is.Not.Null, "it can be clicked");
        }

        [Test]
        public void TheTitleShotFlagsParse()
        {
            Assert.That(VhTitleShot.Parse(new[] { "game.exe" }, out _, out _), Is.False);
            Assert.That(VhTitleShot.Parse(new[] { "game.exe", "-title-shot", "C:/out/t.png", "-title-variant", "Despair" }, out var path, out var variant), Is.True);
            Assert.That(path, Is.EqualTo("C:/out/t.png"));
            Assert.That(variant, Is.EqualTo("despair"));
            Assert.That(VhTitleShot.Parse(new[] { "game.exe", "-title-shot" }, out path, out variant), Is.True);
            Assert.That(path, Is.EqualTo("title-shot.png"));
            Assert.That(variant, Is.Null);
        }
    }
}
