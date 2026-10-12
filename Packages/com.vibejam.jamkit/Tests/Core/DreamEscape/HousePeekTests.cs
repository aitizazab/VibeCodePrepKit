using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.DreamEscape;
using JamKit.Stealth;
using NUnit.Framework;

namespace JamKit.Tests.DreamEscape
{
    /// <summary>Peeking in Night 2's house (docs/redesign/house.md, docs/STEALTH.md "Peeking"): at the house's own door
    /// frames, with Theo's own eyes, a head leaned round the jamb is a sliver to him and stepping into the doorway is all of
    /// her, so peeking is clearly the safer way to look.</summary>
    public class HousePeekTests
    {
        public struct PeekSpot
        {
            public HouseDoor door;
            /// <summary>Her feet behind the jamb, her feet stepped out into the doorway, Theo's feet (house space).</summary>
            public Vector3 behind, stepped, theo;
            /// <summary>The way along the wall toward the opening (unit, horizontal): the side she leans to.</summary>
            public Vector3 toward;
        }

        static Vector3 At(HouseDoor d, float along, float across, float y)
            => d.alongX ? new Vector3(along, y, across) : new Vector3(across, y, along);

        static StealthTarget Her(Vector3 feet, Vector3 lean)
        {
            var t = StealthTarget.At(feet);
            t.lean = lean;
            return t;
        }

        static float YawTo(Vector3 from, Vector3 to) => (float)(Math.Atan2(to.X - from.X, to.Z - from.Z) * 180.0 / Math.PI);

        /// <summary>Where a peek round a door frame works: interior doors, each side and jamb, Theo a few metres out on the far
        /// side. Kept when, with his eyes, she's unseen upright behind the jamb and seen well (most of her) stepped into the doorway.</summary>
        public static List<PeekSpot> Spots(DreamHouse h, TheoBrain theo, float lean = 0.4f)
        {
            var list = new List<PeekSpot>();
            foreach (var d in h.doors)
            {
                if (d.exterior || d.y1 - d.y0 < 1.9f || d.b - d.a < 0.75f) continue;
                for (int s = -1; s <= 1; s += 2)
                    for (int j = 0; j < 2; j++)
                    {
                        float jamb = j == 0 ? d.a : d.b, dir = j == 0 ? 1f : -1f;
                        float herAcross = d.c - s * 0.6f;
                        var behind = At(d, jamb - dir * 0.45f, herAcross, d.y0);
                        var stepped = At(d, jamb + dir * 0.45f, herAcross, d.y0);
                        var toward = At(d, dir, 0f, 0f);
                        foreach (float depth in new[] { 3f, 2.5f, 3.5f, 4f })
                            foreach (float off in new[] { 0.7f, 0.45f, 0.95f })
                            {
                                var theoAt = At(d, jamb + dir * off, d.c + s * depth, d.y0);
                                var spot = new PeekSpot { door = d, behind = behind, stepped = stepped, theo = theoAt, toward = toward };
                                if (See(theo, spot.theo, spot.behind, Vector3.Zero) > 0f) continue;
                                if (See(theo, spot.theo, spot.stepped, Vector3.Zero) < 0.5f) continue;
                                if (See(theo, spot.theo, spot.behind, toward * lean) <= 0f) continue;
                                list.Add(spot);
                                goto nextJamb;
                            }
                        nextJamb:;
                    }
            }
            return list;
        }

        /// <summary>Theo at <paramref name="theoFeet"/>, facing her, sees her this well (standing still).</summary>
        public static float See(TheoBrain theo, Vector3 theoFeet, Vector3 herFeet, Vector3 lean)
        {
            theo.Place(theoFeet, YawTo(theoFeet, herFeet));
            return theo.VisibilityOf(Her(herFeet, lean));
        }

        [Test]
        public void PeekingRoundTheHousesDoorFramesIsClearlySaferThanSteppingOut()
        {
            var h = DreamHouse.Build();
            var world = h.StealthWorld();
            var theo = new TheoBrain(h, world, h.Graph(world));
            var spots = Spots(h, theo);
            TestContext.Out.WriteLine($"{spots.Count} door frames to peek round");
            Assert.That(spots.Count, Is.GreaterThanOrEqualTo(3), "the house has door frames a peek works at");
            float worst = 0f;
            foreach (var p in spots)
            {
                float peek = See(theo, p.theo, p.behind, p.toward * 0.4f);
                float stepped = See(theo, p.theo, p.stepped, Vector3.Zero);
                worst = Math.Max(worst, peek / stepped);
                Assert.That(peek, Is.LessThanOrEqualTo(stepped * 0.3f), p.door.name + ": a head round the frame is a sliver");
            }
            var def = theo.Definition;
            var first = spots[0];
            float pv = See(theo, first.theo, first.behind, first.toward * 0.4f), sv = See(theo, first.theo, first.stepped, Vector3.Zero);
            TestContext.Out.WriteLine($"worst peek / stepped out {worst:0.00}; at the {first.door.name}: peeking {pv:0.000} (found in {1f / (pv * def.fillRate):0.0} s of steady sight), stepped out {sv:0.000} ({1f / (sv * def.fillRate):0.0} s)");
        }
    }
}
