using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    public struct Spawn
    {
        /// <summary>An <see cref="EnemyDef.id"/>.</summary>
        public string enemy;
        public Vector3 position;
        /// <summary>The bots that wake together (Task 11b): a group wakes when one of its members sees the player or is hurt.</summary>
        public int group;
        /// <summary>For a bot on a rail (the gondola): the rail runs from <see cref="position"/> to here. Ignored otherwise.</summary>
        public Vector3 railEnd;

        public Spawn(string enemy, Vector3 position) : this(enemy, position, 0) { }

        public Spawn(string enemy, Vector3 position, int group)
        {
            this.enemy = enemy;
            this.position = position;
            this.group = group;
            railEnd = position;
        }

        public Spawn(string enemy, Vector3 position, int group, Vector3 railEnd)
        {
            this.enemy = enemy;
            this.position = position;
            this.group = group;
            this.railEnd = railEnd;
        }
    }

    /// <summary>One fight: where the player stands when it starts (bearings are measured from here) and who spawns where.</summary>
    public sealed class EncounterDef
    {
        public string id = "";
        public Vector3 anchor;
        public Spawn[] spawns = new Spawn[0];
        /// <summary>A deliberate keep-out zone (2.6): the aim models may mix here, on purpose.</summary>
        public bool keepOut;
    }

    /// <summary>The encounter rules of docs/SKYLINE-DESIGN.md 2.6. (The clean line is checked with the generator in M2.)</summary>
    public static class EncounterRules
    {
        public const float MinBearingSpread = 90f;
        public const int MaxLayers = 2;

        /// <summary>Degrees, 0 along +Z, 90 along +X.</summary>
        public static float Bearing(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z) * (180f / MathF.PI);

        /// <summary>The widest angle (0..180) between two spawns' bearings from the anchor. Spawns within 1 m of the anchor
        /// (on the flat) have no bearing.</summary>
        public static float BearingSpread(EncounterDef e)
        {
            var bearings = new List<float>();
            if (e?.spawns == null) return 0f;
            foreach (var s in e.spawns)
            {
                float dx = s.position.X - e.anchor.X, dz = s.position.Z - e.anchor.Z;
                if (dx * dx + dz * dz >= 1f) bearings.Add(Bearing(e.anchor, s.position));
            }
            float best = 0f;
            for (int i = 0; i < bearings.Count; i++)
                for (int j = i + 1; j < bearings.Count; j++)
                    best = MathF.Max(best, MathF.Abs(AimAssist.DeltaAngle(bearings[i], bearings[j])));
            return best;
        }

        /// <summary>The layers no spawn threatens (2.6: "reading the open layer is the skill").</summary>
        public static List<ThreatLayer> OpenLayers(EncounterDef e, Func<string, EnemyDef> lookup)
        {
            var open = new List<ThreatLayer> { ThreatLayer.Floor, ThreatLayer.Walls, ThreatLayer.Air };
            if (e?.spawns == null || lookup == null) return open;
            foreach (var s in e.spawns)
            {
                var d = lookup(s.enemy);
                if (d != null) open.Remove(d.layer);
            }
            return open;
        }

        /// <summary>Every rule the encounter breaks, in words; empty when it's fair: no mixed aim models (unless a keep-out
        /// zone), at most two layers, spawns on bearings at least 90 degrees apart, every enemy known.</summary>
        public static List<string> Check(EncounterDef e, Func<string, EnemyDef> lookup)
        {
            var problems = new List<string>();
            if (e == null)
            {
                problems.Add("no encounter");
                return problems;
            }
            string name = string.IsNullOrEmpty(e.id) ? "encounter" : e.id;
            if (e.spawns == null || e.spawns.Length == 0)
            {
                problems.Add(name + ": no spawns");
                return problems;
            }
            var aims = new List<AimModel>();
            var layers = new HashSet<ThreatLayer>();
            foreach (var s in e.spawns)
            {
                var d = lookup?.Invoke(s.enemy);
                if (d == null)
                {
                    problems.Add($"{name}: unknown enemy '{s.enemy}'");
                    continue;
                }
                aims.Add(d.aim);
                layers.Add(d.layer);
            }
            if (!e.keepOut && !AimRules.CanShareEncounter(aims.ToArray()))
                problems.Add(name + ": mixes shots that lead you with shots at where you are now (2.6)");
            if (layers.Count > MaxLayers)
                problems.Add($"{name}: threatens {layers.Count} layers; at most {MaxLayers}, so one is left open (2.6)");
            float spread = BearingSpread(e);
            if (spread < MinBearingSpread)
                problems.Add($"{name}: spawns span only {spread:0} degrees of bearing; they need at least {MinBearingSpread:0} (2.6)");
            return problems;
        }
    }

    /// <summary>A solid axis-aligned piece of cover (world metres): a shot that crosses it is stopped, and a shooter can't see
    /// through it. The arena lists them as data (<see cref="GalleriaArena.HallCover"/>) and the Unity builder builds exactly those.</summary>
    public struct CoverBox
    {
        public Vector3 min, max;
    }

    /// <summary>A difficulty preset's numbers (7.4). M1 builds Normal; Easy and Hard come with M4.</summary>
    public sealed class DifficultyDef
    {
        public string id = "normal";
        /// <summary>How much of your velocity a leading shot accounts for (0..1; rises with difficulty).</summary>
        public float leadFactor = 0.75f;
        /// <summary>Scales the plating wrecks refund.</summary>
        public float scrapScale = 1f;
    }

    public static class Difficulties
    {
        /// <summary>Normal: tuned so the straight-line bot takes about twice the jinking bot's hits (Task 11).</summary>
        public static DifficultyDef Normal() => new DifficultyDef { id = "normal", leadFactor = 0.75f, scrapScale = 1f };
    }

    /// <summary>
    /// M1's arena (11.2) as data, read by the Unity builder and the headless checks alike. Two connected spaces: the
    /// warm-up hall (south: the practice bot and the boost ledge, 6.8) and the Galleria's main hall (a two-storey atrium,
    /// mezzanine at 5 m) with two encounters. Metres; +Z is north, the way the player faces at the start.
    /// </summary>
    public static class GalleriaArena
    {
        public const float HallMinX = -25f, HallMaxX = 25f, HallMinZ = -20f, HallMaxZ = 20f, MezzanineY = 5f;
        public const float WarmupMinX = -15f, WarmupMaxX = 15f, WarmupMinZ = -48f, WarmupMaxZ = -20f;
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -45f);
        /// <summary>The practice bot that visibly grows spots (Dot's Writ lesson); it never attacks.</summary>
        public static readonly Vector3 PracticeBot = new Vector3(-9f, 1.4f, -38f);
        /// <summary>The top of a kiosk in the warm-up hall: reachable only with a Breacher boost at the double jump's apex
        /// (jump + double jump + mantle falls short; checked in the sim in Task 12). A scrap cache waits on top.</summary>
        public static readonly Vector3 BoostLedgeTop = new Vector3(0f, 4.6f, -34f);
        public const float BoostLedgeSize = 3f;
        /// <summary>The kiosk keeps at least this far (m) from every wall, so no wall-run reaches its top.</summary>
        public const float LedgeWallClearance = 12f;

        /// <summary>The main hall's entrance from the warm-up hall (which joins its south wall at z = -20), 2 m inside. Encounter bearings are measured from here (plan amendment 5).</summary>
        public static readonly Vector3 HallEntry = new Vector3(0f, 0f, -18f);

        /// <summary>The entry lane around <see cref="HallEntry"/> (x -6..6, z -20..-14, metres): the food court starts when the
        /// player steps in here (the first fight's zone, <see cref="ZoneOf"/>).</summary>
        public const float EntryZoneMinX = -6f, EntryZoneMaxX = 6f, EntryZoneMinZ = -20f, EntryZoneMaxZ = -14f;

        /// <summary>The route through the hall (judge round 1: three waves in one room, one booth winning them all): the fights
        /// progress north, each from its own anchor, where its balance is measured, each with its own pocket of cover: the food
        /// court by the entrance (<see cref="HallEntry"/>; the information booth's U), the facade in the middle (two staggered
        /// stall backs, each shielding one side's drones; the gondolas ride the side walls), the skylight by the mezzanine
        /// (its underside hides from the spotlights on its rail); then the far goal, the service lift under the mezzanine
        /// (<see cref="LiftLobby"/>), its sign in sight from the middle of the hall on.</summary>
        public static readonly Vector3 FacadeAnchor = new Vector3(0f, 0f, -3f), SkylightAnchor = new Vector3(0f, 0f, 8f);

        /// <summary>Each fight starts when the player's feet enter its zone: a 12 x 6 m lane round its anchor (the food court's is
        /// the entry lane), shown by a lit floor marker while the fight is armed and waiting.</summary>
        public const float ZoneHalfX = 6f, ZoneHalfZ = 3f;

        public static void ZoneOf(EncounterDef e, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            if (e != null && e.anchor == HallEntry)
            {
                minX = EntryZoneMinX; maxX = EntryZoneMaxX; minZ = EntryZoneMinZ; maxZ = EntryZoneMaxZ;
                return;
            }
            Vector3 a = e != null ? e.anchor : HallEntry;
            minX = a.X - ZoneHalfX; maxX = a.X + ZoneHalfX; minZ = a.Z - ZoneHalfZ; maxZ = a.Z + ZoneHalfZ;
        }

        const string D = SkylineEnemies.DroneId, F = SkylineEnemies.FloorCareId, S = SkylineEnemies.SpotlightId, G = SkylineEnemies.GondolaId;

        /// <summary>Drones hover at this height (m): below the tall pieces' tops (4.5-5 m), so a tall piece shadows the floor
        /// behind it even from a player jumping (the eye at +2 m), and above the kiosks (2.5 m).</summary>
        public const float DroneY = 3.8f;

        /// <summary>The food court (Normal: the creator's "at least 12"): twelve drones (air, leading) in three groups of four
        /// (west, east, and north by the mezzanine), and two floor-care units (floor) that wake with the west and east groups.
        /// Walls open. No piece seals a group away: which groups see the player depends on where the player stands.</summary>
        public static EncounterDef FoodCourt() => new EncounterDef
        {
            id = "food-court",
            anchor = HallEntry,
            spawns = new[]
            {
                new Spawn(D, new Vector3(-21f, DroneY, -8f), 0), new Spawn(D, new Vector3(-19f, DroneY, -4f), 0),
                new Spawn(D, new Vector3(-21f, DroneY, 0f), 0), new Spawn(D, new Vector3(-18f, DroneY, 2f), 0),
                new Spawn(D, new Vector3(21f, DroneY, -8f), 1), new Spawn(D, new Vector3(19f, DroneY, -4f), 1),
                new Spawn(D, new Vector3(21f, DroneY, 0f), 1), new Spawn(D, new Vector3(18f, DroneY, 2f), 1),
                new Spawn(D, new Vector3(-9f, 4.2f, 9f), 2), new Spawn(D, new Vector3(-6f, 4.2f, 9.5f), 2),
                new Spawn(D, new Vector3(6f, 4.2f, 9.5f), 2), new Spawn(D, new Vector3(9f, 4.2f, 9f), 2),
                new Spawn(F, new Vector3(-21f, 0f, 12f), 0), new Spawn(F, new Vector3(21f, 0f, 12f), 1),
            },
        };

        /// <summary>The skylight, the last fight and the climax, by the mezzanine: three spotlights on its railing (air, where
        /// you are now; the third, off-centre, since judge round 2's escalation), the outer two each with two floor-care units
        /// (floor) that wake with it and come from the hall's sides (three each swarmed the line under the deck: 22/30 clean). Walls open. Break their sight (the tall pieces, the mezzanine's underside) and
        /// change direction against the beams while jumping the acid.</summary>
        public static EncounterDef Skylight() => new EncounterDef
        {
            id = "skylight",
            anchor = SkylightAnchor,
            spawns = new[]
            {
                new Spawn(S, new Vector3(-12.5f, 6.5f, 12.3f), 0), new Spawn(S, new Vector3(12.5f, 6.5f, 12.3f), 1), new Spawn(S, new Vector3(20f, 6.5f, 12.3f), 2),
                new Spawn(F, new Vector3(-21f, 0f, -8f), 0), new Spawn(F, new Vector3(-17f, 0f, 7f), 0),
                new Spawn(F, new Vector3(21f, 0f, -8f), 1), new Spawn(F, new Vector3(17f, 0f, 7f), 1),
            },
        };

        /// <summary>The gondolas' rails: along the side walls at wall-run height, so their mines guard the wall-run route.</summary>
        public const float RailY = 2.4f;
        /// <summary>Where the rails run along the side walls (z), south to north.</summary>
        public const float RailStartZ = -14f, RailEndZ = 10f;

        /// <summary>The facade, the middle fight: two window-washer gondolas on rails along the side walls at wall-run height
        /// (walls), and eight drones in two groups of four along the west and east walls (air; 19-21 m out, so a peek from the
        /// middle's stall backs isn't point-blank) that also cover the centre floor. The floor is the
        /// safe layer; the side walls are the fast line between cover (a wall-run past the rails), and the mines guard it.</summary>
        public static EncounterDef Facade() => new EncounterDef
        {
            id = "facade",
            anchor = FacadeAnchor,
            spawns = new[]
            {
                new Spawn(G, new Vector3(-24.2f, RailY, RailStartZ), 0, new Vector3(-24.2f, RailY, RailEndZ)),
                new Spawn(G, new Vector3(24.2f, RailY, RailStartZ), 1, new Vector3(24.2f, RailY, RailEndZ)),
                new Spawn(D, new Vector3(-21f, DroneY, -9f), 0), new Spawn(D, new Vector3(-20f, DroneY, -2f), 0),
                new Spawn(D, new Vector3(-19f, DroneY, 5f), 0), new Spawn(D, new Vector3(-20f, DroneY, 9f), 0),
                new Spawn(D, new Vector3(21f, DroneY, -9f), 1), new Spawn(D, new Vector3(20f, DroneY, -2f), 1),
                new Spawn(D, new Vector3(19f, DroneY, 5f), 1), new Spawn(D, new Vector3(20f, DroneY, 9f), 1),
            },
        };

        public static EncounterDef[] Encounters() => new[] { FoodCourt(), Facade(), Skylight() };

        /// <summary>The mezzanine's deck (x across the hall, z 12..20, its slab 0.4 m thick under the 5 m floor): it blocks sight
        /// and shots between above it and below it, except past its open edge at z = 12. Not a floor piece, so it isn't in
        /// <see cref="HallCover"/>; the fight's cover is <see cref="FightCover"/>.</summary>
        public static readonly CoverBox MezzanineDeck = new CoverBox
        {
            min = new Vector3(HallMinX, MezzanineY - 0.4f, 12f), max = new Vector3(HallMaxX, MezzanineY, HallMaxZ),
        };

        /// <summary>The main hall's cover (world metres), built exactly as listed by the Unity builder (14 pieces). No piece
        /// seals a part of the hall: tall pieces stand with real gaps between them, so what sees the player depends on where
        /// the player stands, and splitting a fight is the player's job.
        /// <list type="bullet">
        /// <item>three pillars that hold up the north mezzanine (1 x 1 m, floor to the 5 m mezzanine);</item>
        /// <item>four food-stall backs with their menu boards (0.6 m wide, 4.8 m tall): two by the side walls (4 m), and two in
        /// the middle, staggered either side of the facade's anchor (5 m, overlapping 3 m), the facade's pocket;</item>
        /// <item>an information booth on the approach: three advertising screens in a U open to the entry (two 0.4 x 4 m, one
        /// 5.4 x 0.4 m; 4.5 m tall), the food court's pocket;</item>
        /// <item>two kiosks, staggered either side of the middle (4 x 3 m, 2.5 m tall);</item>
        /// <item>two planters beside the booth (2 x 0.8 m, 1 m tall: half cover).</item>
        /// </list>
        /// The entry lane (4 m around <see cref="HallEntry"/>) is clear, and every piece stands at least 3 m from every spawn.</summary>
        public static CoverBox[] HallCover() => new[]
        {
            Pillar(-16f, 12f), Pillar(0f, 12f), Pillar(16f, 12f),
            Block(-13f, -7f, 0.6f, 4f, 4.8f), Block(-2.2f, -3.5f, 0.6f, 5f, 4.8f), Block(13f, -7f, 0.6f, 4f, 4.8f), Block(2.2f, -1.5f, 0.6f, 5f, 4.8f),
            Block(-2.5f, -12f, 0.4f, 4f, 4.5f), Block(2.5f, -12f, 0.4f, 4f, 4.5f), Block(0f, -9.8f, 5.4f, 0.4f, 4.5f),
            Block(-6f, -3f, 4f, 3f, 2.5f), Block(6f, 3f, 4f, 3f, 2.5f),
            Block(-6f, -12f, 2f, 0.8f, 1f), Block(6f, -12f, 2f, 0.8f, 1f),
        };

        /// <summary>Everything that blocks sight and shots in a main-hall fight: <see cref="HallCover"/> and the mezzanine's deck.</summary>
        public static CoverBox[] FightCover()
        {
            var floor = HallCover();
            var rail = MezzanineRail();
            var all = new CoverBox[floor.Length + 2 + rail.Length];
            floor.CopyTo(all, 0);
            all[floor.Length] = MezzanineDeck;
            all[floor.Length + 1] = LiftLobby;
            rail.CopyTo(all, floor.Length + 2);
            return all;
        }

        /// <summary>The glass balustrade's height above the deck (m): its top (6.05 m) is out of a mantle's reach from a boosted
        /// double jump off the floor (feet about 3.3 m + 1.9 m), so the deck isn't an unplanned perch (judge round 1).</summary>
        public const float BalustradeHeight = 0.55f;
        /// <summary>The spotlights' bodies sit this far under their centres (SkylineBotBodies' mount): the plinths' tops.</summary>
        public const float SpotlightBaseBelow = 0.88f;

        /// <summary>The solid pieces on the mezzanine's edge (judge round 1: every visible solid has a collider): the glass
        /// balustrade along the open edge, and a plinth under each spotlight on the skylight's railing (1 x 0.65 m, from the
        /// deck up to the spotlight's base). Shots and sight stop at them in Unity, so they are in <see cref="FightCover"/>.</summary>
        public static CoverBox[] MezzanineRail()
        {
            var d = MezzanineDeck;
            var list = new List<CoverBox>
            {
                new CoverBox { min = new Vector3(d.min.X, d.max.Y, d.min.Z), max = new Vector3(d.max.X, d.max.Y + BalustradeHeight, d.min.Z + 0.05f) },
            };
            foreach (var s in Skylight().spawns)
            {
                if (s.enemy != SkylineEnemies.SpotlightId) continue;
                float top = s.position.Y - SpotlightBaseBelow;
                if (top <= d.max.Y + 0.01f) continue;
                list.Add(new CoverBox
                {
                    min = new Vector3(s.position.X - 0.5f, d.max.Y, d.min.Z + 0.05f), max = new Vector3(s.position.X + 0.5f, top, s.position.Z + 0.4f),
                });
            }
            return list.ToArray();
        }

        /// <summary>The far goal (judge round 1: no route, no far goal): the service lift up to the next floor, a lobby under the
        /// mezzanine against the north wall (x 4..8, z 16..20, floor to the deck's underside). In every fight its security
        /// shutter is down, so it is one solid box (in <see cref="FightCover"/>); once the hall is cleared the shutter rises to
        /// <see cref="ShutterGap"/> m and the lobby is reached by sliding (or crouching) under it: the hall's slide. Its lit
        /// sign shows under the deck's soffit from the middle of the hall on.</summary>
        public static readonly CoverBox LiftLobby = new CoverBox
        {
            min = new Vector3(4f, 0f, 16f), max = new Vector3(8f, MezzanineY - 0.4f, HallMaxZ),
        };

        /// <summary>The raised shutter's clearance (m): under a standing Unit (1.7 m), over a sliding or crouching one (1.0 m).</summary>
        public const float ShutterGap = 1.25f;

        /// <summary>The lobby's walls' thickness (m); the shutter hangs in its open south face.</summary>
        public const float LobbyWall = 0.3f;

        /// <summary>The practice counter in the warm-up (Nan's double jump and mantle, 6.8): its top is above a jump and a mantle
        /// (3.15 m) and below a jump, a double jump and a mantle (4.15 m). It stands by the start, over 12 m (edge to edge) from
        /// the boost kiosk, so no jump from its top reaches the kiosk's: that one still needs the boost.</summary>
        public static readonly Vector3 PracticeLedgeTop = new Vector3(12.5f, 3.5f, -45.5f);
        public const float PracticeLedgeSize = 3f;

        /// <summary>The warm-up's wall-run ledge (judge round 2: nothing asked for a wall-run): a column of lockers 1.5 m off the
        /// west wall, its top (4.6 m) out of a jump, a double jump and a mantle (4.15 m). Run the west wall north, kick off it
        /// toward the lockers and pull up; a scrap cache waits on top. It stands over 12 m (edge to edge) from the boost kiosk.</summary>
        public static readonly CoverBox WallRunLedge = new CoverBox
        {
            min = new Vector3(WarmupMinX + 1.5f, 0f, -24f), max = new Vector3(WarmupMinX + 4f, 4.6f, -22f),
        };

        /// <summary>Where the wall-run starts: by the west wall, south of the lockers, facing north.</summary>
        public static readonly Vector3 WallRunStart = new Vector3(WarmupMinX + 0.6f, 0f, -40f);

        static CoverBox Pillar(float x, float z) => Block(x, z, 1f, 1f, MezzanineY);

        static CoverBox Block(float x, float z, float sizeX, float sizeZ, float height) => new CoverBox
        {
            min = new Vector3(x - sizeX * 0.5f, 0f, z - sizeZ * 0.5f),
            max = new Vector3(x + sizeX * 0.5f, height, z + sizeZ * 0.5f),
        };
    }

    public enum Speaker { Dot, Concierge, Nan }

    public enum BeatTrigger { WarmupStart, PracticeBotSeen, FirstSpotCreated, NearBoostLedge, BoostLedgeReached, FirstPlatingBreak, FirstSeen, NextFightArmed, NearPracticeLedge, LiftOpened,
        // Floor 1 (docs/SKYLINE-PLAN-FLOOR1.md, task F0): the lobby, the repossession and the waves.
        LobbyArrive, LobbyDesk, LobbyLiftNear, LiftRiding, FloorStart, CircleNear, MeterStarted, AssetTowed, WaveAnnounce,
        AlarmRaised, AllTowed, FirstSpecialAmmo,
        /// <summary>The first time the Unit leaves an asset's circle while its tow is under way (once per floor).</summary>
        LeftCircleMidTow }

    /// <summary>One teaching line (6.8): who says it, when, and the words. Drafts are for the creator to rewrite.</summary>
    public sealed class TeachingBeat
    {
        public string id = "", text = "";
        /// <summary>FirstSeen: the bot whose first appearance triggers it.</summary>
        public string enemy;
        public Speaker speaker;
        public BeatTrigger trigger;
        /// <summary>A draft (false only for the lines the spec quotes).</summary>
        public bool draft = true;
        /// <summary>Plays every time its trigger comes (the next-fight notice); every other beat plays once.</summary>
        public bool repeats;
    }

    /// <summary>M1's teaching beats (6.8): Dot teaches the guns and plating, CONCIERGE's safety notices introduce each bot,
    /// and Nan opens the warm-up. One file, so the creator can rewrite every line in one place.</summary>
    public static class TeachingBeats
    {
        /// <summary>The longest line that fits the subtitle box at 1080p without clipping (10: no clipped text).</summary>
        public const int MaxChars = 140;

        public static TeachingBeat[] M1() => new[]
        {
            // Judge round 1: each line fires at its moment (SkylineArenaDirector.MomentPassed) and is dropped once that has
            // passed. Nan opens; Dot's Writ lesson at the practice bot; Nan's double jump and mantle at the practice counter;
            // Dot's boost at the kiosk; the reactions (a spot made, the ledge reached, the plating broken) cut in at once.
            // CONCIERGE points to each next fight's marked area and announces the lift.
            new TeachingBeat { id = "nan-warmup", speaker = Speaker.Nan, trigger = BeatTrigger.WarmupStart,
                text = "Unit's live. Warm up here, then the Galleria's through the far doors." },
            new TeachingBeat { id = "dot-writ-spots", speaker = Speaker.Dot, trigger = BeatTrigger.PracticeBotSeen,
                text = "Practice bot! Tag its lens with the Writ: it cracks open a weak spot. Then hit the spot." },
            new TeachingBeat { id = "dot-writ-spot-made", speaker = Speaker.Dot, trigger = BeatTrigger.FirstSpotCreated,
                text = "That glowing crack? That's the spot. Shoot it!" },
            new TeachingBeat { id = "nan-double-jump-mantle", speaker = Speaker.Nan, trigger = BeatTrigger.NearPracticeLedge,
                text = "That counter's your practice. Tap jump twice for a second hop, then catch its lip and pull yourself up." },
            new TeachingBeat { id = "dot-breacher-boost", speaker = Speaker.Dot, trigger = BeatTrigger.NearBoostLedge,
                text = "Breacher kicks both ways. Jump, jump again, then blast the floor. Scrap's up top!" },
            new TeachingBeat { id = "dot-boost-done", speaker = Speaker.Dot, trigger = BeatTrigger.BoostLedgeReached,
                text = "Ha! Doesn't hurt you, either. Shoot again mid-air to climb higher." },
            new TeachingBeat { id = "dot-plating", speaker = Speaker.Dot, trigger = BeatTrigger.FirstPlatingBreak, draft = false,
                text = "Strip it! Scrap's armour!" },
            new TeachingBeat { id = "concierge-floor-care", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.FloorCareId, draft = false,
                text = "Please note: the floor-care unit operates at ankle height. Guests are advised to jump." },
            new TeachingBeat { id = "concierge-drone", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.DroneId,
                text = "Please note: security drones anticipate where guests are going. Guests are advised to change their minds." },
            new TeachingBeat { id = "concierge-spotlight", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.SpotlightId,
                text = "Please note: the spotlight confirms its target before firing. Unconfirmed guests turn sharply, or stand behind something." },
            new TeachingBeat { id = "concierge-gondola", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.GondolaId,
                text = "Please note: window cleaning is in progress on the walls. Guests are advised to keep their feet on the floor." },
            new TeachingBeat { id = "concierge-next-fight", speaker = Speaker.Concierge, trigger = BeatTrigger.NextFightArmed, repeats = true,
                text = "Security is re-deploying further in. Guests are asked to proceed to the marked area." },
            new TeachingBeat { id = "concierge-lift-open", speaker = Speaker.Concierge, trigger = BeatTrigger.LiftOpened,
                text = "The service lift is now open. Its shutter is stuck at knee height; guests are asked to slide under it." },
        };
    }
}
