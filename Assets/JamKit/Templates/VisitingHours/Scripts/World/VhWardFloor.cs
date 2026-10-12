using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Ward 4 West, the floor plan (docs/redesign/day.md has the drawing). Metres; +X east, +Z north; the floor is 48 by 23.
    /// <code>
    ///  z=23 +------+--------+------+----------+------+--------+-----+-----+
    ///       |  11  |   12   |  13  |    14    |  15  |   16   | STB |lin. |   north: the rooms (windows out)
    ///  z=14 +--D---+--doorD-+--D---+--doorD---+--D---+--doorD-+-DD--+--D--+
    ///  WIN  |            the corridor (sun pours in from the west window)   |
    ///  z=11 +--D---+------D-+------------------+---  open  ---+-------------+
    ///       |quiet |  staff |     nurses'      |  lift lobby  | family room |   south
    ///       | room |  room  |     station      |   (spawn)    |   (TV)      |
    ///  z=0  +------+--------+------------------+--LIFT--LIFT--+-------------+
    ///       x=0    6        15                 27             37            48
    /// </code>
    /// </summary>
    public static partial class VhWakingWard
    {
        const float WardH = 2.8f, CeilY = 2.7f;

        /// <summary>The ward on the late shift (3 PM to 11 PM; the sun low in the west). <paramref name="day2"/>: the second
        /// day's people and things (Rafael on his break, the woman from LeBaget, Mr. Brandt awake, the round's new tasks).
        /// Built all at once (tests, the captures); the game builds it a room a frame with <see cref="HospitalSteps"/>.</summary>
        public static void Hospital(VhArea a, VisitingHoursConfig c, bool day2)
        {
            var steps = HospitalSteps(a, c, day2);
            while (steps.MoveNext()) { }
        }

        /// <summary>
        /// The ward, a room at a time (docs/redesign/perf.md, Day item 3: built in one frame it hitched 0.3-1.5 s): the shell,
        /// then each room under its own root (per-room culling, <see cref="VhWardRooms"/>), then the light and the batching.
        /// Each step is one frame's work; VisitingHoursGame runs them under the day's black card.
        /// </summary>
        public static IEnumerator HospitalSteps(VhArea a, VisitingHoursConfig c, bool day2)
        {
            var k = Begin(a, c, day2);
            var rooms = a.root.gameObject.AddComponent<VhWardRooms>();
            rooms.enabled = false;
            a.rooms = rooms;
            k.rooms = rooms;

            // ---------- Floors and the ceiling ----------
            // The open spaces' floors are Look's terrazzo; the rooms keep the ward's vinyl (and the carpets).
            Floor(k, "Floor (corridor)", 0f, 48f, 11f, 14f, Vinyl, "terrazzo");
            Floor(k, "Floor (rooms)", 0f, 48f, 14f, 23f, VinylRoom);
            Floor(k, "Floor (quiet room)", 0f, 6f, 0f, 11f, CarpetWarm, "carpet");
            Floor(k, "Floor (staff room)", 6f, 15f, 0f, 11f, VinylRoom);
            Floor(k, "Floor (station)", 15f, 27f, 0f, 11f, Vinyl, "terrazzo");
            Floor(k, "Floor (lobby)", 27f, 37f, 0f, 11f, Vinyl, "terrazzo");
            Floor(k, "Floor (family room)", 37f, 48f, 0f, 11f, CarpetBlue, "carpet");
            SuspendedCeiling(k, 0f, 48f, 0f, 23f, CeilY);

            // ---------- The outer walls ----------
            // West: the quiet room's window, the corridor's tall end window (the sun's way in).
            WallRun(k, 0f, 0f, 0f, 23f, WardH, WallPaint, Window(3.5f, 4f), Window(11.25f, 2.5f, 0.35f, 2.55f));
            // North: each room's window beside its bed.
            WallRun(k, 0f, 23f, 48f, 23f, WardH, WallRoom, Window(1.6f, 2f), Window(11.4f, 1.4f), Window(14.6f, 2f), Window(24.4f, 2f), Window(28.6f, 2f), Window(37.9f, 1.6f));
            // East: the family room's window; the corridor ends at Ward 4 East's door.
            WallRun(k, 48f, 23f, 48f, 0f, WardH, WallPaint, Window(15f, 4.6f, 0.8f, 2.3f));
            // South: the staff room's window over the sink, the lifts, the family room's window.
            WallRun(k, 48f, 0f, 0f, 0f, WardH, WallPaint, Window(2.6f, 5.6f, 0.85f, 2.3f), Window(36.2f, 3.6f, 1.25f, 2.35f));

            // ---------- The corridor's walls ----------
            // North side (z 14): the doorways of rooms 12, 14 and 16; the rest are shut doors drawn on the wall.
            WallRun(k, 0f, 14f, 48f, 14f, WardH, WallSage, Doorway(7f, 1.4f), Doorway(19.6f, 1.6f), Doorway(33.6f, 1.4f));
            // South side (z 11): the quiet room's and the staff room's doorways, the station and the lobby open (a bulkhead over).
            WallRun(k, 0f, 11f, 15f, 11f, WardH, WallSage, Doorway(3.6f, 1f), Doorway(13.2f, 1f));
            WallRun(k, 15f, 11f, 37f, 11f, WardH, WallSage, new Gap(0.4f, 11.6f, 0f, 2.45f), new Gap(13f, 21f, 0f, 2.45f));
            Wall(k, 37f, 11f, 48f, 11f, WardH, WallSage);
            // Between the rooms (north) and the spaces (south).
            foreach (float x in new[] { 6.5f, 13f, 19f, 27f, 33f, 40f, 44f })
                Wall(k, x, 14.06f, x, 22.94f, WardH, WallRoom);
            Wall(k, 6f, 0.06f, 6f, 10.94f, WardH, WallWarm);
            Wall(k, 15f, 0.06f, 15f, 10.94f, WardH, WallPaint);
            Wall(k, 15.06f, 4f, 26.94f, 4f, WardH, WallPaint);
            Wall(k, 27f, 0.06f, 27f, 10.94f, WardH, WallPaint);
            WallRun(k, 37f, 0.06f, 37f, 10.94f, WardH, WallPaint, Doorway(4.2f, 2.2f));
            yield return null;

            // ---------- The rooms, a frame each (each under its own root: per-room culling) ----------
            Room(k, "Corridor", 0f, 48f, 11f, 14f, true, Corridor);
            yield return null;
            Room(k, "Station", 15f, 27f, 0f, 11f, true, Station);
            yield return null;
            Room(k, "Room 14", 19f, 27f, 14f, 23f, false, Room14);
            yield return null;
            Room(k, "Room 12", 6.5f, 13f, 14f, 23f, false, Room12);
            yield return null;
            Room(k, "Room 16", 33f, 40f, 14f, 23f, false, Room16);
            yield return null;
            Room(k, "Staff room", 6f, 15f, 0f, 11f, false, StaffRoom);
            yield return null;
            Room(k, "Quiet room", 0f, 6f, 0f, 11f, false, QuietRoom);
            yield return null;
            Room(k, "Lift lobby", 27f, 37f, 0f, 11f, true, Lobby);
            yield return null;
            Room(k, "Family room", 37f, 48f, 0f, 11f, false, FamilyRoom);
            yield return null;

            var focus = VhWardLight.Day(k.root, k.lib, ProbeRooms);
            a.bedsideFocus = focus;
            Batch(k);
            // Without Look's rig the ward's own trilight stands (the rig's sky ambient replaces it).
            if (k.lib == null || k.lib.dayRig == null)
                VhWakingStyle.SetAmbient(new Color(0.46f, 0.5f, 0.55f), new Color(0.28f, 0.26f, 0.24f), new Color(0.2f, 0.22f, 0.23f), 0f, 0.3f);
            a.spawn = new Vector3(32f, 0.05f, 4.2f);
            a.spawnYaw = 0f;
            rooms.enabled = true;
            JamKit.Perf.VhPerfTiers.CapNow();
        }

        /// <summary>The reflection probes' spaces (box-projected; baked by JamKit > Visiting Hours > Bake the Ward Probes, in
        /// this order).</summary>
        public static readonly Bounds[] ProbeRooms =
        {
            new Bounds(new Vector3(8f, 1.5f, 12.5f), new Vector3(16f, 3f, 3f)),
            new Bounds(new Vector3(24f, 1.5f, 12.5f), new Vector3(16f, 3f, 3f)),
            new Bounds(new Vector3(40f, 1.5f, 12.5f), new Vector3(16f, 3f, 3f)),
            new Bounds(new Vector3(23f, 1.5f, 18.5f), new Vector3(8f, 3f, 9f)),
            new Bounds(new Vector3(10.5f, 1.5f, 5.5f), new Vector3(9f, 3f, 11f)),
            new Bounds(new Vector3(37f, 1.5f, 6f), new Vector3(20f, 3f, 10f)),
        };

        /// <summary>One room of the floor: what <paramref name="build"/> makes goes under the room's own root (so it can be
        /// culled with the room), over the floor area x0..x1, z0..z1. <paramref name="open"/>: a space she sees the others
        /// from (the corridor, the station, the lobby).</summary>
        static void Room(Kit k, string name, float x0, float x1, float z0, float z1, bool open, System.Action<Kit> build)
        {
            var root = new GameObject("Room: " + name).transform;
            root.SetParent(k.place, false);
            var keep = k.root;
            k.root = root;
            try { build(k); }
            finally { k.root = keep; }
            if (k.rooms != null)
                k.rooms.Add(name, root, new Bounds(new Vector3((x0 + x1) * 0.5f, 1.4f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 2.8f, z1 - z0)), open);
        }

        /// <summary>
        /// Static batching for everything that doesn't move (walls, floors, furniture, signs): the floor is a few thousand small
        /// meshes on a few dozen materials. What moves or changes is left out: the hands, the live monitors, the people.
        /// </summary>
        static void Batch(Kit k)
        {
            var keep = new List<GameObject>();
            var sources = new List<Mesh>();
            foreach (var r in k.root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null || !f.sharedMesh.isReadable) continue;
                if (r.GetComponentInParent<VhHandRig>() != null || r.GetComponentInParent<VhPatientMonitor>() != null) continue;
                if (r.GetComponentInParent<CapsuleCollider>() != null) continue;
                if (r.GetComponentInParent<JamKit.VisitingHours.Characters.VhCharacter>() != null) continue;
                keep.Add(r.gameObject);
                if (!sources.Contains(f.sharedMesh)) sources.Add(f.sharedMesh);
            }
            if (keep.Count <= 1) return;
            StaticBatchingUtility.Combine(keep.ToArray(), k.root.gameObject);
            // perf.md (Day item 3): the combined mesh draws them now; the source meshes are tens of MB of copies. Free the
            // ones no mesh filter points at any more (the kit's Dispose skips what's already gone); anything still
            // referenced stays.
            var stillShown = new HashSet<Mesh>();
            foreach (var f in k.root.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null) stillShown.Add(f.sharedMesh);
            foreach (var m in sources)
                if (!stillShown.Contains(m)) VhUtil.Kill(m);
        }

        // ---------- The corridor ----------

        static void Corridor(Kit k)
        {
            // Light panels down the middle every four metres; the far west ones dimmer (the sun does the work there).
            for (float x = 2f; x < 48f; x += 4f)
                CeilingLight(k, new Vector3(x, CeilY - 0.01f, 12.5f), new Vector2(1.2f, 0.6f), new Color(0.94f, 0.97f, 1f), x < 8f ? 0.5f : 0.95f, 5.5f, false);

            // Shut doors: rooms 11, 13, 15, the linen store; stairwell B's double fire doors.
            Door(k, new Vector3(3.2f, 0f, 13.94f), Vector3.back, 1.1f, DoorBlue, "11");
            Door(k, new Vector3(16f, 0f, 13.94f), Vector3.back, 1.1f, DoorBlue, "13");
            Door(k, new Vector3(30f, 0f, 13.94f), Vector3.back, 1.1f, DoorBlue, "15");
            Door(k, new Vector3(46f, 0f, 13.94f), Vector3.back, 0.9f, new Color(0.62f, 0.62f, 0.6f));
            WallSign(k, "sign_linen", new Vector3(46f, 2.25f, 13.94f), Vector3.back, 0.34f, 0.13f, Plastic);
            // Room numbers beside the open doorways.
            WallSign(k, "sign_12", new Vector3(6.6f, 1.6f, 13.94f), Vector3.back, 0.17f, 0.1f, Plastic);
            WallSign(k, "sign_14", new Vector3(19.2f, 1.6f, 13.94f), Vector3.back, 0.17f, 0.1f, Plastic);
            WallSign(k, "sign_16", new Vector3(33.2f, 1.6f, 13.94f), Vector3.back, 0.17f, 0.1f, Plastic);
            Sanitiser(k, new Vector3(9.0f, 1.25f, 13.94f), Vector3.back);
            Sanitiser(k, new Vector3(21.8f, 1.25f, 13.94f), Vector3.back);
            Sanitiser(k, new Vector3(35.6f, 1.25f, 13.94f), Vector3.back);

            // Stairwell B: the fire doors, their push bar, its sign, the green exit sign's glow.
            var stair = new Vector3(42f, 0f, 13.94f);
            Box(k, "Stairwell door", stair + new Vector3(-0.36f, 1.05f, -0.03f), new Vector3(0.7f, 2.1f, 0.05f), DoorBlue, false, 0f, null, "wood");
            Box(k, "Stairwell door", stair + new Vector3(0.36f, 1.05f, -0.03f), new Vector3(0.7f, 2.1f, 0.05f), DoorBlue, false, 0f, null, "wood");
            Box(k, "Stairwell glass", stair + new Vector3(-0.36f, 1.5f, -0.06f), new Vector3(0.3f, 0.5f, 0.01f), Glass, false, 0f, null, "glass");
            Box(k, "Stairwell glass", stair + new Vector3(0.36f, 1.5f, -0.06f), new Vector3(0.3f, 0.5f, 0.01f), Glass, false, 0f, null, "glass");
            Box(k, "Stairwell frame", stair + new Vector3(0f, 2.13f, -0.03f), new Vector3(1.5f, 0.06f, 0.08f), DoorFrame, false, 0f, null, "plastic");
            Box(k, "Push bar", stair + new Vector3(0f, 1.0f, -0.1f), new Vector3(1.2f, 0.05f, 0.05f), Steel, false, 0f, null, "metal");
            WallSign(k, "sign_stairs", new Vector3(42f, 2.42f, 13.94f), Vector3.back, 0.7f, 0.175f, Plastic);
            Glow(k, "Exit sign", k.meshes.Box(new Vector3(0.4f, 0.16f, 0.05f)), new Color(0.2f, 0.5f, 0.3f), new Color(0.25f, 0.95f, 0.45f) * 1.4f, new Vector3(44.2f, 2.45f, 13.9f), Quaternion.identity);
            Sign(k, null, "sign_exit", new Vector3(44.2f, 2.45f, 13.87f), Quaternion.identity, 0.36f, 0.14f, Plastic);
            AddLight(k, "Exit sign glow", new Vector3(44.2f, 2.3f, 13.5f), new Color(0.4f, 1f, 0.55f), 0.2f, 2f);
            Spot(k, k.day2 ? "stairwell2" : "stairwell", new Vector3(42f, 1.1f, 13.8f), 0.8f);

            // The far end: Ward 4 East's doors, shut, a notice taped to them.
            Door(k, new Vector3(47.94f, 0f, 12.5f), Vector3.left, 1.2f, new Color(0.62f, 0.62f, 0.6f));

            // Handrails along both sides between the doors.
            Handrail(k, new Vector3(0.6f, 0f, 13.94f), new Vector3(2.4f, 0f, 13.94f), Vector3.back * 0.07f);
            Handrail(k, new Vector3(9.8f, 0f, 13.94f), new Vector3(15.2f, 0f, 13.94f), Vector3.back * 0.07f);
            Handrail(k, new Vector3(22.6f, 0f, 13.94f), new Vector3(29.2f, 0f, 13.94f), Vector3.back * 0.07f);
            Handrail(k, new Vector3(36.4f, 0f, 13.94f), new Vector3(41f, 0f, 13.94f), Vector3.back * 0.07f);
            Handrail(k, new Vector3(0.6f, 0f, 11.06f), new Vector3(3.3f, 0f, 11.06f), Vector3.forward * 0.07f);
            Handrail(k, new Vector3(5f, 0f, 11.06f), new Vector3(12.8f, 0f, 11.06f), Vector3.forward * 0.07f);
            Handrail(k, new Vector3(37.4f, 0f, 11.06f), new Vector3(47.4f, 0f, 11.06f), Vector3.forward * 0.07f);

            // The noticeboard by the staff room: cork in a frame, the notices pinned up.
            var board = new Vector3(9.5f, 1.45f, 11.06f);
            Box(k, "Noticeboard frame", board + Vector3.forward * 0.015f, new Vector3(1.6f, 0.95f, 0.03f), Laminate, false, 0f, null, "wood");
            Box(k, "Noticeboard", board + Vector3.forward * 0.035f, new Vector3(1.52f, 0.87f, 0.02f), Cork, false, 0f, null, "carpet");
            Sign(k, null, "notice_hygiene", board + new Vector3(-0.45f, 0f, 0.05f), Quaternion.Euler(0f, 180f, 2f), 0.42f, 0.6f, Plastic);
            Sign(k, null, "notice_leaving", board + new Vector3(0.22f, 0.12f, 0.05f), Quaternion.Euler(0f, 180f, -3f), 0.5f, 0.35f, Plastic);
            Sign(k, null, "sign_ward", board + new Vector3(0.3f, -0.26f, 0.05f), Quaternion.Euler(0f, 180f, 1f), 0.6f, 0.15f, Plastic);

            // A fire extinguisher, a linen trolley, a wheelchair, the wet-floor sign (Day 1), the window's view.
            var ext = new Vector3(14.6f, 0f, 11.12f);
            Part(k, k.root, "Extinguisher", k.meshes.Cylinder(0.075f, 0.075f, 0.5f, 16), ExtinguisherRed, ext + Vector3.up * 0.55f, Quaternion.identity, true, "plastic");
            Part(k, k.root, "Extinguisher top", k.meshes.Cylinder(0.03f, 0.02f, 0.08f, 10), Shoe, ext + Vector3.up * 1.05f, Quaternion.identity, true, "plastic_dark");
            LinenTrolley(k, new Vector3(25.6f, 0f, 13.45f));
            Wheelchair(k, new Vector3(38.2f, 0f, 13.4f), 90f);
            Plant(k, new Vector3(0.5f, 0f, 13.5f), 1.3f);
            Plant(k, new Vector3(0.5f, 0f, 11.5f), 1.1f);
            if (!k.day2)
            {
                var sign = new Vector3(5.6f, 0f, 12.3f);
                Box(k, "Wet floor sign", sign + new Vector3(0f, 0.3f, 0.06f), new Vector3(0.28f, 0.6f, 0.012f), SharpsYellow, false, 0f, Quaternion.Euler(-12f, 20f, 0f), "plastic");
                Box(k, "Wet floor sign", sign + new Vector3(0f, 0.3f, -0.06f), new Vector3(0.28f, 0.6f, 0.012f), SharpsYellow, false, 0f, Quaternion.Euler(12f, 20f, 0f), "plastic");
            }
            Spot(k, DayContent.SpotWindow, new Vector3(0.3f, 1.5f, 12.5f), 1.2f);
        }

        static void LinenTrolley(Kit k, Vector3 t)
        {
            foreach (var o in new[] { new Vector3(-0.22f, 0f, -0.32f), new Vector3(0.22f, 0f, -0.32f), new Vector3(-0.22f, 0f, 0.32f), new Vector3(0.22f, 0f, 0.32f) })
            {
                Part(k, k.root, "Trolley post", k.meshes.Cylinder(0.012f, 0.012f, 0.9f, 8), Steel, t + o + Vector3.up * 0.08f, Quaternion.identity, true, "metal");
                Part(k, k.root, "Castor", k.meshes.Cylinder(0.035f, 0.035f, 0.03f, 10), Shoe, t + o + new Vector3(0f, 0.035f, 0f), Quaternion.Euler(0f, 0f, 90f), true, "plastic_dark");
            }
            Box(k, "Trolley rim", t + Vector3.up * 0.98f, new Vector3(0.48f, 0.025f, 0.68f), Steel, false, 0f, null, "metal");
            Box(k, "Linen bag", t + Vector3.up * 0.62f, new Vector3(0.44f, 0.66f, 0.62f), LinenBlue, true, 0.08f, null, "fabric");
            Box(k, "Folded sheets", t + Vector3.up * 1.03f + Vector3.forward * 0.12f, new Vector3(0.3f, 0.08f, 0.24f), VhWakingStyle.Sheet, false, 0.02f, null, "linen");
        }

        // ---------- The nurses' station ----------

        static void Station(Kit k)
        {
            var c = k.config;
            WallSign(k, "sign_station", new Vector3(21f, 2.58f, 11.06f), Vector3.forward, 0.9f, 0.225f, Plastic);
            // The counter facing the corridor (a raised ledge over the work desk), a gap at its east end to walk in.
            Box(k, "Counter front", new Vector3(20.6f, 0.55f, 10.3f), new Vector3(9.2f, 1.1f, 0.08f), Laminate, true, 0.004f, null, "wood");
            Box(k, "Counter ledge", new Vector3(20.6f, 1.12f, 10.38f), new Vector3(9.3f, 0.04f, 0.34f), Worktop, false, 0.01f, null, "plastic");
            Box(k, "Counter end", new Vector3(16.02f, 0.55f, 9.0f), new Vector3(0.08f, 1.1f, 2.6f), Laminate, true, 0.004f, null, "wood");
            Box(k, "Desk", new Vector3(20.6f, 0.73f, 9.75f), new Vector3(9.0f, 0.04f, 0.9f), Worktop, true, 0.01f, null, "plastic");
            Box(k, "Back desk", new Vector3(21f, 0.73f, 4.45f), new Vector3(11.6f, 0.04f, 0.7f), Worktop, true, 0.01f, null, "plastic");
            for (float x = 17f; x < 26f; x += 3f)
                Box(k, "Desk pedestal", new Vector3(x, 0.36f, 4.45f), new Vector3(0.5f, 0.7f, 0.6f), Laminate, true, 0.01f, null, "wood");

            // Monitors and keyboards on the desk, the desk phone, a mug, binders, the paper tray.
            for (int i = 0; i < 3; i++)
            {
                float x = 17.6f + i * 2.4f;
                var screen = k.style.Unique(Color.black, null, "VH station screen");
                VhWakingStyle.SetEmission(screen, (i == 1 ? new Color(0.36f, 0.5f, 0.62f) : new Color(0.42f, 0.48f, 0.5f)) * 0.75f);
                Box(k, "Monitor", new Vector3(x, 1.07f, 9.5f), new Vector3(0.52f, 0.32f, 0.04f), VhWakingStyle.MetalDark, false, 0.01f, null, "plastic_dark");
                VhMeshKit.Show(k.root, "Monitor screen", k.meshes.Quad(0.48f, 0.28f), screen, new Vector3(x, 1.075f, 9.478f), Quaternion.identity, false);
                Box(k, "Monitor stand", new Vector3(x, 0.84f, 9.54f), new Vector3(0.05f, 0.18f, 0.04f), VhWakingStyle.MetalDark, false, 0f, null, "metal");
                Box(k, "Keyboard", new Vector3(x, 0.76f, 9.85f), new Vector3(0.44f, 0.02f, 0.15f), new Color(0.22f, 0.23f, 0.25f), false, 0.005f, null, "plastic_dark");
                Box(k, "Mouse", new Vector3(x + 0.32f, 0.76f, 9.85f), new Vector3(0.06f, 0.025f, 0.1f), new Color(0.22f, 0.23f, 0.25f), false, 0.012f, null, "plastic_dark");
                Model(k, "chair_desk", new Vector3(x, 0f, 9.0f), 180f, 0.95f);
            }
            AddLight(k, "Screens", new Vector3(20.6f, 1.15f, 9.1f), new Color(0.6f, 0.75f, 0.9f), 0.15f, 2f);
            Box(k, "Desk phone", new Vector3(23.9f, 0.78f, 9.7f), new Vector3(0.22f, 0.06f, 0.2f), new Color(0.24f, 0.25f, 0.27f), false, 0.02f, null, "plastic_dark");
            Box(k, "Handset", new Vector3(23.83f, 0.825f, 9.7f), new Vector3(0.2f, 0.04f, 0.05f), new Color(0.24f, 0.25f, 0.27f), false, 0.018f, null, "plastic_dark");
            Part(k, k.root, "Mug", k.meshes.Cylinder(0.04f, 0.04f, 0.095f, 14), VhWakingStyle.Mug, new Vector3(24.4f, 0.75f, 9.6f), Quaternion.identity, true, "plastic");
            Color[] binders = { new Color(0.3f, 0.4f, 0.58f), new Color(0.58f, 0.28f, 0.26f), new Color(0.3f, 0.48f, 0.38f), new Color(0.62f, 0.56f, 0.32f), new Color(0.3f, 0.4f, 0.58f), new Color(0.45f, 0.45f, 0.48f) };
            for (int i = 0; i < binders.Length; i++)
                Box(k, "Binder", new Vector3(25.2f + i * 0.075f, 0.91f, 4.5f), new Vector3(0.065f, 0.32f, 0.28f), binders[i], false, 0.004f, Quaternion.Euler(0f, 0f, i == 5 ? 10f : 0f), "plastic");
            Box(k, "Paper tray", new Vector3(16.9f, 0.79f, 9.75f), new Vector3(0.32f, 0.08f, 0.24f), PlasticGrey, false, 0.01f, null, "plastic");

            // The round board on the back wall (Rafael's capitals), the clock, wall cupboards, a plant, the bin.
            var board = new Vector3(19.6f, 1.55f, 4.07f);
            Box(k, "Whiteboard frame", board, new Vector3(2.5f, 1.45f, 0.03f), PlasticGrey, false, 0f, null, "metal");
            Sign(k, null, k.day2 ? "board_day2" : "board_day1", board + Vector3.forward * 0.02f, Quaternion.Euler(0f, 180f, 0f), 2.4f, 1.35f, Plastic);
            Spot(k, DayContent.SpotRoundBoard, board, 1.2f);
            Clock(k, new Vector3(23.4f, 2.25f, 4.07f), Vector3.forward, k.day2 ? 4.9f : 3.1f);
            Box(k, "Wall cupboard", new Vector3(24.8f, 2.0f, 4.25f), new Vector3(2.0f, 0.6f, 0.34f), Laminate, false, 0.01f, null, "wood");
            Plant(k, new Vector3(26.4f, 0f, 4.6f), 1.2f);
            Box(k, "Bin", new Vector3(16.5f, 0.18f, 9.5f), new Vector3(0.26f, 0.36f, 0.26f), PlasticGrey, true, 0.03f, null, "plastic");
            // The medicine trolley, parked by the counter's end.
            Box(k, "Medicine trolley", new Vector3(26.2f, 0.5f, 8.2f), new Vector3(0.55f, 0.9f, 0.45f), new Color(0.3f, 0.5f, 0.68f), true, 0.03f, null, "plastic");
            for (int i = 0; i < 4; i++)
                Box(k, "Drawer", new Vector3(26.2f, 0.25f + i * 0.2f, 7.97f), new Vector3(0.5f, 0.17f, 0.01f), new Color(0.86f, 0.86f, 0.84f), false, 0.004f, null, "plastic");
            CeilingLight(k, new Vector3(18f, CeilY - 0.01f, 7.5f), new Vector2(1.2f, 0.6f), new Color(1f, 0.96f, 0.9f), 0.9f, 5.5f, false);
            CeilingLight(k, new Vector3(23.5f, CeilY - 0.01f, 7.5f), new Vector2(1.2f, 0.6f), new Color(1f, 0.96f, 0.9f), 0.9f, 5.5f, true);

            if (!k.day2)
            {
                // The police envelope on the ledge, for the family.
                var env = c.Art("prop_police_envelope");
                Box(k, "Police envelope", new Vector3(18.3f, 1.147f, 10.4f), new Vector3(0.33f, 0.012f, 0.24f), new Color(0.83f, 0.76f, 0.6f), false, 0f, Quaternion.Euler(0f, 8f, 0f), "linen");
                if (env != null)
                    Picture(k, null, "Envelope face", env, Color.white, 0.3f, 0.22f, new Vector3(18.3f, 1.154f, 10.4f), Quaternion.Euler(90f, 8f, 0f));
                Spot(k, "police_report", new Vector3(18.3f, 1.2f, 10.5f), 0.5f);
                // Rafael at the counter, waiting to hand over.
                People(k).Place(VhWardPeople.Rafael, new Vector3(21.3f, 0f, 9.1f), 0f, VhWardPeople.Pose.Standing);
                Spot(k, "rafael", new Vector3(21.3f, 1.4f, 9.1f), 0.9f);
            }
            Spot(k, "station", new Vector3(23.9f, 1f, 10.3f), 0.6f);
        }

        // ---------- Room 14: Elias ----------

        static void Room14(Kit k)
        {
            var c = k.config;
            const float x0 = 19f, x1 = 27f;
            CeilingLight(k, new Vector3(22.6f, CeilY - 0.01f, 18.6f), new Vector2(0.6f, 1.2f), new Color(0.95f, 0.96f, 1f), 0.55f, 5f, true, 0.8f);
            // The door, open into the room against its west wall.
            OpenLeaf(k, new Vector3(19.6f, 0f, 14.06f), Vector3.forward, Vector3.right, 1.0f, DoorBlue);

            var bedAt = new Vector3(22.6f, 0f, 21.2f);
            Bed(k, bedAt);
            Elias(k, bedAt);

            // The headwall: trunking with outlets, the reading light, the oxygen.
            Box(k, "Headwall trunking", new Vector3(22.6f, 1.45f, 22.86f), new Vector3(2.8f, 0.24f, 0.1f), Plastic, false, 0.01f, null, "plastic");
            for (int i = 0; i < 4; i++)
                Box(k, "Socket", new Vector3(21.6f + i * 0.22f, 1.45f, 22.8f), new Vector3(0.14f, 0.08f, 0.02f), Plastic * 0.95f, false, 0f, null, "plastic");
            Part(k, k.root, "Oxygen outlet", k.meshes.Cylinder(0.035f, 0.035f, 0.04f, 12), new Color(0.82f, 0.82f, 0.85f), new Vector3(23.4f, 1.45f, 22.79f), Quaternion.Euler(90f, 0f, 0f), false, "metal");
            Part(k, k.root, "Flowmeter", k.meshes.Cylinder(0.02f, 0.02f, 0.16f, 10), new Color(0.75f, 0.85f, 0.85f), new Vector3(23.4f, 1.24f, 22.75f), Quaternion.identity, false, "glass");
            Box(k, "Reading light", new Vector3(22.6f, 2.05f, 22.84f), new Vector3(1.1f, 0.06f, 0.12f), Plastic, false, 0.02f, null, "plastic");
            AddLight(k, "Reading light (low)", new Vector3(22.6f, 1.9f, 22.4f), new Color(1f, 0.86f, 0.7f), 0.7f, 3.4f, true);

            // The monitor on its arm by the bed's head, alive.
            // (On the far side of the bed's head, so from her chair it's in view over his hand.)
            var mon = new Vector3(21.75f, 1.74f, 22.6f);
            var monRot = Quaternion.Euler(0f, -20f, 0f);
            Box(k, "Monitor arm", new Vector3(21.75f, 1.74f, 22.8f), new Vector3(0.06f, 0.06f, 0.32f), VhWakingStyle.MetalDark, false, 0f, null, "metal");
            Box(k, "Patient monitor", mon, new Vector3(0.4f, 0.3f, 0.1f), new Color(0.82f, 0.82f, 0.8f), false, 0.02f, monRot, "plastic");
            var live = MonitorScreen(k, mon, monRot, true);
            if (k.day2 && JamKit.Shell.GameRoot.Exists && Chain.WasForcedOut(JamKit.Shell.GameRoot.Save.Data, ChainSegment.Night1))
                live.SetResting(74f);
            k.area.patientMonitor = live;
            k.area.monitor = mon;
            Spot(k, k.day2 ? "vitals2" : "vitals", mon, 0.45f);
            AddLight(k, "Monitor glow", mon + new Vector3(0f, 0f, -0.3f), new Color(0.5f, 1f, 0.7f), 0.12f, 1.6f);

            // The chart at the foot of the bed.
            var chartArt = c.Art("prop_chart");
            Box(k, "Clipboard", new Vector3(22.6f, 0.68f, 20.1f), new Vector3(0.24f, 0.32f, 0.015f), Laminate, false, 0.005f, Quaternion.Euler(-8f, 0f, 0f), "wood");
            Picture(k, null, "Chart page", chartArt, VhWakingStyle.Paper, 0.21f, 0.28f, new Vector3(22.6f, 0.675f, 20.09f), Quaternion.Euler(-8f, 0f, 0f));
            Box(k, "Clip", new Vector3(22.6f, 0.84f, 20.09f), new Vector3(0.08f, 0.025f, 0.02f), Steel, false, 0f, null, "metal");
            Spot(k, "chart", new Vector3(22.6f, 0.75f, 20.05f), 0.45f);

            // The drip on its stand at the far side of the bed, the pump, the line to his far hand.
            var drip = new Vector3(21.55f, 0f, 21.9f);
            Part(k, k.root, "Drip stand base", k.meshes.Cylinder(0.24f, 0.24f, 0.03f, 5), VhWakingStyle.MetalDark, drip + Vector3.up * 0.08f, Quaternion.identity, true, "metal");
            Part(k, k.root, "Drip stand", k.meshes.Cylinder(0.015f, 0.015f, 1.95f, 8), Steel, drip + Vector3.up * 0.08f, Quaternion.identity, true, "metal");
            Part(k, k.root, "Drip hooks", k.meshes.Box(new Vector3(0.28f, 0.012f, 0.012f)), Steel, drip + Vector3.up * 2.02f, Quaternion.identity, true, "metal");
            Part(k, k.root, "Drip bag", k.meshes.RoundedBox(new Vector3(0.14f, 0.22f, 0.035f), 0.03f, 3, 0.01f), new Color(0.86f, 0.9f, 0.92f), drip + new Vector3(0.1f, 1.86f, 0f), Quaternion.identity, true, "glass");
            Box(k, "Infusion pump", drip + new Vector3(0f, 1.22f, -0.07f), new Vector3(0.16f, 0.2f, 0.12f), Plastic, false, 0.02f, null, "plastic");
            Glow(k, "Pump display", k.meshes.Quad(0.1f, 0.05f), Color.black, new Color(0.4f, 0.8f, 0.6f), drip + new Vector3(0f, 1.26f, -0.131f), Quaternion.identity);
            if (k.area.elias == null)
            {
                // (The 3D model's line is drawn to his own hand, in EliasModel.)
                var line = new List<Vector3> { drip + new Vector3(0.1f, 1.74f, 0f), drip + new Vector3(0.05f, 1.35f, -0.05f), drip + new Vector3(0.1f, 1.0f, -0.12f), drip + new Vector3(0.4f, 0.82f, -0.4f), new Vector3(22.25f, 0.8f, 21.3f) };
                Part(k, k.root, "Drip line", k.meshes.Tube(line, 0.0035f, 5), new Color(0.85f, 0.88f, 0.9f), Vector3.zero, Quaternion.identity, false, "glass");
            }
            if (k.day2)
            {
                // The round's two bags on the trolley by the drip.
                Box(k, "Bag (saline)", new Vector3(21.0f, 0.95f, 21.3f), new Vector3(0.14f, 0.03f, 0.22f), new Color(0.86f, 0.9f, 0.92f), false, 0.012f, null, "glass");
                Box(k, "Bag (glucose)", new Vector3(21.0f, 0.95f, 20.95f), new Vector3(0.14f, 0.03f, 0.22f), new Color(0.88f, 0.9f, 0.86f), false, 0.012f, null, "glass");
                Box(k, "Trolley top", new Vector3(21.0f, 0.92f, 21.1f), new Vector3(0.45f, 0.03f, 0.6f), Steel, false, 0f, null, "metal");
                Part(k, k.root, "Trolley leg", k.meshes.Cylinder(0.015f, 0.015f, 0.9f, 8), Steel, new Vector3(21.0f, 0f, 21.1f), Quaternion.identity, true, "metal");
                Spot(k, "round2", new Vector3(21.0f, 0.95f, 21.1f), 0.4f);
            }

            // The locker by the bed's head: the photo, a card; on Day 2, what came after Day 1.
            Box(k, "Locker", new Vector3(23.9f, 0.4f, 22.45f), new Vector3(0.48f, 0.8f, 0.44f), Laminate, true, 0.02f, null, "wood");
            Box(k, "Locker drawer", new Vector3(23.9f, 0.62f, 22.22f), new Vector3(0.42f, 0.16f, 0.01f), Laminate * 1.08f, false, 0.004f, null, "wood");
            Box(k, "Drawer pull", new Vector3(23.9f, 0.62f, 22.215f), new Vector3(0.12f, 0.02f, 0.015f), Steel, false, 0.004f, null, "metal");
            var photoArt = c.Art("prop_family_photo");
            var framed = new GameObject("Framed photo").transform;
            framed.SetParent(k.root, false);
            framed.localPosition = new Vector3(24.0f, 0.8f, 22.52f);
            framed.localRotation = Quaternion.Euler(10f, 30f, 0f);
            Part(k, framed, "Frame", k.meshes.Box(new Vector3(0.22f, 0.17f, 0.015f)), VhWakingStyle.WoodDark, new Vector3(0f, 0.085f, 0f), Quaternion.identity, true, "wood");
            Picture(k, framed, "Photo", photoArt, VhWakingStyle.Paper, 0.19f, 0.14f, new Vector3(0f, 0.085f, -0.009f), Quaternion.identity);
            Box(k, "Get-well card", new Vector3(23.75f, 0.88f, 22.58f), new Vector3(0.12f, 0.16f, 0.004f), new Color(0.88f, 0.84f, 0.7f), false, 0f, Quaternion.Euler(0f, 160f, 8f), "linen");
            Spot(k, "photo", new Vector3(24.0f, 0.9f, 22.5f), 0.45f);

            // The over-bed table with water; the curtain on its track.
            var tbl = new Vector3(21.4f, 0f, 20.1f);
            Box(k, "Over-bed table base", tbl + new Vector3(0f, 0.04f, 0f), new Vector3(0.4f, 0.04f, 0.5f), VhWakingStyle.MetalDark, false, 0.01f, null, "metal");
            Part(k, k.root, "Over-bed table post", k.meshes.Cylinder(0.025f, 0.025f, 0.72f, 10), Steel, tbl + new Vector3(0f, 0.06f, -0.18f), Quaternion.identity, true, "metal");
            Box(k, "Over-bed table top", tbl + new Vector3(0f, 0.8f, 0.1f), new Vector3(0.4f, 0.03f, 0.8f), Worktop, true, 0.01f, null, "wood");
            Part(k, k.root, "Water jug", k.meshes.Cylinder(0.05f, 0.045f, 0.2f, 14), new Color(0.78f, 0.84f, 0.88f), tbl + new Vector3(0.05f, 0.815f, 0.25f), Quaternion.identity, true, "glass");
            Part(k, k.root, "Cup", k.meshes.Cylinder(0.033f, 0.038f, 0.09f, 12), Plastic, tbl + new Vector3(-0.08f, 0.815f, 0.05f), Quaternion.identity, true, "plastic");
            Box(k, "Tissues", tbl + new Vector3(-0.08f, 0.86f, 0.38f), new Vector3(0.22f, 0.08f, 0.12f), new Color(0.76f, 0.82f, 0.84f), false, 0.01f, null, "plastic");
            var track = new List<Vector3> { new Vector3(21.1f, 2.6f, 22.9f), new Vector3(21.1f, 2.6f, 19.8f), new Vector3(24.2f, 2.6f, 19.8f), new Vector3(24.2f, 2.6f, 20.4f) };
            Part(k, k.root, "Curtain track", k.meshes.Tube(track, 0.012f, 6), Steel, Vector3.zero, Quaternion.identity, false, "metal");
            var curtain = new Color(0.58f, 0.66f, 0.66f);
            for (int i = 0; i < 9; i++)
                Box(k, "Curtain (pulled back)", new Vector3(21.1f + (i % 2 == 0 ? 0.03f : -0.03f), 1.45f, 22.8f - i * 0.09f), new Vector3(0.03f, 2.25f, 0.11f), curtain, false, 0f, Quaternion.Euler(0f, i % 2 == 0 ? 35f : -35f, 0f), "fabric");

            // The window over the radiator; LeBaget's lilies on the sill.
            Box(k, "Radiator", new Vector3(25.4f, 0.5f, 22.86f), new Vector3(1.6f, 0.55f, 0.1f), new Color(0.88f, 0.88f, 0.86f), true, 0.01f, null, "plastic");
            Bouquet(k, new Vector3(24.9f, 0.94f, 22.92f));
            Spot(k, "bouquet", new Vector3(24.9f, 1.2f, 22.8f), 0.5f);

            // The sink by the door, its mirror, the towel and glove dispensers, the sharps bin, a pedal bin.
            Box(k, "Sink", new Vector3(19.3f, 0.82f, 15.4f), new Vector3(0.42f, 0.18f, 0.55f), Plastic, true, 0.04f, null, "plastic");
            Part(k, k.root, "Tap", k.meshes.Tube(new List<Vector3> { new Vector3(19.1f, 0.92f, 15.4f), new Vector3(19.1f, 1.08f, 15.4f), new Vector3(19.25f, 1.1f, 15.4f) }, 0.012f, 8), Steel, Vector3.zero, Quaternion.identity, true, "metal");
            Box(k, "Mirror", new Vector3(19.08f, 1.55f, 15.4f), new Vector3(0.02f, 0.6f, 0.45f), new Color(0.55f, 0.6f, 0.64f), false, 0f, null, "glass");
            Box(k, "Paper towels", new Vector3(19.12f, 1.35f, 16.05f), new Vector3(0.12f, 0.34f, 0.28f), Plastic, false, 0.02f, null, "plastic");
            Box(k, "Sharps bin", new Vector3(19.15f, 1.15f, 16.5f), new Vector3(0.15f, 0.26f, 0.2f), SharpsYellow, false, 0.015f, null, "plastic");
            Part(k, k.root, "Pedal bin", k.meshes.Cylinder(0.14f, 0.14f, 0.42f, 16), new Color(0.78f, 0.78f, 0.76f), new Vector3(19.4f, 0f, 16.9f), Quaternion.identity, true, "metal");

            // The visitors' armchair in the corner (Day 1: his effects bag on it), and her chair by his bed.
            Chair(k, new Vector3(26.2f, 0f, 15.2f), -135f, ChairTeal);
            if (!k.day2)
            {
                Box(k, "Effects bag", new Vector3(26.2f, 0.66f, 15.2f), new Vector3(0.42f, 0.3f, 0.28f), new Color(0.85f, 0.86f, 0.88f), false, 0.07f, Quaternion.Euler(0f, 25f, 4f), "plastic");
                Box(k, "Effects label", new Vector3(26.04f, 0.7f, 15.06f), new Vector3(0.1f, 0.06f, 0.004f), VhWakingStyle.Paper, false, 0f, Quaternion.Euler(0f, 25f, 4f), "linen");
                Spot(k, "effects", new Vector3(26.2f, 0.75f, 15.2f), 0.5f);
            }
            else
                Box(k, "Effects bag", new Vector3(24.35f, 0.13f, 22.5f), new Vector3(0.36f, 0.26f, 0.24f), new Color(0.85f, 0.86f, 0.88f), false, 0.07f, Quaternion.Euler(0f, -20f, 0f), "plastic");
            var chairAt = new Vector3(23.75f, 0f, 20.85f);
            Chair(k, chairAt, -78f, ChairBlue);
            Spot(k, k.day2 ? "sit" : "sit1", chairAt + Vector3.up * 0.7f, 0.5f);
            k.area.bedsideStand = new Vector3(24.55f, 0f, 20.5f);
            k.area.bedsideEye = chairAt + new Vector3(-0.06f, 1.16f, 0.02f);
            k.area.bedsideYaw = -66f;
            k.area.bedsidePitch = 14f;
        }

        /// <summary>Elias in bed: the gown at his shoulders, the blanket folded back over his chest, his arms out on it, his
        /// near hand (the bedside's) a real hand with fingers that answer her.</summary>
        static void Elias(Kit k, Vector3 bed)
        {
            if (EliasModel(k, bed)) return;
            var skin = new Color(0.78f, 0.62f, 0.52f);
            Part(k, k.root, "Elias (under the blanket)", k.meshes.RoundedBox(new Vector3(0.48f, 0.2f, 1.45f), 0.1f, 5, 0.04f), HospitalBlanket, bed + new Vector3(0f, 0.73f, -0.25f), Quaternion.identity, true, "fabric");
            Part(k, k.root, "Blanket", k.meshes.RoundedBox(new Vector3(0.94f, 0.06f, 1.5f), 0.03f, 4, 0.012f), HospitalBlanket, bed + new Vector3(0f, 0.67f, -0.28f), Quaternion.identity, true, "fabric");
            Part(k, k.root, "Sheet turned down", k.meshes.RoundedBox(new Vector3(0.92f, 0.05f, 0.18f), 0.02f, 3, 0.01f), VhWakingStyle.Sheet, bed + new Vector3(0f, 0.81f, 0.43f), Quaternion.identity, true, "linen");
            Part(k, k.root, "Elias (shoulders, gown)", k.meshes.RoundedBox(new Vector3(0.46f, 0.17f, 0.32f), 0.075f, 5, 0.01f), Gown, bed + new Vector3(0f, 0.78f, 0.62f), Quaternion.identity, true, "linen");
            Part(k, k.root, "Elias (neck)", k.meshes.Cylinder(0.055f, 0.055f, 0.12f, 12), skin, bed + new Vector3(0f, 0.82f, 0.75f), Quaternion.Euler(90f, 0f, 0f), true, "skin");
            var headRot = Quaternion.Euler(-74f, 0f, 0f);
            var head = bed + new Vector3(0f, 0.87f, 0.94f);
            Part(k, k.root, "Elias (head)", k.meshes.RoundedBox(new Vector3(0.18f, 0.22f, 0.22f), 0.085f, 6, 0.012f), skin, head, headRot, true, "skin");
            Part(k, k.root, "Elias (nose)", k.meshes.RoundedBox(new Vector3(0.03f, 0.045f, 0.05f), 0.012f, 3), skin * 0.97f, head + headRot * new Vector3(0f, -0.01f, 0.115f), headRot, true, "skin");
            Part(k, k.root, "Elias (hair)", k.meshes.RoundedBox(new Vector3(0.2f, 0.12f, 0.215f), 0.06f, 5, 0.01f), new Color(0.15f, 0.11f, 0.09f), head + headRot * new Vector3(0f, 0.085f, -0.01f), headRot, true, "carpet");
            Part(k, k.root, "Elias (stubble)", k.meshes.RoundedBox(new Vector3(0.17f, 0.08f, 0.2f), 0.06f, 4), new Color(0.42f, 0.34f, 0.3f), head + headRot * new Vector3(0f, -0.075f, 0.01f), headRot, false, "carpet");
            var cannula = new List<Vector3> { new Vector3(23.4f, 1.4f, 22.78f), new Vector3(23.2f, 1.0f, 22.6f), head + new Vector3(0.12f, 0.06f, 0.0f), head + new Vector3(0.0f, 0.1f, -0.08f), head + new Vector3(-0.12f, 0.06f, 0.0f) };
            Part(k, k.root, "Nasal cannula", k.meshes.Tube(cannula, 0.004f, 5), new Color(0.82f, 0.9f, 0.9f), Vector3.zero, Quaternion.identity, false, "glass");

            var hand = M(k, skin, "skin");
            var gown = M(k, Gown, "linen");
            // His far arm (the drip's) on the blanket.
            Part(k, k.root, "Elias (far arm, gown)", k.meshes.Cylinder(0.052f, 0.05f, 0.24f, 12), Gown, bed + new Vector3(-0.29f, 0.8f, 0.6f), Quaternion.Euler(-90f, 0f, 0f), true, "linen");
            var far = new GameObject("Elias (far hand)").transform;
            far.SetParent(k.root, false);
            // (He lies on his back, head north: his right hand is on the west side, his left on the east, hers; palms down, the
            // thumbs towards his middle.)
            far.localPosition = bed + new Vector3(-0.31f, 0.735f, -0.1f);
            far.localRotation = Quaternion.Euler(0f, 184f, 0f);
            far.gameObject.AddComponent<VhHandRig>().Build(k.meshes, hand, gown, 1f, -1f, 0.46f, 0.36f);
            Box(k, "Cannula dressing", bed + new Vector3(-0.31f, 0.758f, -0.06f), new Vector3(0.04f, 0.006f, 0.05f), new Color(0.94f, 0.94f, 0.92f), false, 0.002f, null, "linen");
            // His near arm (the bedside's): the hand palm down on the blanket, fingers towards the foot of the bed.
            Part(k, k.root, "Elias (near arm, gown)", k.meshes.Cylinder(0.052f, 0.05f, 0.24f, 12), Gown, bed + new Vector3(0.29f, 0.8f, 0.6f), Quaternion.Euler(-90f, 0f, 0f), true, "linen");
            var near = new GameObject("Elias (near hand)").transform;
            near.SetParent(k.root, false);
            near.localPosition = bed + new Vector3(0.31f, 0.735f, -0.12f);
            near.localRotation = Quaternion.Euler(0f, 176f, 0f);
            var rig = near.gameObject.AddComponent<VhHandRig>();
            rig.Build(k.meshes, hand, gown, 1.04f, 1f, 0.46f, 0.36f);
            rig.curl = 0.22f;
            Part(k, k.root, "Wristband", k.meshes.Cylinder(0.036f, 0.036f, 0.022f, 14), new Color(0.94f, 0.94f, 0.92f), bed + new Vector3(0.31f, 0.735f, -0.07f), Quaternion.Euler(90f, 0f, 0f), false, "plastic");
            Box(k, "Nurse call", bed + new Vector3(0.18f, 0.71f, -0.5f), new Vector3(0.05f, 0.03f, 0.14f), Plastic, false, 0.01f, Quaternion.Euler(0f, 20f, 0f), "plastic");
            k.area.eliasHand = rig;
            k.area.bedsideHold = near.position + Vector3.up * 0.035f + near.forward * 0.045f;
            k.area.bedsideHandForward = near.forward;
            Spot(k, "elias", bed + new Vector3(0f, 0.9f, 0.4f), 1.1f);
            Spot(k, k.day2 ? "wristband2" : "wristband", bed + new Vector3(0.31f, 0.78f, -0.07f), 0.35f);
        }

        /// <summary>
        /// Elias in bed as the People agent's 3D model (EliasBed: the gown, the head bandage, eyes painted shut, the wristband
        /// and cannula; animation "lie"). The clip's pose is laid along the bed (his head on the pillow, his back on the
        /// mattress), whatever way the clip lies; a blanket over his legs to the waist; the nasal cannula to his face and the
        /// drip to his far hand; and his near hand (the chair's side) bound to <see cref="VhBedHand"/> so his fingers answer
        /// her. What the model can't do: open his eyes (painted shut, no lids), move his face (he's in a coma: it stays
        /// still), or grip her hand back (the fingers flex; the hand doesn't close round hers). False: no model in the build.
        /// </summary>
        static bool EliasModel(Kit k, Vector3 bed)
        {
            var c = People(k).Cast(VhWardPeople.Elias, bed + new Vector3(0f, 0.66f, 0f), 0f, VhWardPeople.Pose.LyingInBed);
            if (c == null) return false;
            var an = c.animator;
            if (an == null || !an.isHuman || an.GetBoneTransform(HumanBodyBones.Hips) == null || an.GetBoneTransform(HumanBodyBones.Head) == null)
            {
                Debug.LogWarning("JAMKIT_VH ward: EliasBed has no humanoid pose; the stand-in lies there");
                k.area.cast.Remove(c);
                VhUtil.Kill(c.gameObject);
                return false;
            }
            c.breathe = 0.5f;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            an.Update(0f);

            var bedWorld = k.root.TransformPoint(bed);
            var bedRot = k.root.rotation;
            var bedFwd = bedRot * Vector3.forward;
            var bedRight = bedRot * Vector3.right;
            var hips = an.GetBoneTransform(HumanBodyBones.Hips);
            var head = an.GetBoneTransform(HumanBodyBones.Head);
            var lf = an.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = an.GetBoneTransform(HumanBodyBones.RightFoot);
            var feet = lf != null && rf != null ? (lf.position + rf.position) * 0.5f : hips.position - (head.position - hips.position);
            // Along the bed: feet to head towards the headboard.
            var axis = head.position - feet;
            axis.y = 0f;
            if (axis.sqrMagnitude > 1e-4f)
                c.transform.RotateAround(hips.position, Vector3.up, Vector3.SignedAngle(axis, bedFwd, Vector3.up));
            // His head on the pillow, his hips on the mattress.
            var headAt = bedWorld + bedFwd * 0.8f;
            var d = headAt - head.position;
            d.y = 0f;
            c.transform.position += d;
            c.transform.position += Vector3.up * (bedWorld.y + 0.77f - hips.position.y);
            an.Update(0f);

            // The near hand: the one on the chair's side (+X of the bed).
            var lh = an.GetBoneTransform(HumanBodyBones.LeftHand);
            var rh = an.GetBoneTransform(HumanBodyBones.RightHand);
            bool nearLeft = lh != null && (rh == null || Vector3.Dot(lh.position - rh.position, bedRight) >= 0f);
            var fingers = c.gameObject.AddComponent<VhBedHand>();
            fingers.Bind(an, nearLeft);
            an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            k.area.elias = c;
            k.area.eliasFingers = fingers;
            var nearHand = nearLeft ? lh : rh;
            var farHand = nearLeft ? rh : lh;
            k.area.bedsideHold = fingers.Ready ? fingers.HoldPoint : nearHand != null ? nearHand.position + Vector3.up * 0.03f : bedWorld + bedRight * 0.3f + Vector3.up * 0.75f;
            k.area.bedsideHandForward = fingers.Ready ? fingers.Fingers : -bedFwd;

            // The blanket over his legs, folded back at his waist; the sheet's turned edge.
            var hipsLocal = k.root.InverseTransformPoint(hips.position);
            float top = Mathf.Clamp(hipsLocal.z - bed.z + 0.12f, -0.4f, 0.4f);
            float foot = -0.98f;
            Part(k, k.root, "Blanket (over his legs)", k.meshes.RoundedBox(new Vector3(0.92f, 0.2f, top - foot), 0.06f, 5, 0.03f), HospitalBlanket, bed + new Vector3(0f, 0.75f, (top + foot) * 0.5f), Quaternion.identity, true, "fabric");
            Part(k, k.root, "Sheet turned down", k.meshes.RoundedBox(new Vector3(0.94f, 0.05f, 0.16f), 0.02f, 3, 0.01f), VhWakingStyle.Sheet, bed + new Vector3(0f, 0.84f, top - 0.04f), Quaternion.identity, false, "linen");

            // The nasal cannula from the oxygen outlet over his ears to his nose; the drip to his far hand.
            var h = k.root.InverseTransformPoint(head.position);
            var up = Vector3.up;
            var toTop = k.root.InverseTransformDirection(bedFwd);
            var side = k.root.InverseTransformDirection(bedRight);
            var cannula = new List<Vector3>
            {
                new Vector3(23.4f, 1.4f, 22.78f), new Vector3(23.2f, 1.0f, 22.6f),
                h + side * 0.1f + up * 0.05f, h + up * 0.11f - toTop * 0.07f, h - side * 0.1f + up * 0.05f,
            };
            Part(k, k.root, "Nasal cannula", k.meshes.Tube(cannula, 0.004f, 5), new Color(0.82f, 0.9f, 0.9f), Vector3.zero, Quaternion.identity, false, "glass");
            if (farHand != null)
            {
                var drip = new Vector3(21.55f, 0f, 21.9f);
                var to = k.root.InverseTransformPoint(farHand.position) + up * 0.03f;
                var line = new List<Vector3> { drip + new Vector3(0.1f, 1.74f, 0f), drip + new Vector3(0.05f, 1.35f, -0.05f), drip + new Vector3(0.1f, 1.0f, -0.12f), Vector3.Lerp(drip + new Vector3(0.4f, 0.9f, -0.4f), to, 0.5f) + up * 0.08f, to };
                Part(k, k.root, "Drip line (to his hand)", k.meshes.Tube(line, 0.0035f, 5), new Color(0.85f, 0.88f, 0.9f), Vector3.zero, Quaternion.identity, false, "glass");
            }
            Box(k, "Nurse call", bed + new Vector3(0.18f, 0.71f, -0.5f), new Vector3(0.05f, 0.03f, 0.14f), Plastic, false, 0.01f, Quaternion.Euler(0f, 20f, 0f), "plastic");
            var wrist = nearHand != null ? k.root.InverseTransformPoint(nearHand.position) : bed + new Vector3(0.31f, 0.78f, -0.07f);
            Spot(k, "elias", bed + new Vector3(0f, 0.9f, 0.4f), 1.1f);
            Spot(k, k.day2 ? "wristband2" : "wristband", wrist + Vector3.up * 0.04f, 0.35f);
            return true;
        }

        /// <summary>A live monitor screen on a monitor's face (<paramref name="beeps"/>: room 14's, heard).</summary>
        static VhPatientMonitor MonitorScreen(Kit k, Vector3 at, Quaternion rot, bool beeps)
        {
            var go = new GameObject("Monitor screen (live)");
            go.transform.SetParent(k.root, false);
            go.transform.localPosition = at + rot * new Vector3(0f, 0.005f, -0.052f);
            go.transform.localRotation = rot;
            var mon = go.AddComponent<VhPatientMonitor>();
            var mat = mon.Build(k.style, k.lib != null ? k.lib.Picture("monitor_digits") : null, beeps);
            VhMeshKit.Show(go.transform, "Screen", k.meshes.Quad(0.34f, 0.2125f), mat, Vector3.zero, Quaternion.identity, false);
            return mon;
        }

        // ---------- Room 12: Mrs. Okafor ----------

        static void Room12(Kit k)
        {
            CeilingLight(k, new Vector3(9.8f, CeilY - 0.01f, 18.6f), new Vector2(0.6f, 1.2f), new Color(0.95f, 0.96f, 1f), 0.6f, 5f, false, 0.8f);
            OpenLeaf(k, new Vector3(8.4f, 0f, 14.06f), Vector3.right, Vector3.forward, 1.0f, DoorBlue);
            var bed = new Vector3(9.9f, 0f, 21.2f);
            Bed(k, bed, 0f, true);
            People(k).Place(VhWardPeople.Okafor, bed, 180f, VhWardPeople.Pose.InBed);
            Box(k, "Okafor's blanket", bed + new Vector3(0f, 0.7f, -0.35f), new Vector3(0.9f, 0.16f, 1.3f), new Color(0.72f, 0.58f, 0.62f), false, 0.06f, null, "fabric");
            // Her red wristband (penicillin), her table with the tray, her monitor.
            Box(k, "Over-bed table", bed + new Vector3(0f, 0.86f, -0.4f), new Vector3(0.8f, 0.03f, 0.4f), Worktop, true, 0.01f, null, "wood");
            Part(k, k.root, "Over-bed table post", k.meshes.Cylinder(0.025f, 0.025f, 0.84f, 10), Steel, bed + new Vector3(0.6f, 0f, -0.4f), Quaternion.identity, true, "metal");
            Box(k, "Tray", bed + new Vector3(0.1f, 0.885f, -0.42f), new Vector3(0.36f, 0.02f, 0.26f), new Color(0.45f, 0.62f, 0.7f), false, 0.008f, null, "plastic");
            Part(k, k.root, "Toast plate", k.meshes.Cylinder(0.09f, 0.09f, 0.012f, 18), Plastic, bed + new Vector3(0.0f, 0.9f, -0.42f), Quaternion.identity, false, "plastic");
            Part(k, k.root, "Medicine cup", k.meshes.Cylinder(0.022f, 0.026f, 0.04f, 10), Plastic, bed + new Vector3(0.2f, 0.9f, -0.38f), Quaternion.identity, false, "plastic");
            var mon = bed + new Vector3(1.05f, 1.72f, 1.42f);
            Box(k, "Patient monitor", mon, new Vector3(0.4f, 0.3f, 0.1f), new Color(0.82f, 0.82f, 0.8f), false, 0.02f, null, "plastic");
            var live = MonitorScreen(k, mon, Quaternion.identity, false);
            live.restingRate = k.day2 ? 96f : 78f;
            live.spo2 = 96;
            live.systolic = 132;
            live.diastolic = 80;
            live.SetResting(live.restingRate);
            Chair(k, bed + new Vector3(1.3f, 0f, -0.3f), -90f, ChairTeal);
            Spot(k, DayContent.SpotOkafor, bed + new Vector3(0f, 1.15f, 0.55f), 1.0f);
            if (k.day2) Spot(k, DayContent.SpotOkaforChart, mon, 0.5f);
            Plant(k, new Vector3(12.4f, 0f, 22.5f), 0.7f);
        }

        // ---------- Room 16: Mr. Brandt ----------

        static void Room16(Kit k)
        {
            CeilingLight(k, new Vector3(36.6f, CeilY - 0.01f, 18.6f), new Vector2(0.6f, 1.2f), new Color(0.95f, 0.96f, 1f), 0.55f, 5f, false, 0.8f);
            OpenLeaf(k, new Vector3(35f, 0f, 14.06f), Vector3.right, Vector3.forward, 1.0f, DoorBlue);
            var bed = new Vector3(36.6f, 0f, 21.2f);
            Bed(k, bed, 0f, k.day2);
            People(k).Place(VhWardPeople.Brandt, bed, 180f, k.day2 ? VhWardPeople.Pose.InBed : VhWardPeople.Pose.LyingInBed);
            Box(k, "Brandt's blanket", bed + new Vector3(0f, 0.7f, -0.35f), new Vector3(0.9f, 0.16f, 1.3f), HospitalBlanket, false, 0.06f, null, "fabric");
            var mon = bed + new Vector3(1.05f, 1.72f, 1.42f);
            Box(k, "Patient monitor", mon, new Vector3(0.4f, 0.3f, 0.1f), new Color(0.82f, 0.82f, 0.8f), false, 0.02f, null, "plastic");
            var live = MonitorScreen(k, mon, Quaternion.identity, false);
            live.restingRate = 70f;
            live.systolic = 141;
            live.diastolic = 86;
            live.SetResting(70f);
            Chair(k, bed + new Vector3(-1.3f, 0f, -0.2f), 90f, ChairTeal);
            if (k.day2)
            {
                Spot(k, DayContent.SpotBrandt, bed + new Vector3(0f, 1.15f, 0.55f), 1.0f);
                Spot(k, DayContent.SpotBrandtBand, bed + new Vector3(-0.15f, 0.9f, -0.42f), 0.4f);
                // His drug chart on the over-bed table.
                Box(k, "Over-bed table", bed + new Vector3(0f, 0.86f, -0.4f), new Vector3(0.8f, 0.03f, 0.4f), Worktop, true, 0.01f, null, "wood");
                Box(k, "Drug chart", bed + new Vector3(-0.15f, 0.88f, -0.42f), new Vector3(0.21f, 0.01f, 0.29f), VhWakingStyle.Paper, false, 0f, Quaternion.Euler(0f, 6f, 0f), "linen");
            }
        }

        // ---------- The staff room ----------

        static void StaffRoom(Kit k)
        {
            WallSign(k, "sign_staff", new Vector3(13.7f, 2.45f, 11.06f), Vector3.forward, 0.7f, 0.175f, Plastic);
            OpenLeaf(k, new Vector3(13.2f, 0f, 10.94f), Vector3.left, Vector3.back, 1.0f, DoorBlue);
            CeilingLight(k, new Vector3(9f, CeilY - 0.01f, 6f), new Vector2(1.2f, 0.6f), new Color(1f, 0.92f, 0.82f), 0.8f, 6f, true);
            CeilingLight(k, new Vector3(12.5f, CeilY - 0.01f, 3f), new Vector2(1.2f, 0.6f), new Color(1f, 0.92f, 0.82f), 0.6f, 5f, false);

            // The kitchenette under the south window: cupboards, the worktop, the sink, the kettle, a microwave, the fridge.
            Box(k, "Kitchen units", new Vector3(9.4f, 0.43f, 0.4f), new Vector3(5.6f, 0.86f, 0.6f), new Color(0.9f, 0.9f, 0.88f), true, 0.01f, null, "plastic");
            for (float x = 7.1f; x < 12.2f; x += 0.6f)
                Box(k, "Unit door", new Vector3(x, 0.45f, 0.705f), new Vector3(0.56f, 0.7f, 0.01f), new Color(0.92f, 0.92f, 0.9f), false, 0.006f, null, "plastic");
            Box(k, "Worktop", new Vector3(9.4f, 0.885f, 0.42f), new Vector3(5.7f, 0.04f, 0.66f), Laminate, false, 0.008f, null, "wood");
            Box(k, "Splashback", new Vector3(9.4f, 1.05f, 0.08f), new Vector3(5.7f, 0.3f, 0.02f), Plastic, false, 0f, null, "tiles");
            Box(k, "Sink (kitchen)", new Vector3(8.2f, 0.88f, 0.42f), new Vector3(0.5f, 0.04f, 0.4f), Steel, false, 0.01f, null, "metal");
            Part(k, k.root, "Tap", k.meshes.Tube(new List<Vector3> { new Vector3(8.2f, 0.9f, 0.18f), new Vector3(8.2f, 1.12f, 0.18f), new Vector3(8.2f, 1.14f, 0.36f) }, 0.012f, 8), Steel, Vector3.zero, Quaternion.identity, true, "metal");
            Part(k, k.root, "Kettle", k.meshes.Cylinder(0.085f, 0.07f, 0.22f, 16), new Color(0.82f, 0.84f, 0.86f), new Vector3(9.7f, 0.905f, 0.45f), Quaternion.identity, true, "metal");
            Part(k, k.root, "Kettle handle", k.meshes.Tube(new List<Vector3> { new Vector3(9.62f, 1.06f, 0.45f), new Vector3(9.7f, 1.18f, 0.45f), new Vector3(9.79f, 1.06f, 0.45f) }, 0.012f, 6), PlasticGrey, Vector3.zero, Quaternion.identity, true, "plastic_dark");
            Part(k, k.root, "Mug (WORLD'S OKAYEST)", k.meshes.Cylinder(0.042f, 0.042f, 0.1f, 14), new Color(0.86f, 0.3f, 0.26f), new Vector3(9.95f, 0.905f, 0.55f), Quaternion.identity, true, "plastic");
            Box(k, "Microwave", new Vector3(11.3f, 1.06f, 0.4f), new Vector3(0.5f, 0.3f, 0.38f), new Color(0.24f, 0.25f, 0.27f), false, 0.02f, null, "plastic_dark");
            Box(k, "Microwave door", new Vector3(11.25f, 1.06f, 0.6f), new Vector3(0.34f, 0.24f, 0.01f), Glass, false, 0f, null, "glass");
            Box(k, "Fridge", new Vector3(12.75f, 0.85f, 0.42f), new Vector3(0.62f, 1.7f, 0.66f), new Color(0.92f, 0.92f, 0.9f), true, 0.03f, null, "plastic");
            Box(k, "Fridge handle", new Vector3(12.5f, 1.2f, 0.77f), new Vector3(0.03f, 0.4f, 0.03f), Steel, false, 0.01f, null, "metal");
            Box(k, "Wall cupboards", new Vector3(7.2f, 1.95f, 0.24f), new Vector3(1.1f, 0.65f, 0.34f), new Color(0.9f, 0.9f, 0.88f), false, 0.01f, null, "plastic");
            Spot(k, DayContent.SpotKettle, new Vector3(9.7f, 1.0f, 0.5f), 0.6f);

            // The lockers along the west wall (hers, fourth from the window), the bench in front of them.
            for (int i = 0; i < 8; i++)
            {
                float z = 3.4f + i * 0.5f;
                Box(k, "Locker", new Vector3(6.32f, 0.95f, z), new Vector3(0.5f, 1.9f, 0.48f), new Color(0.46f, 0.56f, 0.64f), true, 0.006f, null, "metal");
                Box(k, "Locker vent", new Vector3(6.575f, 1.7f, z), new Vector3(0.01f, 0.12f, 0.3f), new Color(0.3f, 0.36f, 0.42f), false, 0f, null, "metal");
                Box(k, "Locker handle", new Vector3(6.58f, 1.05f, z + 0.15f), new Vector3(0.02f, 0.12f, 0.03f), Steel, false, 0.006f, null, "metal");
            }
            Box(k, "Bench", new Vector3(7.1f, 0.42f, 5.15f), new Vector3(0.36f, 0.05f, 2.8f), Laminate, true, 0.01f, null, "wood");
            Box(k, "Bench leg", new Vector3(7.1f, 0.2f, 4.0f), new Vector3(0.3f, 0.4f, 0.04f), Steel, false, 0f, null, "metal");
            Box(k, "Bench leg", new Vector3(7.1f, 0.2f, 6.3f), new Vector3(0.3f, 0.4f, 0.04f), Steel, false, 0f, null, "metal");
            Spot(k, DayContent.SpotLocker, new Vector3(6.6f, 1.3f, 4.9f), 0.6f);

            // The table and four chairs, the sofa, the rota on the north wall.
            Box(k, "Table", new Vector3(10.6f, 0.73f, 5.6f), new Vector3(1.6f, 0.04f, 0.9f), Laminate, true, 0.01f, null, "wood");
            Part(k, k.root, "Table leg", k.meshes.Cylinder(0.03f, 0.03f, 0.71f, 10), Steel, new Vector3(10.0f, 0f, 5.6f), Quaternion.identity, true, "metal");
            Part(k, k.root, "Table leg", k.meshes.Cylinder(0.03f, 0.03f, 0.71f, 10), Steel, new Vector3(11.2f, 0f, 5.6f), Quaternion.identity, true, "metal");
            Chair(k, new Vector3(10.1f, 0f, 4.75f), 0f, ChairBlue, false);
            if (!k.day2)
            {
                // Her phone, face down by her mug ("Not now.").
                Box(k, "Her phone", new Vector3(9.95f, 0.757f, 5.85f), new Vector3(0.075f, 0.01f, 0.15f), new Color(0.14f, 0.15f, 0.17f), false, 0.004f, Quaternion.Euler(0f, -14f, 0f), "plastic_dark");
                Part(k, k.root, "Her mug", k.meshes.Cylinder(0.042f, 0.042f, 0.1f, 14), new Color(0.3f, 0.42f, 0.6f), new Vector3(10.15f, 0.75f, 5.95f), Quaternion.identity, true, "plastic");
                Spot(k, "phone", new Vector3(9.95f, 0.8f, 5.85f), 0.4f);
            }
            Chair(k, new Vector3(11.1f, 0f, 6.45f), 180f, ChairBlue, false);
            Chair(k, new Vector3(9.4f, 0f, 5.6f), 90f, ChairBlue, false);
            if (k.day2)
            {
                // On his break, in the chair at the table's end (the model sits on the seat's centre).
                People(k).Place(VhWardPeople.Rafael, new Vector3(11.85f, 0f, 5.6f), -90f, VhWardPeople.Pose.Sitting);
                Chair(k, new Vector3(11.85f, 0f, 5.6f), -90f, ChairBlue, false);
                Part(k, k.root, "Rafael's mug", k.meshes.Cylinder(0.042f, 0.042f, 0.1f, 14), new Color(0.86f, 0.3f, 0.26f), new Vector3(11.1f, 0.75f, 5.7f), Quaternion.identity, true, "plastic");
                Spot(k, "rafael2", new Vector3(11.75f, 1.1f, 5.6f), 0.9f);
            }
            else
                Chair(k, new Vector3(11.85f, 0f, 5.6f), -90f, ChairBlue, false);
            Box(k, "Sofa seat", new Vector3(14.45f, 0.38f, 5.0f), new Vector3(0.85f, 0.32f, 2.0f), new Color(0.4f, 0.44f, 0.5f), true, 0.06f, null, "fabric");
            Box(k, "Sofa back", new Vector3(14.78f, 0.72f, 5.0f), new Vector3(0.22f, 0.6f, 2.0f), new Color(0.4f, 0.44f, 0.5f), true, 0.06f, null, "fabric");
            Box(k, "Sofa arm", new Vector3(14.45f, 0.55f, 3.95f), new Vector3(0.85f, 0.4f, 0.16f), new Color(0.4f, 0.44f, 0.5f), true, 0.05f, null, "fabric");
            Box(k, "Sofa arm", new Vector3(14.45f, 0.55f, 6.05f), new Vector3(0.85f, 0.4f, 0.16f), new Color(0.4f, 0.44f, 0.5f), true, 0.05f, null, "fabric");
            Box(k, "Cushion", new Vector3(14.5f, 0.62f, 4.4f), new Vector3(0.18f, 0.36f, 0.36f), new Color(0.72f, 0.5f, 0.32f), false, 0.07f, Quaternion.Euler(0f, 0f, -14f), "fabric");
            var rota = new Vector3(10.2f, 1.55f, 10.92f);
            Box(k, "Rota board", rota + Vector3.back * 0.01f, new Vector3(1.32f, 1.05f, 0.02f), Cork, false, 0f, null, "carpet");
            Sign(k, null, "notice_rota", rota + Vector3.back * 0.025f, Quaternion.identity, 1.2f, 0.94f, Plastic);
            Spot(k, DayContent.SpotRota, rota, 0.9f);
            Clock(k, new Vector3(12.8f, 2.2f, 10.94f), Vector3.back, k.day2 ? 4.9f : 3.1f);
        }

        // ---------- The quiet room ----------

        static void QuietRoom(Kit k)
        {
            WallSign(k, "sign_quiet", new Vector3(4.1f, 2.45f, 11.06f), Vector3.forward, 0.7f, 0.175f, Plastic);
            OpenLeaf(k, new Vector3(4.6f, 0f, 10.94f), Vector3.right, Vector3.back, 0.9f, Laminate);
            AddLight(k, "Quiet room lamp", new Vector3(3.2f, 2.3f, 6f), new Color(1f, 0.82f, 0.62f), 0.55f, 6f, false);
            // Two short rows of chairs facing the window; the low table with the candles under it; the book on its lectern.
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < 3; i++)
                    Chair(k, new Vector3(3.2f + r * 1.2f, 0f, 3.9f + i * 1.2f), -90f, new Color(0.5f, 0.36f, 0.32f));
            Box(k, "Candle table", new Vector3(0.65f, 0.4f, 5.5f), new Vector3(0.5f, 0.06f, 1.8f), Laminate, true, 0.01f, null, "wood");
            Box(k, "Candle table legs", new Vector3(0.65f, 0.19f, 5.5f), new Vector3(0.42f, 0.38f, 1.7f), Laminate * 0.8f, false, 0.01f, null, "wood");
            for (int i = 0; i < 7; i++)
            {
                var at = new Vector3(0.6f + (i % 2) * 0.12f, 0.43f, 4.8f + i * 0.22f);
                float h = 0.08f + (i % 3) * 0.04f;
                Part(k, k.root, "Candle", k.meshes.Cylinder(0.025f, 0.025f, h, 12), new Color(0.94f, 0.92f, 0.86f), at, Quaternion.identity, true, "plastic");
                Glow(k, "Flame", k.meshes.RoundedBox(new Vector3(0.012f, 0.026f, 0.012f), 0.005f, 2), new Color(1f, 0.8f, 0.4f), new Color(1f, 0.65f, 0.25f) * 2.2f, at + Vector3.up * (h + 0.014f), Quaternion.identity);
            }
            AddLight(k, "Candles", new Vector3(0.9f, 0.7f, 5.5f), new Color(1f, 0.62f, 0.3f), 0.5f, 2.4f, false);
            Spot(k, DayContent.SpotCandles, new Vector3(0.7f, 0.6f, 5.5f), 0.7f);
            var lectern = new Vector3(1.8f, 0f, 9.2f);
            Box(k, "Lectern post", lectern + new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 1.0f, 0.12f), Laminate, true, 0.01f, null, "wood");
            Box(k, "Lectern foot", lectern + new Vector3(0f, 0.03f, 0f), new Vector3(0.4f, 0.06f, 0.4f), Laminate, false, 0.01f, null, "wood");
            Box(k, "Lectern top", lectern + new Vector3(0f, 1.03f, 0f), new Vector3(0.5f, 0.04f, 0.4f), Laminate, false, 0.008f, Quaternion.Euler(-20f, 0f, 0f), "wood");
            Sign(k, null, "book_pages", lectern + new Vector3(0f, 1.065f, 0.007f), Quaternion.Euler(70f, 0f, 0f), 0.48f, 0.3f, VhWakingStyle.Paper);
            Spot(k, DayContent.SpotBook, lectern + Vector3.up * 1.05f, 0.6f);
            // A banner on the east wall: a calm field of colour.
            Box(k, "Banner", new Vector3(5.92f, 1.5f, 5.5f), new Vector3(0.02f, 1.6f, 1.0f), new Color(0.4f, 0.5f, 0.62f), false, 0f, null, "fabric");
            Plant(k, new Vector3(0.6f, 0f, 9.8f), 1.2f);
        }

        // ---------- The lift lobby ----------

        static void Lobby(Kit k)
        {
            // The lifts' steel doors on the south wall, their call buttons, the floor's directory, a bench.
            foreach (float x in new[] { 29.6f, 33.4f })
            {
                Box(k, "Lift doors", new Vector3(x, 1.08f, 0.1f), new Vector3(1.2f, 2.16f, 0.05f), Steel, false, 0f, null, "metal");
                Box(k, "Lift door seam", new Vector3(x, 1.08f, 0.13f), new Vector3(0.01f, 2.12f, 0.01f), Shoe, false, 0f, null, "plastic_dark");
                Box(k, "Lift surround", new Vector3(x, 2.25f, 0.1f), new Vector3(1.4f, 0.14f, 0.06f), Steel, false, 0f, null, "metal");
                Box(k, "Call buttons", new Vector3(x + 0.85f, 1.1f, 0.08f), new Vector3(0.1f, 0.2f, 0.03f), Steel, false, 0.005f, null, "metal");
                Glow(k, "Call light", k.meshes.Box(new Vector3(0.03f, 0.03f, 0.012f)), Color.black, new Color(1f, 0.7f, 0.3f), new Vector3(x + 0.85f, 1.15f, 0.1f), Quaternion.identity);
            }
            WallSign(k, "sign_lifts", new Vector3(31.5f, 2.45f, 0.06f), Vector3.forward, 0.6f, 0.15f, Plastic);
            WallSign(k, "sign_directory", new Vector3(27.06f, 1.55f, 6f), Vector3.right, 0.8f, 1.0f, Plastic);
            WallSign(k, "sign_ward", new Vector3(32f, 2.58f, 10.94f), Vector3.back, 0.9f, 0.225f, Plastic);
            WallSign(k, "sign_family", new Vector3(36.94f, 2.4f, 7.2f), Vector3.left, 0.7f, 0.175f, Plastic);
            Box(k, "Bench", new Vector3(35.6f, 0.44f, 3.5f), new Vector3(0.45f, 0.06f, 1.6f), Laminate, true, 0.01f, null, "wood");
            Box(k, "Bench frame", new Vector3(35.6f, 0.21f, 3.5f), new Vector3(0.4f, 0.42f, 1.5f), Steel, false, 0.01f, null, "metal");
            Plant(k, new Vector3(27.6f, 0f, 2.6f), 1.4f);
            Plant(k, new Vector3(36.4f, 0f, 1.0f), 1.0f);
            CeilingLight(k, new Vector3(32f, CeilY - 0.01f, 4f), new Vector2(1.2f, 0.6f), new Color(0.96f, 0.97f, 1f), 0.8f, 6f, false);
            CeilingLight(k, new Vector3(32f, CeilY - 0.01f, 8.5f), new Vector2(1.2f, 0.6f), new Color(0.96f, 0.97f, 1f), 0.8f, 6f, false);
        }

        // ---------- The family room ----------

        static void FamilyRoom(Kit k)
        {
            CeilingLight(k, new Vector3(42.5f, CeilY - 0.01f, 4.5f), new Vector2(1.2f, 0.6f), new Color(1f, 0.94f, 0.84f), 0.75f, 6f, false);
            CeilingLight(k, new Vector3(42.5f, CeilY - 0.01f, 8.5f), new Vector2(1.2f, 0.6f), new Color(1f, 0.94f, 0.84f), 0.6f, 6f, false);
            // The TV on the north wall: the news on mute.
            var tv = new Vector3(42.5f, 1.85f, 10.9f);
            Box(k, "TV", tv, new Vector3(1.3f, 0.76f, 0.07f), new Color(0.12f, 0.12f, 0.13f), false, 0.01f, null, "plastic_dark");
            var news = k.lib != null ? k.lib.Picture("tv_news") : null;
            var screen = k.style.Screen(news, 1.3f, "VH family room TV");
            VhMeshKit.Show(k.root, "TV screen", k.meshes.Quad(1.24f, 0.7f), screen, tv + new Vector3(0f, 0f, -0.037f), Quaternion.identity, false);
            AddLight(k, "TV glow", tv + new Vector3(0f, -0.2f, -0.6f), new Color(0.6f, 0.62f, 0.9f), 0.25f, 3f);
            Spot(k, DayContent.SpotTv, tv, 1.2f);
            // Two rows of chairs facing it; a low table with magazines; the toy corner; the vending machine.
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < 5; i++)
                {
                    float x = 39.6f + i * 0.95f;
                    if (r == 0 && (i == 2 || i == 4)) continue;
                    Chair(k, new Vector3(x, 0f, 7.6f - r * 1.6f), 0f, ChairTeal);
                }
            Box(k, "Low table", new Vector3(45.6f, 0.38f, 4.2f), new Vector3(0.9f, 0.04f, 0.6f), Laminate, true, 0.01f, null, "wood");
            Box(k, "Magazines", new Vector3(45.5f, 0.41f, 4.2f), new Vector3(0.3f, 0.02f, 0.22f), new Color(0.8f, 0.4f, 0.3f), false, 0.004f, Quaternion.Euler(0f, 12f, 0f), "linen");
            Box(k, "Toy rug", new Vector3(46.4f, 0.005f, 2.9f), new Vector3(2f, 0.01f, 1.4f), new Color(0.62f, 0.5f, 0.32f), false, 0f, null, "carpet");
            Color[] toys = { new Color(0.86f, 0.3f, 0.26f), new Color(0.3f, 0.5f, 0.86f), new Color(0.9f, 0.76f, 0.24f), new Color(0.3f, 0.7f, 0.4f) };
            for (int i = 0; i < 7; i++)
                Box(k, "Toy block", new Vector3(45.9f + (i % 3) * 0.3f, 0.05f + (i / 3) * 0.09f, 2.7f + (i % 2) * 0.25f), Vector3.one * 0.09f, toys[i % 4], false, 0.01f, Quaternion.Euler(0f, i * 23f, 0f), "plastic");
            var vend = new Vector3(47.5f, 0f, 9.9f);
            Box(k, "Vending machine", vend + new Vector3(0f, 0.95f, 0f), new Vector3(0.8f, 1.9f, 0.9f), new Color(0.2f, 0.22f, 0.25f), true, 0.02f, null, "plastic_dark");
            var front = k.lib != null ? k.lib.Picture("vending_front") : null;
            var vendScreen = k.style.Screen(front, 0.9f, "VH vending front");
            VhMeshKit.Show(k.root, "Vending front", k.meshes.Quad(0.74f, 1.48f), vendScreen, vend + new Vector3(-0.405f, 1.05f, 0f), Quaternion.Euler(0f, 90f, 0f), false);
            AddLight(k, "Vending glow", vend + new Vector3(-0.8f, 1.2f, 0f), new Color(0.8f, 0.9f, 1f), 0.3f, 2.5f);
            Spot(k, DayContent.SpotVending, vend + new Vector3(-0.4f, 1.2f, 0f), 0.7f);
            Plant(k, new Vector3(47.4f, 0f, 2.6f), 1.3f);

            if (!k.day2)
            {
                People(k).Place(VhWardPeople.June, new Vector3(41.5f, 0f, 6.0f), 0f, VhWardPeople.Pose.Sitting);
                // Her knitting in her lap: the wool, the needles.
                Box(k, "Knitting", new Vector3(41.5f, 0.66f, 6.3f), new Vector3(0.2f, 0.06f, 0.14f), new Color(0.82f, 0.56f, 0.32f), false, 0.03f, null, "fabric");
                Part(k, k.root, "Needle", k.meshes.Cylinder(0.003f, 0.003f, 0.3f, 6), Steel, new Vector3(41.4f, 0.66f, 6.2f), Quaternion.Euler(0f, 0f, 70f), false, "metal");
                Spot(k, DayContent.SpotJune, new Vector3(41.5f, 1.1f, 6.1f), 0.9f);
                People(k).Place(VhWardPeople.WaitingMan, new Vector3(43.4f, 0f, 6.0f), 0f, VhWardPeople.Pose.Sitting);
            }
            else
            {
                // The woman from LeBaget (People's representative), waiting by the door; her basket of fruit on the chair
                // beside her (the model's hands are empty).
                People(k).Place(VhWardPeople.Visitor, new Vector3(38.4f, 0f, 8.6f), -100f, VhWardPeople.Pose.Standing);
                var basket = new Vector3(39.6f, 0.565f, 7.6f);
                Part(k, k.root, "Fruit basket", k.meshes.RoundedBox(new Vector3(0.32f, 0.12f, 0.22f), 0.04f, 3), Laminate, basket, Quaternion.Euler(0f, -100f, 0f), true, "wood");
                Part(k, k.root, "Cellophane", k.meshes.RoundedBox(new Vector3(0.3f, 0.16f, 0.2f), 0.07f, 3, 0.02f), new Color(0.85f, 0.88f, 0.9f), basket + Vector3.up * 0.1f, Quaternion.Euler(0f, -100f, 0f), false, "glass");
                Color[] fruit = { new Color(0.78f, 0.45f, 0.22f), new Color(0.62f, 0.2f, 0.18f), new Color(0.82f, 0.74f, 0.3f) };
                for (int i = 0; i < 3; i++)
                    Part(k, k.root, "Fruit", k.meshes.RoundedBox(Vector3.one * 0.075f, 0.036f, 3), fruit[i], basket + new Vector3(-0.06f + i * 0.06f, 0.09f, (i % 2) * 0.04f), Quaternion.identity, false, "plastic");
                Spot(k, "visitor", new Vector3(38.4f, 1.4f, 8.6f), 0.9f);
                People(k).Place(VhWardPeople.WaitingMan, new Vector3(43.4f, 0f, 6.0f), 0f, VhWardPeople.Pose.Sitting);
            }
        }
    }
}
