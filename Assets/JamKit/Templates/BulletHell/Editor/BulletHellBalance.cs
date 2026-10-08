using System;
using System.Collections.Generic;
using System.IO;
using JamKit.Roguelite;
using UnityEditor;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;

namespace JamKit.BulletHell.EditorTools
{
    /// <summary>
    /// "JamKit/Bullet Hell/Balance Report": a scripted dodge bot (core <see cref="BalanceSim"/>) plays the
    /// template's real rooms (every Battle and Elite room of every floor, plus each boss's first phase)
    /// with each upgrade alone and with none, and writes out/balance-report.md and out/balance-report.csv:
    /// per upgrade its DPS against the baseline, its own share of the damage, hits taken and time to clear.
    /// Anything under the usefulness threshold is FLAGGED (docs/DESIGN-ROGUELITE.md, section 2).
    /// Batch: <c>-executeMethod JamKit.BulletHell.EditorTools.BulletHellBalance.RunFromCommandLine</c>
    /// (optional <c>-balanceSeeds N</c>, default 8: fewer seeds let noise flip borderline upgrades). Takes seconds.
    /// </summary>
    public static class BulletHellBalance
    {
        public const int DefaultSeeds = 8;

        [MenuItem("JamKit/Bullet Hell/Balance Report")]
        static void Menu()
        {
            var config = AssetDatabase.LoadAssetAtPath<BulletHellConfig>(BulletHellContent.ConfigPath);
            string path = Write(config, DefaultSeeds, out var rows);
            if (path == null) return;
            int flagged = 0;
            foreach (var r in rows) if (r.verdict == BalanceVerdict.Flagged) flagged++;
            Debug.Log("Bullet hell balance report: " + path + " (" + flagged + " flagged)");
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>Batch entry point. Exit code 0 when the report was written (flags are findings, not failures).</summary>
        public static void RunFromCommandLine()
        {
            int code = 1;
            try
            {
                int seeds = DefaultSeeds;
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-balanceSeeds" && int.TryParse(args[i + 1], out int n) && n > 0) seeds = n;
                var config = AssetDatabase.LoadAssetAtPath<BulletHellConfig>(BulletHellContent.ConfigPath);
                string path = Write(config, seeds, out var rows);
                if (path != null)
                {
                    var flagged = new List<string>();
                    foreach (var r in rows) if (r.verdict == BalanceVerdict.Flagged) flagged.Add(r.id);
                    Debug.Log("JAMKIT_BALANCE written " + path + "; flagged: " + (flagged.Count > 0 ? string.Join(", ", flagged) : "none"));
                    code = 0;
                }
            }
            catch (Exception e) { Debug.LogError("JAMKIT_BALANCE_FAIL " + e); }
            EditorApplication.Exit(code);
        }

        /// <summary>Runs the report and writes both files under the project's out/ folder. Returns the .md path.</summary>
        public static string Write(BulletHellConfig config, int seeds, out List<BalanceRow> rows)
        {
            rows = null;
            if (config == null) { Debug.LogError("Balance report: no BulletHellConfig at " + BulletHellContent.ConfigPath); return null; }
            var rooms = Rooms(config, true);
            var sim = Sim(config);
            var player = Player(config.player);
            var pool = new List<UpgradeDef>();
            var byId = new Dictionary<string, UpgradeDef>();
            foreach (var u in config.upgrades)
            {
                if (u == null || u.def == null || string.IsNullOrEmpty(u.def.id)) continue;
                pool.Add(u.def);
                byId[u.def.id] = u.def;
            }
            var started = DateTime.Now;
            rows = BalanceReport.Run(sim, rooms, player, pool, seeds, BalanceReport.DefaultThreshold, id => byId.TryGetValue(id, out var d) ? d : null);
            var baseline = BalanceReport.Baseline(sim, rooms, player, seeds);
            string md = BalanceReport.ToMarkdown(rows, baseline, rooms.Count, seeds, BalanceReport.DefaultThreshold)
                + "\nRooms: " + RoomNames(rooms) + ".\nCompanion " + (sim.companion != null ? "on" : "off") + ". Generated "
                + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " in " + (DateTime.Now - started).TotalSeconds.ToString("0") + " s by JamKit/Bullet Hell/Balance Report.\n";
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "out"));
            Directory.CreateDirectory(dir);
            string mdPath = Path.Combine(dir, "balance-report.md");
            File.WriteAllText(mdPath, md);
            File.WriteAllText(Path.Combine(dir, "balance-report.csv"), BalanceReport.ToCsv(rows));
            return mdPath;
        }

