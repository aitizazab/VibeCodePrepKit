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

        public Spawn(string enemy, Vector3 position)
        {
            this.enemy = enemy;
            this.position = position;
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
        public static DifficultyDef Normal() => new DifficultyDef { id = "normal", leadFactor = 0.6f, scrapScale = 1f };
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

        /// <summary>Two drones (air, leading) and two floor-care units (floor): walls open.</summary>
        public static EncounterDef FoodCourt() => new EncounterDef
        {
            id = "food-court",
            anchor = HallEntry,
            spawns = new[]
            {
                new Spawn(SkylineEnemies.DroneId, new Vector3(-22f, 6f, -8f)),
                new Spawn(SkylineEnemies.DroneId, new Vector3(22f, 6f, -8f)),
                new Spawn(SkylineEnemies.FloorCareId, new Vector3(-10f, 0f, 4f)),
                new Spawn(SkylineEnemies.FloorCareId, new Vector3(12f, 0f, 12f)),
            },
        };

        /// <summary>A spotlight on the north mezzanine (air, where you are now) and two floor-care units (floor): walls open.</summary>
        public static EncounterDef Skylight() => new EncounterDef
        {
            id = "skylight",
            anchor = HallEntry,
            spawns = new[]
            {
                new Spawn(SkylineEnemies.SpotlightId, new Vector3(0f, 9f, 18f)),
                new Spawn(SkylineEnemies.FloorCareId, new Vector3(-20f, 0f, -9f)),
                new Spawn(SkylineEnemies.FloorCareId, new Vector3(20f, 0f, -9f)),
            },
        };

        public static EncounterDef[] Encounters() => new[] { FoodCourt(), Skylight() };
    }

    public enum Speaker { Dot, Concierge, Nan }

    public enum BeatTrigger { WarmupStart, PracticeBotSeen, FirstSpotCreated, NearBoostLedge, BoostLedgeReached, FirstPlatingBreak, FirstSeen }

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
    }

    /// <summary>M1's teaching beats (6.8): Dot teaches the guns and plating, CONCIERGE's safety notices introduce each bot,
    /// and Nan opens the warm-up. One file, so the creator can rewrite every line in one place.</summary>
    public static class TeachingBeats
    {
        /// <summary>The longest line that fits the subtitle box at 1080p without clipping (10: no clipped text).</summary>
        public const int MaxChars = 140;

        public static TeachingBeat[] M1() => new[]
        {
            new TeachingBeat { id = "nan-warmup", speaker = Speaker.Nan, trigger = BeatTrigger.WarmupStart,
                text = "Unit's live. Warm-up hall first; the Galleria's through the far doors. Try not to dent anything we have to return." },
            new TeachingBeat { id = "nan-double-jump-mantle", speaker = Speaker.Nan, trigger = BeatTrigger.WarmupStart,
                text = "Tap jump twice for a second hop, and reach a ledge's lip to mantle up. Every kiosk in here is built for practice." },
            new TeachingBeat { id = "dot-writ-spots", speaker = Speaker.Dot, trigger = BeatTrigger.PracticeBotSeen,
                text = "Practice bot, dead ahead! Tag its lens with the Writ and it leaves a weak spot. Hit the spot: hurts more than the lens!" },
            new TeachingBeat { id = "dot-writ-spot-made", speaker = Speaker.Dot, trigger = BeatTrigger.FirstSpotCreated,
                text = "That glowy bit? That's the spot. Shoot it!" },
            new TeachingBeat { id = "dot-breacher-boost", speaker = Speaker.Dot, trigger = BeatTrigger.NearBoostLedge,
                text = "Breacher kicks both ways. Jump, jump again, then shoot your own feet. There's scrap on top of that kiosk." },
            new TeachingBeat { id = "dot-boost-done", speaker = Speaker.Dot, trigger = BeatTrigger.BoostLedgeReached,
                text = "Ha! One boost a jump, mind. Land or touch a wall to get it back." },
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
        };
    }
}
