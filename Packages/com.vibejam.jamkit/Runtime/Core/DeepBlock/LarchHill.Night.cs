using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.DeepBlock
{
    /// <summary>Night 1's story spots, the critical path's legs (with the autopilot's cues), the escape line through the
    /// middle band, the camera views and the bands. docs/redesign/world.md "Night 1, beat by beat".</summary>
    public sealed partial class LarchHill
    {
        // ---------- The story's spots ----------

        void MakeStory()
        {
            arrival = HillAnchors.Night1Arrival;
            arrivalYaw = HillAnchors.Night1ArrivalYaw;
            float y = RowanGround;
            // The far bank and the loop (the Lindqvists' front garden, opposite the arrival on the rise).
            AddSpot(SpotCreekEdge, "the creek's edge (the kids' jump)", 97f, FarBankY + 1f, -18f, 14f, 3f, 5f);
            AddSpot(SpotCreekWater, "in the creek (wading)", 0f, CreekBed + 1f, (CreekZ0 + CreekZ1) * 0.5f, EastX * 2f, 2.6f, CreekZ1 - CreekZ0);
            AddSpot(SpotSprinkler, "the sprinkler", 121.5f, FarRiseY + 0.6f, -66f);
            AddSpot(SpotNeighbour, "Mrs. Lindqvist (her roses)", 134f, FarRiseY + 0.6f, -61.5f, yaw: -130f);
            AddSpot(SpotDog, "the dog", 124.5f, FarRiseY + 0.6f, -71f, yaw: 60f);
            AddSpot(SpotBallFrom, "the ball (thrown)", 132.8f, FarRiseY + 1.8f, -62.2f);
            AddSpot(SpotBallTo, "the ball (lands)", 118.5f, FarRiseY + 0.6f, -59f);
            AddSpot(SpotLoopZone, "within earshot of the Lindqvists", 124f, FarRiseY + 3f, -54f, 56f, 12f, 44f);
            // Walt's shop and yard.
            AddSpot(SpotGarageDoor, "the half-open garage door", 95f, FootY + 1f, -2.6f, 9f, 3f, 4f);
            AddSpot(SpotPaper, "Saturday's paper (on the counter)", 101.8f, FootY + 1.06f, 9.1f);
            AddSpot(SpotLedger, "Walt's ledger (on the workbench)", 86.8f, FootY + 0.98f, 5.4f);
            AddSpot(SpotPatchedBike, "the bike with the patched rear tyre", 96f, FootY, 6.5f);
            AddSpot(SpotYard, "Walt's yard", (WaltX0 + WaltX1) * 0.5f, FootY + 1f, 27.3f, WaltX1 - WaltX0, 3f, 25.4f);
            AddSpot(SpotWalt, "Walt, sitting on the wall", WaltX0 + 0.45f, FootY, 36f, yaw: 90f);
            AddSpot(SpotWaltAside, "Walt, up off the wall", 88.5f, FootY, 31f, yaw: 60f);
            // The back way's lessons (where Walt or Mags says the move).
            AddSpot(SpotLessonMantle, "the low wall", 87f, FootY + 1f, 36f, 6f, 3f, 8f);
            AddSpot(SpotLessonWallrun, "the Pruitts' patio", 80.5f, FootY + 1f, 31f, 7f, 3f, 17.6f);
            AddSpot(SpotPool, "in the Pruitts' pool", (PoolX0 + PoolX1) * 0.5f, PoolFloor + 0.8f, 31.15f, PoolX1 - PoolX0, 2f, 17.7f);
            AddSpot(SpotLessonSlide, "the Pruitts' deck", 62.5f, FootY + 1f, 31f, 5f, 3f, 17.6f);
            AddSpot(SpotLessonClimb, "the Okafors' lawn by the fence", 40f, FootY + 1f, 23f, 8f, 3f, 34f);
            AddSpot(SpotLessonChimney, "the slot's mouth", 35f, HendersonY + 1f, 33f, 2f, 4f, 14f);
            // The lower band.
            AddSpot(SpotMillStreet, "Mill Street, up out of the slot", 14f, MillFoot + 1f, 42f, 8f, 3f, 4f);
            AddSpot(SpotChatter, "Mr. Okonkwo washing his car (Mill Street)", 80f, MillFoot + 1f, 50f, 18f, 4f, 10f);
            AddSpot(SpotGreen, "Larch Green", 0f, LaneY, 77f, 16f, 5f, 46f);
            AddSpot(SpotLane, "Back Lane B's mouth", -12f, LaneY + 1f, 97f, 8f, 4f, 6f);
            AddSpot(SpotNail, "the nail in the lane (outside Mr. Bell's workshop)", -53f, LaneY + 0.05f, 95.2f);
            AddSpot(SpotDitch, "the ditch", (DitchX0 + DitchX1) * 0.5f, LaneY, 97f, 12f, 6f, 6f);
            AddSpot(SpotShutter, "the Old Mill's shutter", MillX1 + 3f, LaneY + 1f, 97f, 6f, 3f, 6f);
            AddSpot(SpotOak, "under the old oak", -104f, LaneY + 1f, 97f, 8f, 4f, 6f);
            AddSpot(SpotTreehouse, "the treehouse (NADS + ELI)", -113.2f, 12.62f, 95.6f);
            AddSpot(SpotRowan, "Rowan Street's west end", -120f, RowanY + 1.5f, 108f, 40f, 4f, 16f);
            AddSpot(SpotParty, "the block party", -50f, RowanY + 1f, 108f, 120f, 5f, 16f);
            // The house: the gate where Night 1 ends, Elias at his barbecue in the side garden, the window, the door.
            AddSpot(SpotGate, "the Lasnames' gate", HillAnchors.FrontGate.X, RowanFoot + 1f, RowanNorthZ - 0.9f, 5f, 3f, 2.2f);
            AddSpot(SpotBarbecue, "Elias at his barbecue", 39.1f, y, 124.3f, yaw: 160f);
            AddSpot(SpotElias, "Elias, at the stove (through the window)", houseOrigin.X - 4.5f, y, houseOrigin.Z + 2.7f, yaw: 180f);
            AddSpot(SpotWindow, "the front window", houseOrigin.X - 4.3f, y + 1.65f, houseOrigin.Z);
            AddSpot(SpotDoor, "the front door", houseOrigin.X, y, houseOrigin.Z);
            AddSpot(SpotPorchLight, "the porch light", houseOrigin.X + 1.85f, y + 2.3f, houseOrigin.Z - 0.1f);
        }

        // ---------- Routes ----------

        BlockRoute NewRoute(string name, string chapter, string layer, float docSeconds, float startY, params (float x, float z)[] path)
        {
            var c = new LabCourse();
            var pts = new List<Vector3>();
            foreach (var (x, z) in path) pts.Add(new Vector3(x, 0f, z));
            c.route = new PathLine(pts);
            c.lane.Add(new Vector2(0f, 0f));
            c.killY = -8f;
            var r = new BlockRoute { name = name, chapter = chapter, layer = layer, course = c, docSeconds = docSeconds };
            r.start = new Vector3(path[0].x, startY + 0.02f, path[0].z);
            var d = c.route.DirectionAt(0f);
            r.startYaw = (float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI);
            r.finishS = c.route.Length - 0.5f;
            c.finishS = r.finishS;
            c.checkpoints.Add(new LabCheckpoint { name = name + " start", chapter = chapter, position = r.start, yaw = r.startYaw, s = 0f });
            return r;
        }

        static float S(BlockRoute r, float x, float z) => r.course.route.Project(new Vector3(x, 0f, z));
        static void Cue(BlockRoute r, LabCueKind kind, float s, float sEnd = 0f, float x = 0f) => r.course.cues.Add(new LabCue { kind = kind, s = s, sEnd = sEnd, x = x });
        static void Lane(BlockRoute r, float s, float x) => r.course.lane.Add(new Vector2(s, x));
        static void Beat(BlockRoute r, CourseBeatKind kind, string name, float s, float size, float height) =>
            r.beats.Add(new CourseBeat { kind = kind, name = name, s = s, size = size, height = height });

        /// <summary>The finishing height of each leg (where the autopilot must stand at the end).</summary>
        public readonly Dictionary<BlockRoute, float> finishY = new Dictionary<BlockRoute, float>();

        void MakeRoutes()
        {
            // The way in: down off the rise to the kids' jump, over the creek (the first double jump), under the garage door
            // (the first slide), through the shop and out of its back door into Walt's yard.
            var w = NewRoute("The way in", ChapterArrival, "farbank", 14f, FarRiseY,
                (128f, -42f), (103f, -30f), (97f, -24f), (97f, -6f), (95f, -4.4f), (95f, 4f), (96.1f, 9.5f), (96.1f, 18f));
            Cue(w, LabCueKind.Jump, S(w, 97f, -16.7f));
            Cue(w, LabCueKind.DoubleJumpAtApex, S(w, 97f, -14f));
            Cue(w, LabCueKind.Crouch, S(w, 95f, -3.6f), S(w, 95f, -0.9f));
            Beat(w, CourseBeatKind.DoubleGap, "The creek (the first double jump)", S(w, 97f, -12f), CreekZ1 - CreekZ0, 0f);
            Beat(w, CourseBeatKind.Slide, "Walt's half-open garage door (the first slide)", S(w, 95f, -1.85f), 0.3f, GarageDoorGap);
            wayIn = w;
            finishY[w] = FootY;

            // Leg 1, the back way: over Walt's low wall (mantle), along Mill Street's wall over the Pruitts' pool (wall-run),
            // under the fallen pergola (slide), up onto the Hendersons' (jump and grab), then up the slot (wall-jumps).
            var b = NewRoute("The back way", ChapterBackWay, "rowa", 30f, FootY,
                (91f, 38.6f), (85f, 39.1f), (79f, 39.1f), (64f, 39.1f), (60f, 38.4f), (44f, 35f), (36f, 34f), (35f, 34.5f), (34f, 38.25f),
                (12.6f, 38.25f), (12.6f, 41.6f), (12.6f, 43f));
            Beat(b, CourseBeatKind.Mantle, "Walt's low wall", S(b, WaltX0, 39f), 0.3f, 1.6f);
            Cue(b, LabCueKind.Jump, S(b, PoolX1 + 1.4f, 39.1f));
            Beat(b, CourseBeatKind.Wallrun, "Along Mill Street's wall, over the Pruitts' pool", S(b, PoolX1, 39.1f), PoolX1 - PoolX0, MillFoot);
            Cue(b, LabCueKind.Crouch, S(b, PoolX0 - 1.2f, 39f), S(b, A1X0 - 1.2f, 38.2f));
            Beat(b, CourseBeatKind.Slide, "Under the fallen pergola", S(b, A1X0, 38.4f), 0.6f, PergolaGap);
            Cue(b, LabCueKind.Jump, S(b, A2X0 + 1.2f, 34f));
            Beat(b, CourseBeatKind.Mantle, "Up onto the Hendersons' (jump and grab)", S(b, A2X0, 34f), 0.2f, HendersonY + 1.0f - 1.25f);
            float slotS = S(b, SlotX1 - 0.6f, 38.25f);
            Cue(b, LabCueKind.Jump, slotS + 0.4f);
            Cue(b, LabCueKind.Zigzag, slotS + 1.2f, S(b, SlotX0 + 0.2f, 38.25f), 1.15f);
            Beat(b, CourseBeatKind.Chimney, "The slot (wall-jumps up to Mill Street)", slotS, MillWallZ0 - SlotZ0, MillFoot - SlotFloor);
            routes.Add(b);
            night1.Add(b);
            finishY[b] = MillFoot;

            // Leg 2a: across Mill Street and up Larch Green to the mouth of Back Lane B (the rest: the house in view).
            var g = NewRoute("Up the green", ChapterLane, "green", 14f, MillFoot,
                (12.6f, 43f), (5f, 50f), (0f, 58f), (0f, 90f), (-6f, 96.4f), (-11f, 96.4f));
            routes.Add(g);
            night1.Add(g);
            finishY[g] = LaneY;

            // Leg 2b: the garage roofs (crates up; a 3 m gap; a 5 m gap; the ditch, double jump; down into the lane).
            var rf = NewRoute("The garage roofs", ChapterLane, "lane", 16f, LaneY,
                (-11f, 96.4f), (-13.5f, 93.4f), (-17f, 91f), (-77.5f, 91f), (-78.6f, 96.8f), (-79.4f, 96.8f));
            Beat(rf, CourseBeatKind.Mantle, "The crates", S(rf, -14.7f, 93.2f), 1.2f, 1.4f);
            Beat(rf, CourseBeatKind.Mantle, "Up onto the Mirandas' lock-ups", S(rf, -16f, 91f), 1f, GarageRoof - LaneY - 1.4f);
            Cue(rf, LabCueKind.Jump, S(rf, -27.4f, 91f));
            Beat(rf, CourseBeatKind.Gap, "Between the lock-ups and the Novaks' garages", S(rf, -28f, 91f), 3f, 0f);
            Cue(rf, LabCueKind.Jump, S(rf, -42.4f, 91f));
            Beat(rf, CourseBeatKind.Gap, "Over to Mr. Bell's workshop", S(rf, -43f, 91f), 5f, 0f);
            Cue(rf, LabCueKind.Jump, S(rf, DitchX1 + 0.6f, 91f));
            Cue(rf, LabCueKind.DoubleJumpAtApex, S(rf, DitchX1 - 2f, 91f));
            Beat(rf, CourseBeatKind.DoubleGap, "The ditch", S(rf, DitchX1, 91f), DitchX1 - DitchX0, 0f);
            routes.Add(rf);
            night1.Add(rf);
            finishY[rf] = LaneY;

            // Leg 2c: the Old Mill's arch (its shutter on the loop's timer), along Rowan Street's wall over the loading bay
            // (wall-run), under the scaffold's bar (slide), up the old oak's limbs (mantles) onto Rowan Street.
            var ln = NewRoute("The far end of the lane", ChapterOak, "lane", 14f, LaneY,
                (-79.4f, 96.8f), (-91f, 97.2f), (-93f, 99.1f), (-100.2f, 99.1f), (-102.6f, 99.1f), (-110.6f, 99.1f), (-110.8f, 102.6f), (-110.8f, 103.4f));
            Cue(ln, LabCueKind.Jump, S(ln, BayX1 + 0.6f, 99.1f));
            Beat(ln, CourseBeatKind.Wallrun, "Along Rowan Street's wall over the loading bay", S(ln, BayX1, 99.1f), BayX1 - BayX0, RowanFoot - LaneY);
            Cue(ln, LabCueKind.Crouch, S(ln, BayX0 - 0.3f, 99.1f), S(ln, ScaffoldX - 0.6f, 99.1f));
            Beat(ln, CourseBeatKind.Slide, "Under the scaffold's bar", S(ln, ScaffoldX, 99.1f), 0.4f, 1.15f);
            Beat(ln, CourseBeatKind.Mantle, "The oak's low limb", S(ln, -103.5f, 99.1f), 3f, 1.5f);
            Beat(ln, CourseBeatKind.Mantle, "The oak's middle limb", S(ln, -106.5f, 99.1f), 3f, 1.4f);
            Beat(ln, CourseBeatKind.Mantle, "The oak's limb over the wall", S(ln, -109.5f, 99.1f), 2.5f, 1.5f);
            routes.Add(ln);
            night1.Add(ln);
            finishY[ln] = RowanFoot;

            // Leg 3, the Rewind: east along Rowan Street's south footpath through the block party, ahead of the wave, to the
            // Lasnames' gate: the gazebo's curtain (slide), the bales (vault), along the truck over the trench (wall-run), the
            // skip (mantle), the trench across the street (double jump), the stage, the gate.
            var rw = NewRoute("The Rewind", ChapterRewind, "rowan", 20f, RowanFoot,
                (-110.8f, 103.4f), (-73f, 103.4f), (-72f, 104.3f), (-60f, 104.3f), (-58f, 103.4f), (-2f, 103.4f), (14f, 110f), (23.4f, 115.6f));
            Cue(rw, LabCueKind.Crouch, S(rw, GazeboX - 2.2f, 103.4f), S(rw, GazeboX + 0.6f, 103.4f));
            Beat(rw, CourseBeatKind.Slide, "Under the gazebo's curtain", S(rw, GazeboX, 103.4f), 0.5f, 1.15f);
            Beat(rw, CourseBeatKind.Mantle, "The hay bales", S(rw, BalesX - 0.7f, 103.4f), 1.4f, 1.3f - 0.15f);
            Cue(rw, LabCueKind.Jump, S(rw, TrenchX0 - 0.7f, 104.3f));
            Beat(rw, CourseBeatKind.Wallrun, "Along the removal truck over the trench", S(rw, TrenchX0, 104.3f), TrenchX1 - TrenchX0, TruckHeight);
            Beat(rw, CourseBeatKind.Mantle, "The skip", S(rw, CarX - 1f, 103.4f), 2f, 1.6f);
            Cue(rw, LabCueKind.Jump, S(rw, CrossTrenchX0 - 0.8f, 103.4f));
            Cue(rw, LabCueKind.DoubleJumpAtApex, S(rw, CrossTrenchX0 + 2f, 103.4f));
            Beat(rw, CourseBeatKind.DoubleGap, "The trench across the street", S(rw, CrossTrenchX0, 103.4f), CrossTrenchX1 - CrossTrenchX0, 0f);
            Beat(rw, CourseBeatKind.Mantle, "The stage", S(rw, StageX0, 103.4f), StageX1 - StageX0, 1.1f - 0.15f);
            routes.Add(rw);
            night1.Add(rw);
            finishY[rw] = RowanFoot;

            // Night 2: the middle band's line from where the escape bursts out to the Route 9 stop (blocked out; the House
            // agent's escape owns the run up to its exit).
            var e = NewRoute("The middle band (Night 2)", ChapterEscapeLine, "middle", 30f, HillAnchors.M1Y,
                (HillAnchors.EscapeExit.X, HillAnchors.EscapeExit.Z), (100f, 153f), (111f, 160f), (111f, 222f), (HillAnchors.EscapeEnd.X, HillAnchors.EscapeEnd.Z));
            routes.Add(e);
            escapeLine = e;
            finishY[e] = HillAnchors.M3Y;
        }

        // ---------- Views (the judge's stills; docs/redesign/world.md "Captures") ----------

        void View(string name, float ex, float ey, float ez, float lx, float ly, float lz) =>
            views.Add(new HillView { name = name, eye = new Vector3(ex, ey, ez), look = new Vector3(lx, ly, lz) });

        void MakeViews()
        {
            const float e = 1.5f;
            View("hill-01-arrival-the-whole-hill", arrival.X, arrival.Y + e, arrival.Z, 40f, 26f, 150f);
            View("hill-02-the-creek-and-walts-shop", 101f, FarBankY + e + 0.4f, -19.5f, 95f, -1.5f, 1f);
            View("hill-03-the-lindqvists-loop", 116f, FarRiseY + e, -46f, 128f, FarRiseY + 1f, -68f);
            View("hill-04-inside-walts-shop", 104.5f, e, 0.6f, 87f, 1f, 6.5f);
            View("hill-05-walts-yard-walt-on-the-wall", 106f, e, 17f, WaltX0, 1.2f, 36f);
            View("hill-06-the-pool-wall-run-player-height", 82.5f, e, 36.5f, 58f, 1.6f, 40f);
            View("hill-07-the-okafors-and-the-hendersons-fence", 57f, e, 26f, 36f, 2.4f, 32f);
            View("hill-08-the-slot-chimney", 33.6f, SlotFloor + e, 38.25f, 13f, 4.5f, 38.25f);
            View("hill-09-mill-street-looking-back-down", 15f, MillFoot + e, 42.4f, 100f, 0f, -30f);
            View("hill-10-larch-green-up-to-the-house", 0f, MillFoot + e + 0.3f, 60f, 24f, 14f, 125f);
            View("hill-11-back-lane-garage-roofs", -17f, GarageRoof + e, 91f, -60f, GarageRoof, 92f);
            View("hill-12-the-ditch", -49.5f, GarageRoof + e, 91f, -63f, 1.5f, 93f);
            View("hill-13-the-old-mill-shutter", -71f, LaneY + e, 97f, -86f, LaneY + 1.6f, 97f);
            View("hill-14-the-old-oak", -95f, LaneY + e, 96.5f, -110f, 9f, 98.5f);
            View("hill-15-rowan-block-party-the-rewind", -108f, RowanFoot + e, 103.4f, -40f, RowanY + 1.5f, 108f);
            View("hill-16-the-truck-and-the-trench", -77f, RowanFoot + e + 0.4f, 108.5f, -66f, RowanY - 1.2f, 103.2f);
            View("hill-17-the-house-and-its-block", 6f, RowanFoot + e, 104f, 24f, 13f, 128f);
            View("hill-18-elias-at-the-barbecue", 33f, RowanFoot + e, 113.6f, 39.4f, RowanGround + 1.2f, 124.6f);
            View("hill-19-the-middle-band-from-chapel-row", -24f, HillAnchors.M1Y + e + 0.15f, 152f, 40f, 28f, 210f);
            View("hill-20-st-annes-and-the-school", -2f, HillAnchors.M2Y + e + 0.15f, 189f, 50f, 30f, 206f);
            View("hill-21-route-9-bus-stop", 100f, HillAnchors.M3Y + e + 0.15f, 216f, 113f, HillAnchors.M3Y + 1.6f, 228f);
            View("hill-22-crest-road-shops", -30f, HillAnchors.CrestY + e + 0.15f, 286f, 20f, HillAnchors.CrestY + 4f, 300f);
            View("hill-23-st-brigids-hospital", 0f, HillAnchors.SummitY + e, 302f, 0f, HillAnchors.SummitY + 22f, 340f);
            View("hill-24-the-hospital-on-the-skyline-from-the-yards", 58f, e, 20f, 0f, 55f, 330f);
            View("hill-25-the-top-looking-down-the-hill", 0f, HillAnchors.SummitY + e + 0.2f, 309.5f, 8f, 10f, 80f);
        }

        // ---------- Bands ----------

        void MakeBands()
        {
            void Band(string id, string name, float floorY, float x, float y, float z, float yaw) =>
                bands.Add(new HillBand { id = id, name = name, floorY = floorY, recover = new Vector3(x, y, z), recoverYaw = yaw });
            Band("farbank", "the far bank", FarBankY, HillAnchors.Night1Arrival.X, HillAnchors.Night1Arrival.Y, HillAnchors.Night1Arrival.Z, HillAnchors.Night1ArrivalYaw);
            Band("walt", "Walt's shop and yard", FootY, 96f, FootY, 18f, 0f);
            Band("rowa", "the back way (the Row A yards)", FootY, 90f, FootY, 38.6f, -90f);
            Band("mill", "Mill Street and the green", MillFoot, 12.6f, MillFoot, 43f, -45f);
            Band("lane", "Back Lane B", LaneY, -11f, LaneY, 96.4f, -90f);
            Band("rowan", "Rowan Street", RowanFoot, -110.8f, RowanFoot, 103.4f, 90f);
            Band("middle", "the middle band", HillAnchors.M1Y, HillAnchors.EscapeExit.X, HillAnchors.M1Y, HillAnchors.EscapeExit.Z, 0f);
            Band("top", "the top", HillAnchors.CrestY, 0f, HillAnchors.CrestY, 288f, 0f);
        }
    }
}
