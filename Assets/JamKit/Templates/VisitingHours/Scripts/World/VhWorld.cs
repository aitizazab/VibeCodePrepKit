using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The slice's three places, built in code when they start: St. Brigid's corridor and room 14
    /// (cool, flat fluorescent light), the booth in the closed sleep-research wing at 11 PM, and
    /// Walt's street in the dream (golden hour, painted house flats like a stage set). Positions of
    /// the things to examine are here; what they do is in the config and the ink.
    /// </summary>
    public static class VhWorld
    {
        public static VhArea Build(SliceArea which, VisitingHoursConfig config, Transform parent)
        {
            var area = NewArea(which, parent);
            var steps = BuildSteps(area, config);
            while (steps.MoveNext()) { }
            return area;
        }

        /// <summary>An empty place (its root under <paramref name="parent"/>); <see cref="BuildSteps"/> fills it.</summary>
        public static VhArea NewArea(SliceArea which, Transform parent)
        {
            var root = new GameObject(which.ToString()).transform;
            root.SetParent(parent, false);
            return new VhArea { area = which, root = root };
        }

        /// <summary>Builds a place a step at a time (one step a frame: the ward is built room by room, perf.md Day item 3);
        /// the small places in one step.</summary>
        public static System.Collections.IEnumerator BuildSteps(VhArea area, VisitingHoursConfig config)
        {
            switch (area.area)
            {
                // The waking world in style E (VhWakingWard); the slice's painted versions below are kept for reference.
                case SliceArea.Hospital: return VhWakingWard.HospitalSteps(area, config, false);
                case SliceArea.HospitalDay2: return VhWakingWard.HospitalSteps(area, config, true);
                case SliceArea.Booth: VhWakingWard.Booth(area, config, false); break;
                case SliceArea.BoothDay2: VhWakingWard.Booth(area, config, true); break;
                default: Street(area, config); break;
            }
            return Done();
        }

        static System.Collections.IEnumerator Done()
        {
            yield break;
        }

        static void AddSpot(VhArea area, VisitingHoursConfig config, string id, Vector3 at, float radius, JamKit.Doomlike.Billboard billboard = null)
        {
            var def = config.FindExaminable(id);
            if (def == null) return;
            area.spots.Add(new VhSpot { def = def, position = at, radius = radius, billboard = billboard });
        }

        // ---------- St. Brigid's: the corridor, the nurses' station, room 14 ----------

        static void Hospital(VhArea a, VisitingHoursConfig c)
        {
            var world = c.worldMaterial;
            var painted = c.paintedMaterial;
            var floorTex = c.Art("tex_hospital_floor");
            var wallTex = c.Art("tex_hospital_wall");
            Color tint = c.hospitalTint;
            Color dark = tint * 0.62f;
            dark.a = 1f;
            const float h = 3f;
            var r = a.root;

            VhWorldKit.Floor("Floor", r, world, floorTex, tint, -5f, 0f, 9f, 26f, 0f, 2f);
            VhWorldKit.Floor("Ceiling", r, world, wallTex, tint * 0.9f, -5f, 0f, 9f, 26f, h, 3f, false, true);
            for (float z = 2f; z < 26f; z += 4f)
                VhWorldKit.Floor("Light", r, world, Texture2D.whiteTexture, new Color(1f, 1f, 0.94f), -0.5f, z - 0.9f, 0.5f, z + 0.9f, h - 0.02f, 1f, false, true);

            // Corridor, with the nurses' station alcove on the left and room 14 on the right.
            Wall(r, world, wallTex, tint, -2f, 0f, -2f, 3f, h);
            Wall(r, world, wallTex, tint, -2f, 8f, -2f, 26f, h);
            Wall(r, world, wallTex, tint, -5f, 3f, -5f, 8f, h);
            Wall(r, world, wallTex, tint, -5f, 3f, -2f, 3f, h);
            Wall(r, world, wallTex, tint, -5f, 8f, -2f, 8f, h);
            Wall(r, world, wallTex, tint, 2f, 0f, 2f, 18f, h);
            Wall(r, world, wallTex, tint, 2f, 20f, 2f, 26f, h);
            Wall(r, world, wallTex, tint, -2f, 0f, 2f, 0f, h);
            Wall(r, world, wallTex, tint, -2f, 26f, 2f, 26f, h);
            Wall(r, world, wallTex, tint, 9f, 16f, 9f, 24f, h);
            Wall(r, world, wallTex, tint, 2f, 16f, 9f, 16f, h);
            Wall(r, world, wallTex, tint, 2f, 24f, 9f, 24f, h);
            // Above room 14's doorway.
            VhWorldKit.Wall("Lintel", r, world, wallTex, tint, new Vector3(2f, 2.2f, 18f), new Vector3(2f, 2.2f, 20f), h - 2.2f, 2f, false);

            // The nurses' station: a counter, the envelope for the family on it.
            VhWorldKit.Block("Station desk", r, world, wallTex, dark, new Vector3(-3.7f, 0.55f, 5.5f), new Vector3(1f, 1.1f, 3f), 1.5f);
            var envelope = VhWorldKit.Person("Envelope", a, painted, c.Art("prop_police_envelope"), tint, new Vector3(-3.5f, 1.1f, 4.9f), 0.32f);
            AddSpot(a, c, "police_report", new Vector3(-3.5f, 1.2f, 4.9f), 0.5f, envelope);
            AddSpot(a, c, "station", new Vector3(-3.7f, 1f, 6.4f), 0.6f);

            // Room 14.
            var bed = VhWorldKit.Person("Elias", a, painted, c.Art("billboard_elias_bed"), tint, new Vector3(6.4f, 0f, 21.6f), 1.5f);
            AddSpot(a, c, "elias", new Vector3(6.4f, 0.9f, 21.6f), 1.1f, bed);
            var chart = VhWorldKit.Person("Chart", a, painted, c.Art("prop_chart"), tint, new Vector3(5.2f, 0.62f, 19.9f), 0.5f);
            AddSpot(a, c, "chart", new Vector3(5.2f, 0.9f, 19.9f), 0.5f, chart);
            VhWorldKit.Block("Chair", r, world, wallTex, dark, new Vector3(8.2f, 0.25f, 17.3f), new Vector3(0.6f, 0.5f, 0.6f));
            var bag = VhWorldKit.Person("Effects bag", a, painted, c.Art("prop_effects_bag"), tint, new Vector3(8.2f, 0.5f, 17.3f), 0.55f);
            AddSpot(a, c, "effects", new Vector3(8.2f, 0.75f, 17.3f), 0.5f, bag);
            VhWorldKit.Block("Bedside table", r, world, wallTex, dark, new Vector3(8.3f, 0.35f, 22.7f), new Vector3(0.6f, 0.7f, 0.6f));
            var photo = VhWorldKit.Person("Family photo", a, painted, c.Art("prop_family_photo"), tint, new Vector3(8.3f, 0.7f, 22.7f), 0.32f);
            AddSpot(a, c, "photo", new Vector3(8.3f, 0.85f, 22.7f), 0.45f, photo);
            a.monitor = new Vector3(7.8f, 1.4f, 23f);

            // Stairwell B, at the end of the corridor.
            VhWorldKit.Wall("Stairwell door", r, world, wallTex, new Color(0.42f, 0.5f, 0.56f), new Vector3(-0.7f, 0f, 25.95f), new Vector3(0.7f, 0f, 25.95f), 2.2f, 1.4f, false);
            AddSpot(a, c, "stairwell", new Vector3(0f, 1.1f, 25.8f), 0.8f);

            a.spawn = new Vector3(0f, 0.05f, 1.5f);
            a.spawnYaw = 0f;
        }

        static void Wall(Transform r, Material m, Texture t, Color tint, float x0, float z0, float x1, float z1, float h) =>
            VhWorldKit.Wall("Wall", r, m, t, tint, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z1), h, 2f);

        // ---------- 11 PM: the booth ----------

        static void Booth(VhArea a, VisitingHoursConfig c)
        {
            var world = c.worldMaterial;
            var wallTex = c.Art("tex_hospital_wall");
            Color tint = c.boothTint;
            const float h = 2.8f;
            var r = a.root;
            VhWorldKit.Floor("Floor", r, world, c.Art("tex_hospital_floor"), tint * 0.8f, -3f, 0f, 3f, 6f, 0f, 2f);
            VhWorldKit.Floor("Ceiling", r, world, wallTex, tint * 0.5f, -3f, 0f, 3f, 6f, h, 3f, false, true);
            Wall(r, world, wallTex, tint, -3f, 0f, -3f, 6f, h);
            Wall(r, world, wallTex, tint, 3f, 0f, 3f, 6f, h);
            Wall(r, world, wallTex, tint, -3f, 0f, 3f, 0f, h);
            Wall(r, world, wallTex, tint, -3f, 6f, 3f, 6f, h);
            // A night window, and the observation glass onto the dark lab where the chair is.
            VhWorldKit.Wall("Window", r, c.skyMaterial, c.Art("sky_night"), Color.white, new Vector3(-1.6f, 1.05f, 5.95f), new Vector3(1.6f, 1.05f, 5.95f), 1.3f, 3.2f, false);
            VhWorldKit.Wall("Observation glass", r, world, Texture2D.whiteTexture, new Color(0.16f, 0.2f, 0.24f), new Vector3(2.95f, 0.9f, 1.4f), new Vector3(2.95f, 0.9f, 4.6f), 1.4f, 3.2f, false);
            VhWorldKit.Block("Desk", r, world, wallTex, tint * 0.55f, new Vector3(2.4f, 0.45f, 3f), new Vector3(0.9f, 0.9f, 2.4f), 1.2f);
            VhWorldKit.Floor("Desk lamp", r, world, Texture2D.whiteTexture, new Color(1f, 0.86f, 0.6f), 2.2f, 2.6f, 2.6f, 3f, 0.92f, 1f, false);
            a.spawn = new Vector3(-0.8f, 0.05f, 1.2f);
            a.spawnYaw = 45f;
        }

        // ---------- Night 1: Walt's street ----------

        static void Street(VhArea a, VisitingHoursConfig c)
        {
            var world = c.worldMaterial;
            var painted = c.paintedMaterial;
            Color tint = c.dreamTint;
            var r = a.root;

            VhWorldKit.SkyDrum("Sky", r, c.skyMaterial, c.Art("sky_dream"), Color.white, Vector3.zero, 72f, 48f, -3f);
            VhWorldKit.Floor("Road", r, world, c.Art("tex_road"), tint, -48f, -3.5f, 48f, 3.5f, 0f, 7f);
            VhWorldKit.Floor("Sidewalk N", r, world, c.Art("tex_sidewalk"), tint, -48f, 3.5f, 48f, 5.5f, 0.02f, 2f);
            VhWorldKit.Floor("Sidewalk S", r, world, c.Art("tex_sidewalk"), tint, -48f, -5.5f, 48f, -3.5f, 0.02f, 2f);
            VhWorldKit.Floor("Lawns N", r, world, c.Art("tex_grass"), tint, -48f, 5.5f, 48f, 26f, 0.01f, 4f);
            VhWorldKit.Floor("Lawns S", r, world, c.Art("tex_grass"), tint, -48f, -26f, 48f, -5.5f, 0.01f, 4f);
            VhWorldKit.Barrier("Edge W", r, new Vector3(-44f, 0f, -24f), new Vector3(-44f, 0f, 24f));
            VhWorldKit.Barrier("Edge E", r, new Vector3(44f, 0f, -24f), new Vector3(44f, 0f, 24f));
            VhWorldKit.Barrier("Edge N", r, new Vector3(-44f, 0f, 22f), new Vector3(44f, 0f, 22f));
            VhWorldKit.Barrier("Edge S", r, new Vector3(-44f, 0f, -22f), new Vector3(44f, 0f, -22f));

            // The houses: painted flats, like a stage set. North side faces the road (-Z), south side faces it (+Z).
            House(r, c, "flat_house_1", new Vector3(-32f, 0f, 20f), 0f, tint);
            House(r, c, "flat_house_2", new Vector3(-14f, 0f, 20f), 0f, tint);
            House(r, c, "flat_house_3", new Vector3(4f, 0f, 20f), 0f, tint);
            House(r, c, "flat_house_3", new Vector3(-26f, 0f, -20f), 180f, tint);
            House(r, c, "flat_house_1", new Vector3(-8f, 0f, -20f), 180f, tint);
            House(r, c, "flat_house_2", new Vector3(12f, 0f, -20f), 180f, tint);
            House(r, c, "flat_house_1", new Vector3(32f, 0f, -20f), 180f, tint);
            var garage = VhWorldKit.Flat("Abernathy Cycles", r, painted, c.Art("flat_bike_garage"), tint, new Vector3(25f, 0f, 15f), 11f, 0f, true);
            AddSpot(a, c, "shop_sign", new Vector3(25f, 2f, 15f), 4.5f, garage);

            // The barbecue in front of the garage: Walt waving, the neighbour at the grill.
            VhWorldKit.Block("Grill", r, world, Texture2D.whiteTexture, new Color(0.22f, 0.2f, 0.2f), new Vector3(24f, 0.45f, 9.6f), new Vector3(0.9f, 0.9f, 0.6f));
            a.walt = VhWorldKit.Person("Walt", a, painted, c.Art("billboard_walt"), tint, new Vector3(22.2f, 0f, 9.8f), 1.86f);
            AddSpot(a, c, "walt", new Vector3(22.2f, 1.2f, 9.8f), 0.6f, a.walt);
            var grill = VhWorldKit.Person("Neighbour at the grill", a, painted, c.Art("billboard_neighbor_grill"), tint, new Vector3(25.6f, 0f, 9.5f), 1.8f);
            AddSpot(a, c, "grill", new Vector3(25.6f, 1.2f, 9.5f), 0.6f, grill);

            var roses = VhWorldKit.Person("Neighbour watering roses", a, painted, c.Art("billboard_neighbor_roses"), tint, new Vector3(-14f, 0f, 12f), 1.7f);
            AddSpot(a, c, "roses", new Vector3(-14f, 1.1f, 12f), 0.6f, roses);

            a.dogHome = new Vector3(-2f, 0f, -9f);
            a.dog = VhWorldKit.Person("Dog", a, painted, c.Art("billboard_dog"), tint, a.dogHome, 0.75f);
            AddSpot(a, c, "dog", a.dogHome + Vector3.up * 0.4f, 0.6f, a.dog);

            // The Sunday paper, still in its plastic on a porch.
            var paper = VhWorldKit.Lying("Newspaper", r, painted, c.Art("prop_newspaper"), tint, new Vector3(-8f, 0.03f, -15.2f), 0.6f, 0f);
            AddSpot(a, c, "newspaper", new Vector3(-8f, 0.1f, -15.2f), 0.9f, paper);

            a.spawn = new Vector3(-38f, 0.05f, 0f);
            a.spawnYaw = 90f;
        }

        static void House(Transform r, VisitingHoursConfig c, string art, Vector3 feet, float yaw, Color tint) =>
            VhWorldKit.Flat(art, r, c.paintedMaterial, c.Art(art), tint, feet, 13f, yaw, true);
    }
}
