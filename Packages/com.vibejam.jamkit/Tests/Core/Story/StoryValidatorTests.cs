using System.Linq;
using JamKit.Narrative;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class InkScanTests
    {
        const string Ink = @"// A comment with #notatag
EXTERNAL playthrough_count()
VAR trust = 0
LIST Items = brass_key, (photo), ticket
-> platform

=== platform ===
#bg:platform #music:music_story_rain
Iris: Hello. #show:iris smile
* [Stay] -> office.desk
* [Go] -> leave
- (after)
/* a block
   comment #nope */
Done. -> END

=== office ===
= desk
The desk. #sfx:drawer -> END

=== leave ===
Bye. #ending:last_train
-> END

=== orphan ===
Nobody comes here.
-> END

=== function helper(x) ===
~ return x
";

        [Test]
        public void FindsKnotsTagsDivertsAndLists()
        {
            var scan = InkScan.Read("Test.ink", Ink);
            Assert.That(scan.knots.Select(k => k.name), Is.EqualTo(new[] { "platform", "office", "leave", "orphan", "helper" }));
            Assert.That(scan.FindKnot("helper").isFunction, Is.True);
            Assert.That(scan.FindKnot("office").stitches, Does.Contain("desk"));
            Assert.That(scan.tags.Select(t => t.text), Is.EqualTo(new[] { "bg:platform", "music:music_story_rain", "show:iris smile", "sfx:drawer", "ending:last_train" }));
            Assert.That(scan.tags[0].line, Is.EqualTo(8));
            Assert.That(scan.tags[3].knot, Is.EqualTo("office"));
            Assert.That(scan.lists["Items"], Is.EqualTo(new[] { "brass_key", "photo", "ticket" }));
            Assert.That(scan.variables, Does.Contain("trust"));
            Assert.That(scan.externals, Does.Contain("playthrough_count"));
            Assert.That(scan.labels, Does.Contain("after"));
            Assert.That(scan.rootTargets, Does.Contain("platform"));
            var reach = scan.Reachable(null);
            Assert.That(reach, Does.Contain("leave"));
            Assert.That(reach, Does.Contain("office"));
            Assert.That(reach, Does.Not.Contain("orphan"));
        }

        [Test]
        public void TagEndsAtADivertAndDynamicTagsAreMarked()
        {
            var scan = InkScan.Read("t.ink", "=== a ===\nHi #bg:x -> b\nYo #show:{who} #wait:1\n{x: Iris: Hi. #show:iris smile} and on\n=== b ===\n-> END\n");
            Assert.That(scan.tags[0].text, Is.EqualTo("bg:x"));
            Assert.That(scan.tags[1].dynamic, Is.True);
            Assert.That(scan.tags[1].text, Is.EqualTo("show:{who}"));
            Assert.That(scan.tags[2].text, Is.EqualTo("wait:1"));
            Assert.That(scan.tags[3].text, Is.EqualTo("show:iris smile"), "a tag inside {inline logic} ends at its closing brace");
        }
    }

    public class StoryValidatorTests
    {
        static StoryIds Ids()
        {
            var ids = new StoryIds();
            ids.backgrounds.Add("platform");
            ids.cgs.Add("photo");
            ids.sounds.Add("music_story_rain");
            ids.sounds.Add("drawer");
            ids.endings.Add("last_train");
            ids.hotspotScenes.Add("office");
            ids.items.Add("brass_key");
            ids.items.Add("photo");
            ids.items.Add("ticket");
            ids.palette.Add("accent");
            ids.AddCharacter("iris", "Iris", new[] { "neutral", "smile", "sad" });
            return ids;
        }

        const string Good = @"-> start
=== start ===
#bg:platform fade #music:music_story_rain
Iris: Hello, <c=accent>you</c>.<pause=0.3> #show:iris smile left
#investigate:office
-> DONE
=== desk ===
A drawer. #sfx:drawer
-> DONE
=== finish ===
Bye. #ending:last_train
-> END
";

        static HotspotSceneInfo Office()
        {
            var s = new HotspotSceneInfo { id = "office", background = "platform" };
            var desk = new HotspotInfo { id = "desk", label = "Desk", shape = HotspotShape.Rect(0.1f, 0.1f, 0.3f, 0.3f), knot = "desk", showIf = "!item:ticket" };
            desk.itemKnots.Add(new System.Collections.Generic.KeyValuePair<string, string>("brass_key", "finish"));
            s.hotspots.Add(desk);
            s.walkthrough.Add("desk");
            s.walkthrough.Add("brass_key>desk");
            return s;
        }

        [Test]
        public void ValidStoryHasNoErrors()
        {
            var ids = Ids();
            var scan = InkScan.Read("Good.ink", Good + "LIST Items = brass_key, photo, ticket\n");
            var p = StoryValidator.Validate(scan, ids, new[] { Office() });
            Assert.That(p.Errors(), Is.Empty, p.ToString());
        }

        [Test]
        public void BrokenTagsAreReportedWithTheLine()
        {
            const string Broken = @"-> start
=== start ===
#bgg:platform
#bg:platfrom
#show:iris grumpy
#show:irsi
#music:nope
#cg:photo sideways
#ending:secret_one
Hi <c=nope>x</c> <pause=soon>
#flash:sparkly
-> nowhere
";
            var scan = InkScan.Read("Broken.ink", Broken);
            var p = StoryValidator.Validate(scan, Ids());
            string all = p.ToString();
            Assert.That(all, Does.Contain("Broken.ink:3: unknown tag #bgg (did you mean #bg?)"));
            Assert.That(all, Does.Contain("Broken.ink:4: #bg:platfrom: no background with id \"platfrom\" (did you mean \"platform\"?)"));
            Assert.That(all, Does.Contain("iris has no expression \"grumpy\""));
            Assert.That(all, Does.Contain("no character with id \"irsi\""));
            Assert.That(all, Does.Contain("no sound with id \"nope\""));
            Assert.That(all, Does.Contain("didn't understand \"sideways\""));
            Assert.That(all, Does.Contain("no ending (add it to the story config's Endings) with id \"secret_one\""));
            Assert.That(all, Does.Contain("no palette colour called \"nope\""));
            Assert.That(all, Does.Contain("\"soon\" isn't a number"));
            Assert.That(all, Does.Contain("\"sparkly\" isn't a colour"));
            Assert.That(all, Does.Contain("\"-> nowhere\" goes nowhere"));
            Assert.That(p.ErrorCount, Is.GreaterThanOrEqualTo(11));
        }

        [Test]
        public void ExtraTagsAreAllowed()
        {
            var ids = Ids();
            ids.extraTags.Add("achievement");
            var p = new StoryProblems();
            StoryValidator.CheckTag("achievement:first_steps", false, ids, p);
            Assert.That(p.Ok, Is.True);
            StoryValidator.CheckTag("acheivement:first_steps", false, ids, p);
            Assert.That(p.Ok, Is.False);
        }

        [Test]
        public void HotspotDataProblems()
        {
            var s = new HotspotSceneInfo { id = "office", background = "nowhere" };
            s.hotspots.Add(new HotspotInfo { id = "a", label = "", shape = HotspotShape.Rect(0.1f, 0.1f, 0f, 0.1f), knot = "missing", showIf = "item:spoon && (" });
            s.hotspots.Add(new HotspotInfo { id = "a", label = "A", shape = HotspotShape.Polygon(0.1f, 0.1f, 0.2f, 0.1f, 0.2f, 0.3f), knot = "desk" });
            s.walkthrough.Add("ghost");
            var scan = InkScan.Read("Good.ink", Good);
            var p = StoryValidator.Validate(scan, Ids(), new[] { s });
            string all = p.ToString();
            Assert.That(all, Does.Contain("background \"nowhere\" doesn't exist"));
            Assert.That(all, Does.Contain("two hotspots have the id \"a\""));
            Assert.That(all, Does.Contain("no size"));
            Assert.That(all, Does.Contain("knot \"missing\" isn't in the ink"));
            Assert.That(all, Does.Contain("Show If"));
            Assert.That(all, Does.Contain("walkthrough step \"ghost\""));
            Assert.That(all, Does.Contain("no label"));
        }

        [Test]
        public void ItemsListUnreachableKnotsAndSpeakerTypos()
        {
            var ids = Ids();
            var scan = InkScan.Read("S.ink", "LIST Items = brass_key, spoon\n-> a\n=== a ===\nIrsi: hello.\n-> END\n=== lonely ===\n-> END\n");
            var p = StoryValidator.Validate(scan, ids);
            string all = p.ToString();
            Assert.That(all, Does.Contain("LIST Items has \"spoon\""));
            Assert.That(all, Does.Contain("item \"ticket\" is in the story config but not in ink's LIST Items"));
            Assert.That(all, Does.Contain("knot \"lonely\" is never reached"));
            Assert.That(all, Does.Contain("\"Irsi:\" isn't a character (did you mean"));
            Assert.That(all, Does.Contain("no #ending tag anywhere"));
        }

        [Test]
        public void DataKnotsMustExistAndCountAsReachable()
        {
            var ids = Ids();
            ids.dataKnots.Add("desk");
            ids.dataKnots.Add("nope");
            var scan = InkScan.Read("Good.ink", Good);
            var p = StoryValidator.Validate(scan, ids);
            Assert.That(p.ToString(), Does.Contain("starts knot \"nope\""));
            Assert.That(p.ToString(), Does.Not.Contain("knot \"desk\" is never reached"));
        }
    }
}
