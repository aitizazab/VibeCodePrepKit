using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using JamKit.Combat;

namespace JamKit.Skyline
{
    /// <summary>The Bailiff kit's two M1 guns (docs/SKYLINE-DESIGN.md 2.2) as data. Every number is a balance number AND an
    /// animation and sound timing (2.3). The weapon identity agents (docs/SKYLINE-PLAN-M1.md Tasks 18-19) own and retune
    /// them; the tests pin the identities, not the exact numbers. Each call returns a new instance (upgrades change copies).</summary>
    public static class SkylineGuns
    {
        public const string WritId = "writ", BreacherId = "breacher";

        static readonly Vector3 O = Vector3.Zero;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static PoseFrame P(float at, Vector3 gun, Vector3 euler) => new PoseFrame(at, gun, euler, Vector3.Zero);
        static PoseFrame P(float at, Vector3 gun, Vector3 euler, Vector3 mag) => new PoseFrame(at, gun, euler, mag);
        static HandlingEvent E(float at, string id, string sound) => new HandlingEvent(at, id, sound);
        static HandlingClip C(HandlingClipKind k, float s, HandlingEvent[] ev, PoseFrame[] pose) => new HandlingClip(k, s, ev, pose);

        public static GunDef[] All() => new[] { Writ(), Breacher() };

        public static GunDef Find(string id) => id == WritId ? Writ() : id == BreacherId ? Breacher() : null;

        /// <summary>The Writ's inspect sight check (Task 18; docs/SKYLINE-GUN-WRIT.md): the pose offset (camera space) that puts
        /// the rear sight's window 13 cm straight ahead of the eye, on the sight line, and the turn that undoes the Writ's rest
        /// turn. From SkylineViewmodel's Writ rest, (0.2, -0.13, 0.5) turned -9 degrees, and the window at (0, 0.088, -0.079)
        /// in the gun's frame: change them together.</summary>
        public static readonly Vector3 WritSightCheck = new Vector3(-0.2f, 0.042f, -0.291f);
        public static readonly Vector3 WritSightCheckEuler = new Vector3(0f, 9f, 0f);