        static string RoomNames(List<SimRoom> rooms)
        {
            var names = new List<string>();
            foreach (var r in rooms) names.Add(r.name);
            return string.Join(", ", names);
        }

        public static BalanceSim Sim(BulletHellConfig config)
        {
            var radii = new List<float>();
            if (config.bulletStyles != null) foreach (var s in config.bulletStyles) radii.Add(s != null ? s.hitRadius : 0.08f);
            var half = config.playfieldHalfSize;
            return new BalanceSim
            {
                dt = 1f / 30f,
                bounds = PlayfieldBounds.Centered(half.x, half.y),
                styleRadii = radii,
                boosts = config.boosts ?? new PowerBoostRules(),
                shots = config.shots ?? new ShotRules(),
                companion = config.companionEnabled && config.companion != null ? config.companion.rules : null,
            };
        }

        public static SimPlayer Player(PlayerDef p)
        {
            var s = new SimPlayer();
            if (p == null) return s;
            s.moveSpeed = p.moveSpeed;
            s.hitboxRadius = p.hitboxRadius;
            s.grazeRadius = p.grazeRadius;
            s.shotSpeed = p.shotSpeed;
            s.shotDamage = p.shotDamage;
            s.fireInterval = p.weapon != null ? p.weapon.fireInterval : 0.085f;
            s.shotLifetime = p.shotLifetime;
            s.homingTurnRate = p.homingTurnRate;
            s.invulnerableAfterHit = p.invulnerableAfterHit;
            s.lives = p.lives;
            s.sprint = p.sprint ?? new SprintDef();
            s.kitBombs = p.bombsPerLife;
            s.bayBombs = p.bayBombs;
            s.maxBombs = Mathf.Max(p.maxBombs, p.bombsPerLife);
            s.bombRechargeSeconds = p.bombRechargeSeconds;
            s.bombRadius = p.bombRadius;
            s.bombDamage = p.bombDamage;
            s.bombInvulnerable = p.bombInvulnerable;
            return s;
        }

