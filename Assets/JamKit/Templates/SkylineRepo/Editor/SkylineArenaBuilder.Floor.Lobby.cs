using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// Task B2 (docs/SKYLINE-PLAN-FLOOR1.md): the Halcyon Grand's staff hallway and lobby as a grey-box built ONLY from
    /// <see cref="HotelLobby.Solids"/> (one object and one collider per solid, named by the solid's id, through B1's
    /// <c>FloorSolidObject</c>, so <c>SkylineLint.FloorDataMatch</c> checks it with the Food Court) plus collider-less stand-ins
    /// marked <see cref="SkylineNoCollider"/>. The lobby's north wall is the Food Court's south wall (B1 builds it from
    /// <c>RepossessArena</c>; it is not in the lobby's solids).
    /// <para>The body of B1's <c>static partial void BuildLobbyShell(Transform arena)</c>. Everything is under one group,
    /// <c>Hotel Lobby</c>: <c>Staff Hallway</c>, <c>Lobby Shell</c>, <c>Mezzanine</c>, <c>Furniture</c>, <c>Lift Cab</c>
    /// (the solids), and <c>Lobby Fixtures</c> (lights, the chandelier, the CONCIERGE screen, the practice rig's stand-ins,
    /// scrap caches, anchors for L2 and M2, lobby stills).</para>
    /// <para>Anchors the runtime tasks find by name: <c>CONCIERGE Screen</c> (L2: the front-desk screen, emissive,
    /// facing south), <c>Practice Bot Anchor</c> (L2: where the bellhop drone hovers, <see cref="HotelLobby.PracticeBot"/>),
    /// <c>Lift Door Anchor</c> (L2: the cab's far doors are the solid <c>cab-doors-n</c>, found with
    /// <c>GameObject.Find</c> by that name; L2 adds <c>SkylineLiftDoor</c> there, which does not exist yet), <c>Lift Cab Trigger
    /// (SteppedIntoLift)</c> (a marker at the cab's centre; its box is <see cref="HotelLobby.LiftCab"/>), <c>Ceiling Speaker 1..6</c>
    /// (M2: the muzak plays from them), <c>Chandelier Light</c>, <c>Wall-run Start</c>.</para>
    /// <para>The two <see cref="SkylineScrapCache"/>s (boost stack, lockers) need the player: the scene builder calls
    /// <see cref="LobbyWirePlayer"/> once the player exists (B1's <c>BuildFloor1</c> or X0's <c>WireDirector</c>).</para>
    /// </summary>
    public static partial class SkylineArenaBuilder
    {
        /// <summary>The name of the group everything of the lobby is under, below the arena.</summary>
        public const string LobbyGroupName = "Hotel Lobby";

        static partial void BuildLobbyShell(Transform arena)
        {
            var root = Group(arena, LobbyGroupName);
            var hallway = Group(root, "Staff Hallway");
            var shell = Group(root, "Lobby Shell");
            var mezz = Group(root, "Mezzanine");
            var furniture = Group(root, "Furniture");
            var cab = Group(root, "Lift Cab");
            var fx = Group(root, "Lobby Fixtures");

            // the hallway is a service space: the colour-free zone's greys (docs/SKYLINE-STYLE.md), only its lights carry hue
            var mHall = Lit("SK_Hallway_Grey", new Color(0.54f, 0.56f, 0.6f));
            var mHallFloor = Lit("SK_Hallway_Floor_Grey", new Color(0.4f, 0.42f, 0.45f));

            // ---------------------------------------------------------- the solids, once each, from the data
            var built = new List<string>();
            foreach (var s in HotelLobby.Solids())
            {
                Transform parent;
                Material m = FloorMaterialFor(s);
                if (s.id.StartsWith("hallway-", System.StringComparison.Ordinal))
                {
                    parent = hallway;
                    m = s.role == "floor" ? mHallFloor : mHall;
                }
                else if (s.id.StartsWith("mezzanine-", System.StringComparison.Ordinal)) parent = mezz;
                else if (s.id.StartsWith("cab-", System.StringComparison.Ordinal)) parent = cab;
                else if (s.id.StartsWith("lobby-", System.StringComparison.Ordinal)) parent = shell;
                else parent = furniture;
                FloorSolidObject(parent, s, m);
                built.Add(s.id);
            }

            // ---------------------------------------------------------- collider-less stand-ins (SkylineNoCollider via Box's flag)
            var mSign = Emissive("SK_Emissive_Lobby_Sign", new Color(1.6f, 1.1f, 0.5f));
            var mCache = Emissive("SK_Emissive_ScrapCache", new Color(1.8f, 1.2f, 0.3f));
            var mScreen = Emissive("SK_Emissive_Concierge", new Color(0.5f, 1.3f, 1.5f));
            var mChandelier = Emissive("SK_Emissive_Chandelier", new Color(2.2f, 1.6f, 0.9f));
            var mStrip = Emissive("SK_Emissive_Hallway_Strip", new Color(1.1f, 1.2f, 1.3f));
            var mRig = Lit("SK_Lobby_Rig", new Color(0.86f, 0.66f, 0.22f));

            // the practice counter's and the lockers' front edges, as in M1 (a lit lip, so the ledge reads)
            var pc = U(HotelLobby.PracticeCounterTop);
            float ph = GalleriaArena.PracticeLedgeSize * 0.5f;
            LobbyDecor(fx, "Practice Counter Edge", V(pc.x - ph, pc.y, pc.z + ph - 0.12f), V(pc.x + ph, pc.y + 0.04f, pc.z + ph), mSign);
            var wr = HotelLobby.WallRunLockers;
            LobbyDecor(fx, "Wall-run Lockers Edge", V(wr.min.X, wr.max.Y, wr.min.Z), V(wr.max.X, wr.max.Y + 0.04f, wr.min.Z + 0.12f), mSign);

            // the scrap caches of M1's two lessons: the boost stack's (Breacher boost) and the lockers' (wall-run)
            var stackTop = U(HotelLobby.BoostStackTop);
            var cacheGo = new GameObject("Scrap Cache (boost stack)");
            cacheGo.transform.SetParent(fx, false);
            cacheGo.transform.position = stackTop;
            var cache = cacheGo.AddComponent<SkylineScrapCache>();
            cache.visual = LobbyDecor(cacheGo.transform, "Crate", stackTop + V(-0.35f, 0f, -0.25f), stackTop + V(0.35f, 0.4f, 0.25f), mCache);
            var wrTop = new Vector3((wr.min.X + wr.max.X) * 0.5f, wr.max.Y, (wr.min.Z + wr.max.Z) * 0.5f);
            var wrCacheGo = new GameObject("Scrap Cache (lockers)");
            wrCacheGo.transform.SetParent(fx, false);
            wrCacheGo.transform.position = wrTop;
            var wrCache = wrCacheGo.AddComponent<SkylineScrapCache>();
            wrCache.ledgeId = "wall-run-ledge";
            wrCache.scrap = 30f;
            wrCache.visual = LobbyDecor(wrCacheGo.transform, "Crate", wrTop + V(-0.35f, 0f, -0.25f), wrTop + V(0.35f, 0.4f, 0.25f), mCache);
            LobbyMarker(fx, "Wall-run Start", U(HotelLobby.WallRunStart), 270f);

            // the bellhop rig: hangers from the rail up to the ceiling, the floor mark under the practice drone, its anchor
            var pb = U(HotelLobby.PracticeBot);
            float rx0 = -16f, rx1 = -8f;      // the same x range as the data's bellhop-rail solid
            LobbyDecor(fx, "Bellhop Hanger West", V(rx0, HotelLobby.GantryTopY, pb.z - 0.1f), V(rx0 + 0.2f, HotelLobby.Height, pb.z + 0.1f), mRig);
            LobbyDecor(fx, "Bellhop Hanger East", V(rx1 - 0.2f, HotelLobby.GantryTopY, pb.z - 0.1f), V(rx1, HotelLobby.Height, pb.z + 0.1f), mRig);
            LobbyDecor(fx, "Practice Floor Mark", V(pb.x - 1.5f, 0f, pb.z - 1.5f), V(pb.x + 1.5f, 0.01f, pb.z + 1.5f), mRig);
            LobbyMarker(fx, "Practice Bot Anchor", pb, 0f);

            // CONCIERGE's front-desk screen: hangs over the desk on two rods, faces the lobby (south)
            var desk = U(HotelLobby.DeskCenter);
            var screen = LobbyDecor(fx, "CONCIERGE Screen", V(desk.x - 2.5f, 2.4f, desk.z - 0.05f), V(desk.x + 2.5f, 3.8f, desk.z + 0.05f), mScreen);
            LobbyDecor(screen.transform, "Rod West", V(desk.x - 2f, 3.8f, desk.z - 0.03f), V(desk.x - 1.94f, HotelLobby.Height, desk.z + 0.03f), mRig);
            LobbyDecor(screen.transform, "Rod East", V(desk.x + 1.94f, 3.8f, desk.z - 0.03f), V(desk.x + 2f, HotelLobby.Height, desk.z + 0.03f), mRig);
            LobbyLight(fx, "CONCIERGE Glow", V(desk.x, 3.1f, desk.z - 1.2f), new Color(0.55f, 0.85f, 1f), 1.6f, 7f);

            // the chandelier: the light that takes the room over (style rule 3): a body, a rod, one strong warm point light
            var ch = U(HotelLobby.ChandelierPosition);
            var chBody = LobbyDecor(fx, "Chandelier", V(ch.x - 0.9f, ch.y - 0.5f, ch.z - 0.9f), V(ch.x + 0.9f, ch.y + 0.1f, ch.z + 0.9f), mChandelier);
            LobbyDecor(chBody.transform, "Chandelier Rod", V(ch.x - 0.04f, ch.y + 0.1f, ch.z - 0.04f), V(ch.x + 0.04f, HotelLobby.Height, ch.z + 0.04f), mRig);
            LobbyLight(fx, "Chandelier Light", ch + V(0f, -0.4f, 0f), new Color(1f, 0.82f, 0.55f), 5f, 34f);

            // the staff hallway: dim, two ceiling strips and two faint cool lights; the lobby's glow at its end
            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? -74f : -66f;
                LobbyDecor(hallway, "Hallway Strip " + (i + 1), V(-0.4f, HotelLobby.HallwayHeight - 0.04f, z - 0.6f), V(0.4f, HotelLobby.HallwayHeight, z + 0.6f), mStrip);
                LobbyLight(fx, "Hallway Light " + (i + 1), V(0f, HotelLobby.HallwayHeight - 0.4f, z), new Color(0.78f, 0.85f, 1f), 0.9f, 8f);
            }
            LobbyLight(fx, "Lobby Glow (hallway end)", V(0f, 2.6f, HotelLobby.MinZ + 1.5f), new Color(1f, 0.85f, 0.6f), 2.2f, 10f);

            // the lift cab: a lit sign over its open side, a light inside, the door anchor and the stepped-into-lift marker
            var lc = HotelLobby.LiftCab;
            LobbyDecor(cab, "Lift Sign", V(lc.min.X, lc.max.Y - 0.5f, lc.min.Z - 0.05f), V(lc.max.X, lc.max.Y, lc.min.Z), mSign);
            LobbyLight(fx, "Lift Cab Light", V((lc.min.X + lc.max.X) * 0.5f, lc.max.Y - 0.3f, (lc.min.Z + lc.max.Z) * 0.5f),
                new Color(1f, 0.9f, 0.7f), 2f, 8f);
            LobbyMarker(cab, "Lift Door Anchor", V((lc.min.X + lc.max.X) * 0.5f, 0f, lc.max.Z), 0f);
            LobbyMarker(cab, "Lift Cab Trigger (SteppedIntoLift)", V((lc.min.X + lc.max.X) * 0.5f, 0f, (lc.min.Z + lc.max.Z) * 0.5f), 0f);

            // ceiling speakers for the tinny muzak (M2/L2 put the audio on them): two in the hallway, four in the lobby
            var speakerAt = new[]
            {
                V(0f, HotelLobby.HallwayHeight - 0.15f, -74f), V(0f, HotelLobby.HallwayHeight - 0.15f, -66f),
                V(-14f, HotelLobby.Height - 0.2f, -38f), V(14f, HotelLobby.Height - 0.2f, -38f),
                V(-14f, HotelLobby.Height - 0.2f, -54f), V(14f, HotelLobby.Height - 0.2f, -54f),
            };
            for (int i = 0; i < speakerAt.Length; i++)
            {
                var t = LobbyMarker(fx, "Ceiling Speaker " + (i + 1), speakerAt[i], 0f);
                LobbyDecor(t, "Speaker Box", speakerAt[i] + V(-0.2f, -0.12f, -0.2f), speakerAt[i] + V(0.2f, 0.12f, 0.2f), mRig);
            }

            // lobby stills for the agents' own checks (the floor builder's set has the hallway and the Food Court)
            var stills = Group(fx, "Lobby Stills");
            Still(stills, "lobby-01-threshold", V(0f, 1.7f, -62.5f), V(0f, 3f, -44f));
            Still(stills, "lobby-02-desk", V(-2f, 1.7f, -43f), V(desk.x, 2.8f, desk.z));
            Still(stills, "lobby-03-practice-bot", V(pb.x + 4f, 2.2f, pb.z - 5f), pb);
            Still(stills, "lobby-04-boost-stack", V(stackTop.x + 5f, 3.6f, stackTop.z - 7f), V(stackTop.x, stackTop.y - 0.5f, stackTop.z));
            Still(stills, "lobby-05-lockers", V(-8f, 2.2f, -40f), wrTop);
            Still(stills, "lobby-06-mezzanine", V(-2f, 1.8f, -50f), V(18f, 5.4f, -43f));
            Still(stills, "lobby-07-lift-cab", V(0f, 1.7f, -44f), V(0f, 1.6f, -29f));
            Still(stills, "lobby-08-lobby-wide", V(20.5f, 6f, -58f), V(-8f, 2.5f, -38f));
            Still(stills, "lobby-09-practice-counter", V(pc.x - 7f, 2.4f, pc.z + 6f), V(pc.x, pc.y - 0.3f, pc.z));

            Debug.Log("SKYLINE_FLOOR lobby built: " + built.Count + " solids");
        }

        /// <summary>Gives the lobby's two scrap caches the player (they do nothing without one). Call it once the player exists.</summary>
        static void LobbyWirePlayer(Transform arena, SkylinePlayer player)
        {
            if (arena == null || player == null) return;
            foreach (var c in arena.GetComponentsInChildren<SkylineScrapCache>(true)) c.player = player;
        }

        /// <summary>A collider-less, shadowless visual box (marked <see cref="SkylineNoCollider"/> for the lint).</summary>
        static GameObject LobbyDecor(Transform parent, string name, Vector3 min, Vector3 max, Material m) =>
            NoShadow(Box(parent, name, min, max, m, false, null, true));

        static Transform LobbyMarker(Transform parent, string name, Vector3 at, float yawDeg) => FloorMarker(parent, name, at, yawDeg);

        /// <summary>A baked-less, shadowless point light (a hard-edged local pool is the ink look's job, not the light's).</summary>
        static Light LobbyLight(Transform parent, string name, Vector3 at, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