        /// <summary>The Writ: a precise sidearm. Every weak-point hit leaves a weak spot that takes more than a headshot (2.2).
        /// 250 RPM, 8 rounds; the reload is the quill leaving the ink well, writing on a sheet of paper on the table while the
        /// ink refills behind it (docs/SKYLINE-GUN-WRIT.md).</summary>
        public static GunDef Writ()
        {
            // The reload (the creator, 2026-10-14): no magazine, no drum, nothing for the hands to do, so the hands stay on the
            // grip (SkylineRobotArms leaves the left hand alone for the Writ) and the whole gun tilts slightly UP (the muzzle 16
            // degrees up, a touch nearer the eye) for the reload's duration: the table with its paper turns toward the player
            // while the quill writes on it. It settles back at the end. Every frame's magOffset is zero.
            var up = V(0f, 0.012f, -0.015f);
            var upEuler = V(-16f, 0f, 0f);
            var sway = V(0.004f, 0.012f, -0.015f);
            var swayEuler = V(-15f, 1.5f, 0f);
            return new GunDef
            {
                id = WritId,
                displayName = "Writ",
                weapon = new WeaponDef { magazineSize = 8, fireMode = FireMode.Semi, fireInterval = 0.24f },
                damage = 22f,
                pellets = 1,
                spreadDeg = 0f,
                range = 80f,
                falloffStart = 25f,
                falloffEnd = 60f,
                falloffMinScale = 0.6f,
                weakPointMultiplier = 2f,
                enemyKnockback = 0.5f,
                hitStopSeconds = 0.03f,
                killHitStopSeconds = 0.08f,
                // Task 18 (docs/SKYLINE-GUN-WRIT.md): a heavy precise shot, the gavel coming down. 2.8 degrees up, recovered to
                // 7% (0.2 degrees) by the next shot 0.24 s later at rate 11, so the sights are back on target every time.
                shakePerShot = 0.07f,
                kick = new CameraKickDef { pitchDeg = 2.8f, yawDeg = 0.6f, recoverRate = 11f },
                aimAssist = new AimAssistDef { slowdownConeDeg = 5f, slowdownScale = 0.5f, magnetConeDeg = 3f, magnetDegPerSec = 8f, range = 50f,
                    // aimed (ADS) these are the numbers; at the hip the cones halve, the magnet halves and the slowdown halves
                    // (the creator, 2026-10-14: "aim assist is too strong when hip-firing")
                    hipConeScale = 0.5f, hipMagnetScale = 0.5f, hipSlowdownStrength = 0.5f },
                // Aim down sights: through the round brass sight window, so the view narrows most and slows most (no spread to tighten).
                aimFovScale = 0.75f, aimLookScale = 0.65f, aimMoveScale = 0.8f, aimSpreadScale = 1f,
                clips = new[]
                {
                    C(HandlingClipKind.Equip, 0.55f,
                        new[] { E(0f, "unfold", "writ/equip_unfold"), E(0.7f, "lock", "writ/equip_lock") },
                        // rises turned, showing its side while the court assembles (SkylineWritFx), then swings onto the target
                        new[] { P(0f, V(0.06f, -0.28f, -0.12f), V(40f, 10f, 30f)), P(0.45f, V(-0.03f, 0.03f, -0.04f), V(-5f, -35f, -8f)),
                                P(0.7f, V(0f, 0.01f, 0f), V(-3f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.Fire, 0.22f,
                        new[] { E(0f, "shot", "writ/shot") },
                        new[] { P(0f, O, O), P(0.12f, V(0f, 0.02f, -0.07f), V(-9f, 0f, 1.5f)), P(0.45f, V(0f, 0.004f, -0.012f), V(-2f, 0f, 0.3f)), P(1f, O, O) }),
                    C(HandlingClipKind.DryFire, 0.25f,
                        new[] { E(0f, "click", "writ/dry_click") },
                        new[] { P(0f, O, O), P(0.3f, V(0f, 0f, -0.01f), V(-1f, 0f, 0f)), P(1f, O, O) }),
                    // The reload: the quill leaves the ink well ("lift"), writes three strokes on the sheet of paper on the table
                    // while the ink refills behind it, and returns to the well ("return"); the ink is full on the "seat" event (a
                    // brass click) and the quill settles into its lean. The model's behaviour is WritQuill.Reload. From empty it is
                    // the same, longer, and ends with a flourish of the quill ("flourish", animation only). No sound but the seat
                    // is made yet (a quill's scratch is wanted, see the identity sheet).
                    C(HandlingClipKind.ReloadPartial, 1.3f,
                        new[] { E(0.08f, "lift", null), E(0.70f, "return", null), E(0.76f, "seat", "writ/seat") },
                        new[]
                        {
                            P(0f, O, O), P(0.07f, up, upEuler), P(0.35f, sway, swayEuler), P(0.6f, up, upEuler), P(0.84f, up, upEuler), P(1f, O, O),
                        }),
                    C(HandlingClipKind.ReloadEmpty, 1.75f,
                        new[] { E(0.06f, "lift", null), E(0.74f, "return", null), E(0.80f, "seat", "writ/seat"), E(0.88f, "flourish", null) },
                        new[]
                        {
                            P(0f, O, O), P(0.05f, up, upEuler), P(0.3f, sway, swayEuler), P(0.55f, up, upEuler), P(0.86f, up, upEuler), P(1f, O, O),
                        }),
                    // The inspect turns the courtroom to show its side (the colonnade, the lamps, the brass plate), taps the
                    // plate, then raises the gun to the eye for a sight check: the gun centred so the eye looks along the sight
                    // line through the rear sight's round window, the amber bead alone in it on the crosshair.
                    C(HandlingClipKind.Inspect, 2.4f,
                        new[] { E(0.1f, "turn", "writ/inspect_turn"), E(0.55f, "tap", "writ/inspect_tap"), E(0.85f, "settle", "writ/inspect_settle") },
                        new[]
                        {
                            P(0f, O, O), P(0.18f, V(-0.08f, 0.04f, 0.05f), V(-10f, -55f, -20f)), P(0.5f, V(-0.08f, 0.05f, 0.05f), V(-12f, -60f, -25f)),
                            P(0.55f, V(-0.08f, 0.045f, 0.05f), V(-11f, -58f, -24f)), P(0.68f, WritSightCheck, WritSightCheckEuler),
                            P(0.8f, WritSightCheck, WritSightCheckEuler), P(0.92f, O, O), P(1f, O, O),
                        }),
                },
            };
        }

        /// <summary>The Breacher: a door-breach shotgun whose identity is knockback, including on yourself (2.2; the self-push
        /// is Breacher.cs). 80 RPM, a 4-shell cell; a cell swap, with a pump from empty.</summary>
        public static GunDef Breacher()
        {
            // The reload lifts the gun a little up and in and rolls it 55 degrees clockwise (as the player sees it: the top
            // goes right), so its underside and the cell face the camera; the cell then drops 14 cm toward the middle of the
            // view and the left hand brings the new one (judge round 1, C4: the cell was never on screen). Posed against
            // SkylineViewmodel's Breacher rest (0.2, -0.13, 0.5) turned -5 degrees, FOV 60, 16:9. On screen (x, y from -1
            // to 1): the muzzle stays at (0.16, -0.14), clear of the crosshair; the cell is held at (0.22, -0.42) and drops
            // to (0.0, -0.69), its lowest edge at -0.82; the pump hand is at (0.19, -0.25); nothing comes nearer the
            // camera than 0.27 m. Change these together with the rest.
            var tilt = V(-0.03f, 0.04f, -0.06f);
            var tiltEuler = V(0f, -4f, -55f);
            var cellOut = V(0f, -0.14f, 0.02f);
            var seat = V(-0.02f, 0.025f, -0.03f);
            var seatEuler = V(-2f, -3f, -25f);
            return new GunDef
            {
                id = BreacherId,
                displayName = "Breacher",
                special = true,
                weapon = new WeaponDef { magazineSize = 4, fireMode = FireMode.Semi, fireInterval = 0.75f },
                damage = 9f,
                pellets = 9,
                spreadDeg = 5f,
                range = 30f,
                falloffStart = 6f,
                falloffEnd = 18f,
                falloffMinScale = 0.25f,
                weakPointMultiplier = 1.25f,
                // Judge round 2 (N3): 9 m/s read small (a drone hit by three pellets at 6.5 m slid 0.4-0.6 m, under the camera
                // kick). 15 m/s, shared by the pellets that land: about 1 m at 6.5 m, about 3 m at point blank (the bots'
                // slide decays at rate 5, so slide = speed / 5), plus SkylineShoveLurch's tilt and spin on the bot.
                enemyKnockback = 15f,
                hitStopSeconds = 0.05f,
                killHitStopSeconds = 0.1f,
                // Task 19: "the camera kicks hard" (2.2). 8 degrees up, recovered to 0.5% by the next shot (0.75 s at rate 7).
                shakePerShot = 0.3f,
                kick = new CameraKickDef { pitchDeg = 8f, yawDeg = 2f, recoverRate = 7f },
                aimAssist = new AimAssistDef { slowdownConeDeg = 8f, slowdownScale = 0.6f, magnetConeDeg = 5f, magnetDegPerSec = 12f, range = 20f,
                    hipConeScale = 0.5f, hipMagnetScale = 0.5f, hipSlowdownStrength = 0.5f },
                // Aim down sights: shouldered, a little narrower and slower, the pellets 20% tighter.
                aimFovScale = 0.88f, aimLookScale = 0.8f, aimMoveScale = 0.8f, aimSpreadScale = 0.8f,
                clips = new[]
                {
                    C(HandlingClipKind.Equip, 0.7f,
                        new[] { E(0.15f, "rack", "breacher/equip_rack"), E(0.75f, "lock", "breacher/equip_lock") },
                        new[] { P(0f, V(0.06f, -0.35f, -0.2f), V(45f, 10f, 30f)), P(0.15f, V(0.03f, -0.15f, -0.1f), V(20f, 5f, 15f)), P(0.75f, V(0f, 0.01f, 0f), V(-4f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.Fire, 0.6f,
                        new[] { E(0f, "blast", "breacher/blast"), E(0.4f, "vent", "breacher/vent_hiss") },
                        new[] { P(0f, O, O), P(0.07f, V(0f, 0.05f, -0.17f), V(-22f, 0f, 4f)), P(0.4f, V(0f, 0.01f, -0.03f), V(-4f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.DryFire, 0.3f,
                        new[] { E(0f, "clunk", "breacher/dry_clunk") },
                        new[] { P(0f, O, O), P(0.25f, V(0f, -0.01f, -0.02f), V(2f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.ReloadPartial, 1.6f,
                        new[] { E(0.15f, "cell_out", "breacher/cell_out"), E(0.5f, "cell_in", "breacher/cell_in"), E(0.66f, "seat", "breacher/seat") },
                        new[]
                        {
                            P(0f, O, O), P(0.12f, tilt, tiltEuler), P(0.15f, tilt, tiltEuler), P(0.28f, tilt, tiltEuler, cellOut),
                            P(0.44f, tilt, tiltEuler, V(0f, -0.07f, 0.01f)), P(0.5f, tilt, tiltEuler, V(0f, -0.01f, 0f)),
                            P(0.66f, seat, seatEuler), P(1f, O, O),
                        }),
                    C(HandlingClipKind.ReloadEmpty, 2.1f,
                        new[] { E(0.12f, "cell_out", "breacher/cell_out"), E(0.42f, "cell_in", "breacher/cell_in"), E(0.56f, "seat", "breacher/seat"), E(0.82f, "pump", "breacher/pump") },
                        new[]
                        {
                            P(0f, O, O), P(0.1f, tilt, tiltEuler), P(0.12f, tilt, tiltEuler), P(0.24f, tilt, tiltEuler, cellOut),
                            P(0.37f, tilt, tiltEuler, V(0f, -0.07f, 0.01f)), P(0.42f, tilt, tiltEuler, V(0f, -0.01f, 0f)),
                            P(0.56f, seat, seatEuler), P(0.78f, V(0f, -0.02f, -0.08f), V(-6f, 0f, 0f)),
                            P(0.82f, V(0f, -0.02f, 0.02f), V(2f, 0f, 0f)), P(1f, O, O),
                        }),
                    C(HandlingClipKind.Inspect, 2.6f,
                        new[] { E(0.1f, "turn", "breacher/inspect_turn"), E(0.5f, "knock", "breacher/inspect_knock"), E(0.85f, "settle", "breacher/inspect_settle") },
                        new[]
                        {
                            P(0f, O, O), P(0.2f, V(-0.1f, 0.05f, 0.06f), V(-8f, -65f, -30f)), P(0.5f, V(-0.1f, 0.06f, 0.06f), V(-10f, -70f, -35f)),
                            P(0.53f, V(-0.1f, 0.055f, 0.06f), V(-9f, -68f, -33f)), P(0.85f, O, O), P(1f, O, O),
                        }),
                },
            };
        }
    }

    /// <summary>Handling clips as sound forge timelines (tools/skyline/soundforge, "timeline"), so the forge renders a gun's
    /// handling set from the same numbers the game plays.</summary>
    public static class ForgeExport
    {
        /// <summary>{"duration": s, "events": [{"at": f, "recipe": "recipes/&lt;name&gt;.json"}, ...]}, read from the gun's folder
        /// tools/skyline/guns/&lt;gun&gt;/. Animation-only events are left out.</summary>
        public static string TimelineJson(HandlingClip clip)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"duration\": ").Append(F(clip.seconds)).Append(",\n  \"events\": [");
            bool first = true;
            foreach (var e in clip.events)
            {
                if (string.IsNullOrEmpty(e.sound)) continue;
                sb.Append(first ? "\n" : ",\n").Append("    {\"at\": ").Append(F(e.at)).Append(", \"recipe\": \"recipes/")
                  .Append(Name(e.sound)).Append(".json\"}");
                first = false;
            }
            sb.Append(first ? "]\n}\n" : "\n  ]\n}\n");
            return sb.ToString();
        }

        /// <summary>The timeline's file name in the gun's folder: "ReloadEmpty.timeline.json".</summary>
        public static string FileName(HandlingClip clip) => clip.kind + ".timeline.json";

        /// <summary>"writ/mag_out" gives "mag_out".</summary>
        public static string Name(string sound)
        {
            int slash = sound.LastIndexOf('/');
            return slash >= 0 ? sound.Substring(slash + 1) : sound;
        }

        /// <summary>Every sound a gun's clips use, once each, in order of first use.</summary>
        public static List<string> Sounds(GunDef gun)
        {
            var list = new List<string>();
            foreach (var c in gun.clips)
                foreach (var e in c.events)
                    if (!string.IsNullOrEmpty(e.sound) && !list.Contains(e.sound)) list.Add(e.sound);
            return list;
        }

        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