        /// <summary>Every Battle and Elite room of every floor as a simulated room (waves one after another, at
        /// their max duration, or 14 s when they wait to be cleared), plus each boss's first phase.</summary>
        public static List<SimRoom> Rooms(BulletHellConfig config, bool bosses, int floorLimit = int.MaxValue)
        {
            var rooms = new List<SimRoom>();
            var half = config.playfieldHalfSize;
            var bounds = PlayfieldBounds.Centered(half.x, half.y);
            if (config.floors == null) return rooms;
            for (int f = 0; f < config.floors.Count && f < floorLimit; f++)
            {
                var floor = config.floors[f];
                if (floor == null) continue;
                foreach (var list in new[] { floor.combatRooms, floor.eliteRooms })
                {
                    if (list == null) continue;
                    foreach (var room in list)
                    {
                        if (room == null || room.waves == null) continue;
                        var sim = new SimRoom { name = room.name };
                        float offset = 0f;
                        foreach (var wave in room.waves)
                        {
                            if (wave == null || wave.spawns == null) continue;
                            float last = 0f;
                            foreach (var s in wave.spawns)
                            {
                                if (s == null || s.enemy == null) continue;
                                int count = Mathf.Max(1, s.count);
                                for (int i = 0; i < count; i++)
                                {
                                    Vector2 anchor = s.position + s.spacing * (i - (count - 1) * 0.5f);
                                    float t = s.time + i * Mathf.Max(0f, s.interval);
                                    last = Mathf.Max(last, t);
                                    sim.enemies.Add(Enemy(s.enemy, offset + t, s.from, anchor, floor.hpScale, bounds));
                                    if (s.mirror) sim.enemies.Add(Enemy(s.enemy, offset + t, Mirror(s.from), new Vector2(-anchor.x, anchor.y), floor.hpScale, bounds));
                                }
                            }
                            offset += wave.maxDuration > 0f ? wave.maxDuration : Mathf.Max(14f, last + 6f);
                        }
                        rooms.Add(sim);
                    }
                }
                if (bosses && floor.boss != null && floor.boss.phases != null && floor.boss.phases.Count > 0 && floor.boss.phases[0] != null)
                {
                    var b = floor.boss;
                    var p = b.phases[0];
                    float share = b.phases.Count > 1 && b.phases[1] != null ? 1f - b.phases[1].startsAtHealth : 1f;
                    var sim = new SimRoom { name = b.displayName + " (phase 1)" };
                    sim.enemies.Add(new SimEnemy
                    {
                        time = 0f,
                        from = new SVector2(b.homePosition.x, b.homePosition.y + 5f),
                        anchor = new SVector2(b.homePosition.x, b.homePosition.y),
                        hp = Mathf.Max(1f, b.hp * floor.hpScale * share),
                        radius = b.radius,
                        attack = p.attack,
                        attackDelay = 0.3f,
                        move = new EnemyMoveDef { kind = EnemyMoveKind.Strafe, enterSeconds = 2f, amplitude = Mathf.Max(0.1f, p.moveWidth), frequency = Mathf.Max(0.05f, p.moveSpeed), actSeconds = 0f, speed = 3f },
                    });
                    rooms.Add(sim);
                }
            }
            return rooms;
        }

        static SimEnemy Enemy(EnemyDef def, float time, EnterSide side, Vector2 anchor, float hpScale, PlayfieldBounds bounds)
        {
            var from = SpawnPoint(side, anchor, def.movement, bounds);
            return new SimEnemy
            {
                time = time,
                from = new SVector2(from.x, from.y),
                anchor = new SVector2(anchor.x, anchor.y),
                move = def.movement ?? new EnemyMoveDef(),
                hp = Mathf.Max(0.1f, def.hp * Mathf.Max(0.01f, hpScale)),
                radius = def.radius,
                attack = def.attack,
                attackDelay = def.attackDelay,
                contact = def.hurtsOnContact,
            };
        }

        /// <summary>The same rule as BulletHellGame's spawn points.</summary>
        static Vector2 SpawnPoint(EnterSide side, Vector2 anchor, EnemyMoveDef move, PlayfieldBounds b)
        {
            float top = b.maxY + 1f, left = b.minX - 1f, right = b.maxX + 1f;
            var kind = move != null ? move.kind : EnemyMoveKind.Hover;
            if (kind == EnemyMoveKind.Drift) return new Vector2(anchor.x, top);
            if (kind == EnemyMoveKind.Sweep && (side == EnterSide.Top || side == EnterSide.TopLeft || side == EnterSide.TopRight))
                side = side == EnterSide.TopLeft ? EnterSide.Left : side == EnterSide.TopRight ? EnterSide.Right : (anchor.x <= 0f ? EnterSide.Left : EnterSide.Right);
            switch (side)
            {
                case EnterSide.Left: return new Vector2(left, anchor.y);
                case EnterSide.Right: return new Vector2(right, anchor.y);
                case EnterSide.TopLeft: return new Vector2(left, top);
                case EnterSide.TopRight: return new Vector2(right, top);
                default: return new Vector2(anchor.x, top);
            }
        }

        static EnterSide Mirror(EnterSide s)
        {
            switch (s)
            {
                case EnterSide.Left: return EnterSide.Right;
                case EnterSide.Right: return EnterSide.Left;
                case EnterSide.TopLeft: return EnterSide.TopRight;
                case EnterSide.TopRight: return EnterSide.TopLeft;
                default: return s;
            }
        }
    }
}
