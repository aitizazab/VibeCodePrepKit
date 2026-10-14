using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>
    /// A deterministic bot that sprints through a list of waypoints (x, z in the fight's frame) in order and loops back to
    /// the first: the "clean line" a skilled player would take through a fight. It uses only the movement the sim already
    /// has (sprinting and turning), so every change of direction is a corner of the route.
    /// </summary>
    public sealed class ScriptedRouteBot : IPlayerBot
    {
        readonly Vector2[] _route;
        readonly float _arrive;

        /// <summary>The waypoint it is heading for (an index into the route).</summary>
        public int TargetIndex { get; private set; }

        public ScriptedRouteBot(IList<Vector2> waypoints, float arriveRadius = 1.5f)
        {
            if (waypoints == null || waypoints.Count == 0) throw new ArgumentException("a route needs at least one waypoint");
            _route = new Vector2[waypoints.Count];
            waypoints.CopyTo(_route, 0);
            _arrive = arriveRadius;
        }

        public MoveInput Next(MoveSim sim, float time, float h)
        {
            var pos = new Vector2(sim.Position.X, sim.Position.Z);
            // a corner is passed once the bot is within the arrival radius; the next leg starts from wherever it is
            for (int guard = 0; guard < _route.Length && Vector2.Distance(pos, _route[TargetIndex]) <= _arrive; guard++)
                TargetIndex = (TargetIndex + 1) % _route.Length;
            Vector2 d = _route[TargetIndex] - pos;
            float yaw = d.LengthSquared() > 1e-6f ? MathF.Atan2(d.X, d.Y) * (180f / MathF.PI) : sim.Yaw;
            return new MoveInput { move = new Vector2(0f, 1f), yaw = yaw, sprintHeld = true };
        }
    }

    /// <summary>
    /// The dodging measurement (docs/SKYLINE-DESIGN.md 2.6) on an encounter's real layout: its aimed bots become
    /// stationary shooters at their spawns (with the difficulty's lead), and the straight-line and jinking bots run the
    /// fight from the anchor. Area and melee attacks are left out: they're dodged by jumping and spacing, not by changing
    /// direction.
    /// <para>The fight runs in the encounter's own frame: the player starts at the origin, which stands for the anchor
    /// (the hall's entry), and each shooter stands at its spawn minus the anchor. The arena's data stays in world coordinates.</para>
    /// </summary>
    public static class EncounterDodge
    {
        /// <summary>Seconds between the aimed shooters' first shots, so bots of one kind don't fire in lockstep.</summary>
        public const float Stagger = 0.3f;

        public static List<Shooter> Shooters(EncounterDef e, Func<string, EnemyDef> lookup, DifficultyDef difficulty)
        {
            var list = new List<Shooter>();
            if (e?.spawns == null || lookup == null || difficulty == null) return list;
            foreach (var s in e.spawns)
            {
                var d = lookup(s.enemy);
                if (d == null || (d.aim != AimModel.LeadsYou && d.aim != AimModel.FiresAtWhereYouAre)) continue;
                bool beam = d.projectileSpeed <= 0f;
                list.Add(new Shooter
                {
                    position = s.position - e.anchor,
                    aim = d.aim,
                    projectileSpeed = d.projectileSpeed,
                    fireInterval = d.fireInterval,
                    leadFactor = d.aim == AimModel.LeadsYou ? difficulty.leadFactor : 0f,
                    projectileRadius = d.projectileRadius,
                    aimSpreadDeg = d.aimSpreadDeg,
                    aimLagSeconds = d.aimLagSeconds,
                    firstShotAt = d.alertSeconds + (beam ? 0f : d.telegraphSeconds) + Stagger * list.Count,
                    beamTelegraph = d.telegraphSeconds,
                    beamLockBefore = d.beamLockBefore,
                    beamFollowRate = d.beamFollowRate,
                    beamHitRadius = d.beamHitRadius,
                    range = d.range,
                    damage = d.attackDamage,
                });
            }
            return list;
        }

        /// <summary>The hall's cover in the fight's frame (each box moved by minus the encounter's anchor), for
        /// <see cref="DodgeReport"/> and <see cref="CombatSim"/>. The encounter runs below pass no cover.</summary>
        public static List<CoverBox> Cover(EncounterDef e)
        {
            var list = new List<CoverBox>();
            if (e == null) return list;
            foreach (var box in GalleriaArena.HallCover()) list.Add(CoverGeometry.Translated(box, -e.anchor));
            return list;
        }

        public static DodgeResult Run(EncounterDef e, Func<IMoveWorld> makeWorld, Func<string, EnemyDef> lookup, DifficultyDef difficulty,
            float seconds = 60f, ulong seed = 7, int seeds = 5) =>
            DodgeReport.Run(makeWorld, () => Shooters(e, lookup, difficulty), seconds, seed, seeds);

        public static DodgeResult Run(EncounterDef e, Func<IMoveWorld> makeWorld, DifficultyDef difficulty) =>
            Run(e, makeWorld, SkylineEnemies.Find, difficulty);

        /// <summary>The scripted clean line through an encounter: waypoints (x, z) in the fight's frame (the origin is the
        /// anchor), looped. Null for an encounter without one. Each is a route a skilled player could take, found by search
        /// on Normal (Task 11); the headless test holds it to zero hits.
        /// <list type="bullet">
        /// <item>food-court: a straight sprint up and down a lane at x = 9 to 10 between z = 9.6 and z = 22.8, across the line of fire
        /// of both drones (they stand at x = -22 and x = 22), far enough from them that a leading shot has a long way to fall behind.</item>
        /// <item>skylight: a short strafe back and forth under the mezzanine, about a second a leg; the reversals come between
        /// the beam's lock and its shot, so the beam is aimed at where the player was.</item>
        /// </list></summary>
        public static Vector2[] CleanLine(EncounterDef e)
        {
            if (e == null) return null;
            switch (e.id)
            {
                case "food-court": return new[] { new Vector2(9.2f, 9.6f), new Vector2(10.4f, 22.8f) };
                case "skylight": return new[] { new Vector2(-1.5f, 10f), new Vector2(6f, 4.5f) };
                default: return null;
            }
        }

        /// <summary>The clean line's hits in each of the harness's seeded fights (its aim error varies with the seed).</summary>
        public static int[] CleanLineHits(EncounterDef e, Func<IMoveWorld> makeWorld, Func<string, EnemyDef> lookup, DifficultyDef difficulty,
            float seconds = 60f, ulong seed = 7, int seeds = 5)
        {
            var route = CleanLine(e);
            if (route == null) throw new ArgumentException("no clean line for encounter " + e?.id);
            return DodgeReport.HitsPerSeed(makeWorld, () => Shooters(e, lookup, difficulty), s => new ScriptedRouteBot(route), seconds, seed, seeds);
        }
    }
}
