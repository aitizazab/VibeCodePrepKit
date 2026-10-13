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

        /// <summary>The Writ: a precise sidearm. Every weak-point hit leaves a weak spot that takes more than a headshot (2.2).
        /// 250 RPM, 8 rounds; a magazine-and-cell reload with a bolt from empty.</summary>
        public static GunDef Writ()
        {
            var tilt = V(-0.04f, -0.05f, 0f);
            var tiltEuler = V(10f, -10f, -25f);
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
                shakePerShot = 0.06f,
                kick = new CameraKickDef { pitchDeg = 2.2f, yawDeg = 0.5f, recoverRate = 9f },
                aimAssist = new AimAssistDef { slowdownConeDeg = 5f, slowdownScale = 0.5f, magnetConeDeg = 3f, magnetDegPerSec = 8f, range = 50f },
                clips = new[]
                {
                    C(HandlingClipKind.Equip, 0.55f,
                        new[] { E(0f, "unfold", "writ/equip_unfold"), E(0.7f, "lock", "writ/equip_lock") },
                        new[] { P(0f, V(0.05f, -0.3f, -0.15f), V(40f, 0f, 25f)), P(0.7f, V(0f, 0.01f, 0f), V(-3f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.Fire, 0.22f,
                        new[] { E(0f, "shot", "writ/shot"), E(0.45f, "mech", "writ/shot_mech") },
                        new[] { P(0f, O, O), P(0.15f, V(0f, 0.015f, -0.06f), V(-7f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.DryFire, 0.25f,
                        new[] { E(0f, "click", "writ/dry_click") },
                        new[] { P(0f, O, O), P(0.3f, V(0f, 0f, -0.01f), V(-1f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.ReloadPartial, 1.3f,
                        new[] { E(0.18f, "mag_out", "writ/mag_out"), E(0.52f, "cell_in", "writ/cell_in"), E(0.68f, "seat", "writ/seat") },
                        new[]
                        {
                            P(0f, O, O), P(0.15f, tilt, tiltEuler), P(0.18f, tilt, tiltEuler), P(0.3f, tilt, tiltEuler, V(0f, -0.25f, 0f)),
                            P(0.45f, tilt, tiltEuler, V(0f, -0.12f, 0f)), P(0.52f, tilt, tiltEuler, V(0f, -0.02f, 0f)),
                            P(0.68f, V(-0.04f, -0.03f, 0f), V(4f, -6f, -15f)), P(1f, O, O),
                        }),
                    C(HandlingClipKind.ReloadEmpty, 1.75f,
                        new[] { E(0.13f, "mag_out", "writ/mag_out"), E(0.42f, "cell_in", "writ/cell_in"), E(0.55f, "seat", "writ/seat"), E(0.8f, "bolt", "writ/bolt") },
                        new[]
                        {
                            P(0f, O, O), P(0.11f, tilt, tiltEuler), P(0.13f, tilt, tiltEuler), P(0.25f, tilt, tiltEuler, V(0f, -0.25f, 0f)),
                            P(0.36f, tilt, tiltEuler, V(0f, -0.12f, 0f)), P(0.42f, tilt, tiltEuler, V(0f, -0.02f, 0f)),
                            P(0.55f, V(-0.04f, -0.03f, 0f), V(4f, -6f, -15f)), P(0.8f, V(0.02f, -0.02f, 0f), V(0f, 8f, 12f)), P(1f, O, O),
                        }),
                    C(HandlingClipKind.Inspect, 2.4f,
                        new[] { E(0.1f, "turn", "writ/inspect_turn"), E(0.55f, "tap", "writ/inspect_tap"), E(0.85f, "settle", "writ/inspect_settle") },
                        new[]
                        {
                            P(0f, O, O), P(0.2f, V(-0.08f, 0.04f, 0.05f), V(-10f, -55f, -20f)), P(0.55f, V(-0.08f, 0.05f, 0.05f), V(-12f, -60f, -25f)),
                            P(0.6f, V(-0.08f, 0.045f, 0.05f), V(-11f, -58f, -24f)), P(0.85f, O, O), P(1f, O, O),
                        }),
                },
            };
        }

        /// <summary>The Breacher: a door-breach shotgun whose identity is knockback, including on yourself (2.2; the self-push
        /// is Breacher.cs). 80 RPM, a 4-shell cell; a cell swap, with a pump from empty.</summary>
        public static GunDef Breacher()
        {
            var tilt = V(0.03f, -0.06f, 0f);
            var tiltEuler = V(15f, 12f, 30f);
            return new GunDef
            {
                id = BreacherId,
                displayName = "Breacher",
                weapon = new WeaponDef { magazineSize = 4, fireMode = FireMode.Semi, fireInterval = 0.75f },
                damage = 9f,
                pellets = 9,
                spreadDeg = 5f,
                range = 30f,
                falloffStart = 6f,
                falloffEnd = 18f,
                falloffMinScale = 0.25f,
                weakPointMultiplier = 1.25f,
                enemyKnockback = 9f,
                hitStopSeconds = 0.05f,
                killHitStopSeconds = 0.1f,
                // Task 19: "the camera kicks hard" (2.2). 8 degrees up, recovered to 0.5% by the next shot (0.75 s at rate 7).
                shakePerShot = 0.3f,
                kick = new CameraKickDef { pitchDeg = 8f, yawDeg = 2f, recoverRate = 7f },
                aimAssist = new AimAssistDef { slowdownConeDeg = 8f, slowdownScale = 0.6f, magnetConeDeg = 5f, magnetDegPerSec = 12f, range = 20f },
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
                            P(0f, O, O), P(0.12f, tilt, tiltEuler), P(0.15f, tilt, tiltEuler), P(0.28f, tilt, tiltEuler, V(0.05f, -0.3f, 0f)),
                            P(0.44f, tilt, tiltEuler, V(0.02f, -0.12f, 0f)), P(0.5f, tilt, tiltEuler, V(0f, -0.02f, 0f)),
                            P(0.66f, V(0.02f, -0.04f, 0f), V(6f, 8f, 18f)), P(1f, O, O),
                        }),
                    C(HandlingClipKind.ReloadEmpty, 2.1f,
                        new[] { E(0.12f, "cell_out", "breacher/cell_out"), E(0.42f, "cell_in", "breacher/cell_in"), E(0.56f, "seat", "breacher/seat"), E(0.82f, "pump", "breacher/pump") },
                        new[]
                        {
                            P(0f, O, O), P(0.1f, tilt, tiltEuler), P(0.12f, tilt, tiltEuler), P(0.24f, tilt, tiltEuler, V(0.05f, -0.3f, 0f)),
                            P(0.37f, tilt, tiltEuler, V(0.02f, -0.12f, 0f)), P(0.42f, tilt, tiltEuler, V(0f, -0.02f, 0f)),
                            P(0.56f, V(0.02f, -0.04f, 0f), V(6f, 8f, 18f)), P(0.78f, V(0f, -0.02f, -0.08f), V(-6f, 0f, 0f)),
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
