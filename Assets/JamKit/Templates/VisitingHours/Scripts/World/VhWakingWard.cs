using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// St. Brigid's and the 11 PM booth in style E, the waking world (like the opening's bedroom: grounded, muted,
    /// stylised but adult, real light; the creator: "the rooms should look real... no greybox look"). The corridor with
    /// its suspended ceiling, handrails, doors with vision panels and kick plates, sanitiser by every door, the
    /// noticeboard, a linen trolley and a parked wheelchair; the nurses' station (counter and ledge, monitors and
    /// keyboards, the desk phone, the patient whiteboard, binders, the clock, the police envelope, her phone face down);
    /// room 14 (a hospital bed with rails, Elias in a gown under the blanket with a cannula, the headwall with its
    /// outlets, the patient monitor on its arm, the drip and pump, the curtain track, the over-bed table, the locker and
    /// the photo, the sink by the door, the window with its blinds and LeBaget's lilies, the radiator, the sharps bin);
    /// Rafael; on Day 2 the visitor from LeBaget and her chair by the bed; and the booth in the closed wing (the chair
    /// and its rig, the console, the desk lamp, the curtained bed, the closed wing's boxes, Mags). Kenney's furniture
    /// (restyled, as the bedroom does) where the kit has the piece, the rest built here. The layout is the slice's
    /// (the corridor along +Z, the station on the left, room 14 on the right, stairwell B at the end).
    /// People are style-E stand-ins until they have models. docs/NIGHTS-1-2.md, Day 1 and Day 2.
    /// </summary>
    public static class VhWakingWard
    {
        // The ward's palette (muted, slightly cool: the bedroom's register in a hospital).
        static readonly Color WallPaint = new Color(0.70f, 0.73f, 0.70f);
        static readonly Color WallRoom = new Color(0.74f, 0.72f, 0.66f);
        static readonly Color Skirting = new Color(0.36f, 0.40f, 0.42f);
        static readonly Color Vinyl = new Color(0.60f, 0.62f, 0.62f);
        static readonly Color VinylRoom = new Color(0.56f, 0.55f, 0.52f);
        static readonly Color CeilingTile = new Color(0.80f, 0.80f, 0.78f);
        static readonly Color CeilingGrid = new Color(0.66f, 0.66f, 0.64f);
        static readonly Color DoorBlue = new Color(0.42f, 0.50f, 0.56f);
        static readonly Color DoorFrame = new Color(0.78f, 0.78f, 0.76f);
        static readonly Color Glass = new Color(0.10f, 0.13f, 0.16f);
        static readonly Color Laminate = new Color(0.64f, 0.56f, 0.46f);
        static readonly Color Worktop = new Color(0.80f, 0.79f, 0.75f);
        static readonly Color Plastic = new Color(0.86f, 0.86f, 0.84f);
        static readonly Color PlasticGrey = new Color(0.55f, 0.57f, 0.59f);
        static readonly Color BedFrame = new Color(0.80f, 0.79f, 0.75f);
        static readonly Color HospitalBlanket = new Color(0.56f, 0.64f, 0.72f);
        static readonly Color Gown = new Color(0.66f, 0.74f, 0.80f);
        static readonly Color TealScrubs = new Color(0.24f, 0.43f, 0.45f);
        static readonly Color Suit = new Color(0.25f, 0.26f, 0.29f);
        static readonly Color Shirt = new Color(0.86f, 0.87f, 0.88f);
        static readonly Color Tie = new Color(0.42f, 0.18f, 0.20f);
        static readonly Color Cardigan = new Color(0.54f, 0.44f, 0.32f);
        static readonly Color GreyHair = new Color(0.66f, 0.64f, 0.62f);
        static readonly Color DarkHair = new Color(0.17f, 0.13f, 0.11f);
        static readonly Color Shoe = new Color(0.16f, 0.16f, 0.17f);
        static readonly Color Lily = new Color(0.94f, 0.93f, 0.88f);
        static readonly Color Stem = new Color(0.30f, 0.45f, 0.30f);
        static readonly Color SharpsYellow = new Color(0.86f, 0.74f, 0.22f);
        static readonly Color ExtinguisherRed = new Color(0.68f, 0.14f, 0.12f);
        static readonly Color Cork = new Color(0.62f, 0.48f, 0.34f);
        static readonly Color LinenBlue = new Color(0.32f, 0.44f, 0.62f);
        static readonly Color ChairBlue = new Color(0.34f, 0.42f, 0.50f);

        sealed class Kit
        {
            public VhWakingStyle style;
            public VhMeshKit meshes;
            public Transform root;
            public VisitingHoursConfig config;
            public VhArea area;
            public VisitingHoursBoot boot;
        }

        /// <summary>Frees the place's materials and meshes when it's taken down.</summary>
        sealed class Owned : MonoBehaviour
        {
            public VhWakingStyle style;
            public VhMeshKit meshes;

            void OnDestroy()
            {
                style?.Dispose();
                meshes?.Dispose();
            }
        }

        static Kit Begin(VhArea a, VisitingHoursConfig c)
        {
            var boot = VhBoot.Config;
            var k = new Kit { style = new VhWakingStyle(boot != null ? boot.wakingMaterial : null), meshes = new VhMeshKit(), root = a.root, config = c, area = a, boot = boot };
            var owned = a.root.gameObject.AddComponent<Owned>();
            owned.style = k.style;
            owned.meshes = k.meshes;
            return k;
        }

        // ---------- Building blocks ----------

        static MeshRenderer Box(Kit k, string name, Vector3 center, Vector3 size, Color color, bool solid = false, float round = 0f, Quaternion? rot = null, Texture tex = null, float uv = 1f)
        {
            var mesh = round > 0f ? k.meshes.RoundedBox(size, round, 3) : k.meshes.Box(size, uv);
            var r = VhMeshKit.Show(k.root, name, mesh, k.style.Mat(color, tex), center, rot ?? Quaternion.identity);
            if (solid) r.gameObject.AddComponent<BoxCollider>().size = size;
            return r;
        }

        static MeshRenderer Part(Kit k, Transform parent, string name, Mesh mesh, Color color, Vector3 at, Quaternion rot, bool shadows = true) =>
            VhMeshKit.Show(parent, name, mesh, k.style.Mat(color), at, rot, shadows);

        static MeshRenderer Glow(Kit k, string name, Mesh mesh, Color body, Color emission, Vector3 at, Quaternion rot, Transform parent = null)
        {
            var m = k.style.Unique(body, null, "VH " + name);
            VhWakingStyle.SetEmission(m, emission);
            return VhMeshKit.Show(parent != null ? parent : k.root, name, mesh, m, at, rot, false);
        }

        static Quaternion Facing(Vector3 dir) => Quaternion.LookRotation(dir, Vector3.up);

        /// <summary>A wall from (x0,z0) to (x1,z1): the paint, and the vinyl coving at the foot (hospital floors turn up the wall).</summary>
        static void Wall(Kit k, float x0, float z0, float x1, float z1, float h, Color paint)
        {
            var a = new Vector3(x0, 0f, z0);
            var b = new Vector3(x1, 0f, z1);
            float len = Vector3.Distance(a, b);
            var mid = (a + b) * 0.5f;
            var rot = Facing((b - a).normalized);
            Box(k, "Wall", mid + Vector3.up * (h * 0.5f), new Vector3(0.12f, h, len), paint, true, 0f, rot);
            Box(k, "Coving", mid + Vector3.up * 0.06f, new Vector3(0.15f, 0.12f, len + 0.02f), Skirting, false, 0f, rot);
        }

        /// <summary>A handrail along a wall (on brackets, a hand off the wall), as hospital corridors have.</summary>
        static void Handrail(Kit k, Vector3 from, Vector3 to, Vector3 off)
        {
            var path = new List<Vector3> { from + off + Vector3.up * 0.9f, to + off + Vector3.up * 0.9f };
            VhMeshKit.Show(k.root, "Handrail", k.meshes.Tube(path, 0.025f, 10), k.style.Mat(Laminate), Vector3.zero, Quaternion.identity);
            float len = Vector3.Distance(from, to);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 1.5f) + 1);
            var size = new Vector3(Mathf.Abs(off.x) > 0f ? Mathf.Abs(off.x) : 0.03f, 0.04f, Mathf.Abs(off.z) > 0f ? Mathf.Abs(off.z) : 0.03f);
            for (int i = 0; i < n; i++)
                Box(k, "Rail bracket", Vector3.Lerp(from, to, i / (float)(n - 1)) + Vector3.up * 0.87f + off * 0.5f, size, PlasticGrey);
        }

        /// <summary>A shut door in a wall (<paramref name="center"/> on the wall's face at the floor, <paramref name="normal"/>
        /// out of the wall towards the viewer): the frame, the leaf, a vision panel, the lever, a kick plate, a number
        /// plate, and hand sanitiser beside it.</summary>
        static void Door(Kit k, Vector3 center, Vector3 normal, float width, Color leaf, string number = null)
        {
            var right = Vector3.Cross(Vector3.up, -normal).normalized;
            var g = new GameObject("Door" + (number != null ? " " + number : "")).transform;
            g.SetParent(k.root, false);
            g.localPosition = center + normal * 0.065f;
            g.localRotation = Facing(-normal);
            float w = width, h = 2.1f;
            Part(k, g, "Frame (left)", k.meshes.Box(new Vector3(0.06f, h + 0.06f, 0.04f)), DoorFrame, new Vector3(-w * 0.5f - 0.03f, (h + 0.06f) * 0.5f, 0f), Quaternion.identity);
            Part(k, g, "Frame (right)", k.meshes.Box(new Vector3(0.06f, h + 0.06f, 0.04f)), DoorFrame, new Vector3(w * 0.5f + 0.03f, (h + 0.06f) * 0.5f, 0f), Quaternion.identity);
            Part(k, g, "Frame (top)", k.meshes.Box(new Vector3(w + 0.12f, 0.06f, 0.04f)), DoorFrame, new Vector3(0f, h + 0.03f, 0f), Quaternion.identity);
            Part(k, g, "Leaf", k.meshes.Box(new Vector3(w, h, 0.03f)), leaf, new Vector3(0f, h * 0.5f, 0.005f), Quaternion.identity);
            Part(k, g, "Vision panel", k.meshes.Box(new Vector3(0.16f, 0.6f, 0.035f)), Glass, new Vector3(w * 0.22f, 1.45f, 0f), Quaternion.identity, false);
            Part(k, g, "Kick plate", k.meshes.Box(new Vector3(w - 0.04f, 0.22f, 0.036f)), VhWakingStyle.Metal, new Vector3(0f, 0.13f, 0f), Quaternion.identity, false);
            Part(k, g, "Lever", k.meshes.Box(new Vector3(0.13f, 0.022f, 0.022f)), VhWakingStyle.Metal, new Vector3(-w * 0.5f + 0.13f, 1.02f, -0.045f), Quaternion.identity, false);
            Part(k, g, "Lever rose", k.meshes.Cylinder(0.025f, 0.025f, 0.012f, 12), VhWakingStyle.Metal, new Vector3(-w * 0.5f + 0.08f, 1.02f, -0.024f), Quaternion.Euler(90f, 0f, 0f), false);
            if (number != null)
            {
                Part(k, g, "Number plate", k.meshes.Box(new Vector3(0.2f, 0.12f, 0.015f)), Plastic, new Vector3(w * 0.5f + 0.24f, 1.6f, -0.02f), Quaternion.identity, false);
                Part(k, g, "Number", k.meshes.Box(new Vector3(0.09f, 0.05f, 0.017f)), Skirting, new Vector3(w * 0.5f + 0.24f, 1.6f, -0.024f), Quaternion.identity, false);
            }
            Sanitiser(k, center + right * (w * 0.5f + 0.3f) + Vector3.up * 1.25f, normal);
        }

        static void Sanitiser(Kit k, Vector3 wallPoint, Vector3 normal)
        {
            var rot = Facing(-normal);
            Box(k, "Hand sanitiser", wallPoint + normal * 0.11f, new Vector3(0.12f, 0.24f, 0.09f), Plastic, false, 0.02f, rot);
            Box(k, "Sanitiser window", wallPoint + normal * 0.157f + Vector3.up * 0.04f, new Vector3(0.05f, 0.08f, 0.004f), new Color(0.6f, 0.75f, 0.8f), false, 0f, rot);
            Box(k, "Sanitiser lever", wallPoint + normal * 0.13f - Vector3.up * 0.1f, new Vector3(0.09f, 0.03f, 0.06f), PlasticGrey, false, 0.01f, rot);
        }

        static void AddLight(Kit k, string name, Vector3 at, Color color, float intensity, float range, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(k.root, false);
            go.transform.localPosition = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        /// <summary>A recessed ceiling light: the diffuser glowing, and the light it gives.</summary>
        static void CeilingLight(Kit k, Vector3 at, Vector2 size, Color light, float intensity, float range, bool shadows, float glow = 1.1f)
        {
            Box(k, "Light housing", at + Vector3.up * 0.01f, new Vector3(size.x + 0.06f, 0.03f, size.y + 0.06f), CeilingGrid);
            Glow(k, "Light diffuser", k.meshes.Box(new Vector3(size.x, 0.02f, size.y)), new Color(0.9f, 0.92f, 0.9f), new Color(0.96f, 0.97f, 0.92f) * glow, at - Vector3.up * 0.005f, Quaternion.identity);
            AddLight(k, "Ceiling light", at - Vector3.up * 0.35f, light, intensity, range, shadows);
        }

        /// <summary>A suspended ceiling: the tiles, and the grid between them.</summary>
        static void SuspendedCeiling(Kit k, float x0, float x1, float z0, float z1, float y)
        {
            Box(k, "Ceiling tiles", new Vector3((x0 + x1) * 0.5f, y + 0.02f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.04f, z1 - z0), CeilingTile);
            const float step = 1.2f;
            for (float x = x0 + step * 0.5f; x < x1 - 0.1f; x += step * 0.5f)
                Box(k, "Ceiling grid", new Vector3(x, y - 0.005f, (z0 + z1) * 0.5f), new Vector3(0.025f, 0.012f, z1 - z0), CeilingGrid);
            for (float z = z0 + step; z < z1 - 0.1f; z += step)
                Box(k, "Ceiling grid", new Vector3((x0 + x1) * 0.5f, y - 0.005f, z), new Vector3(x1 - x0, 0.012f, 0.025f), CeilingGrid);
        }

        static void Spot(Kit k, string id, Vector3 at, float radius)
        {
            var def = k.config.FindExaminable(id);
            if (def == null) return;
            k.area.spots.Add(new VhSpot { def = def, position = at, radius = radius });
        }

        /// <summary>
        /// One of the boot asset's models (Kenney's furniture), restyled into the palette as the bedroom does: scaled so its
        /// height (or with <paramref name="byWidth"/> its widest side) is <paramref name="size"/>, standing on
        /// <paramref name="floor"/>, its front (+Z in the kit) turned <paramref name="yaw"/> degrees. A box collider round it
        /// when <paramref name="solid"/>. Null when the model isn't in the build (the caller builds its own).
        /// </summary>
        static GameObject Model(Kit k, string name, Vector3 floor, float yaw, float size, bool byWidth = false, bool solid = true)
        {
            var prefab = k.boot != null ? k.boot.Model(name) : null;
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, k.root);
            go.name = name;
            foreach (var an in go.GetComponentsInChildren<Animator>(true)) an.enabled = false;
            k.style.Restyle(go);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var b = Bounds(go);
            float have = byWidth ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
            if (have > 1e-4f) go.transform.localScale = Vector3.one * (size / have);
            b = Bounds(go);
            var target = k.root.TransformPoint(floor);
            go.transform.position += target - new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.RotateAround(target, Vector3.up, yaw);
            if (solid)
            {
                b = Bounds(go);
                var col = new GameObject(name + " (collider)");
                col.transform.SetParent(k.root, false);
                col.transform.position = b.center;
                col.AddComponent<BoxCollider>().size = b.size;
            }
            return go;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>A wall clock: the rim, the face, the ticks, the hands at <paramref name="hours"/> (12-hour, decimal).</summary>
        static void Clock(Kit k, Vector3 wallPoint, Vector3 normal, float hours)
        {
            var g = new GameObject("Wall clock").transform;
            g.SetParent(k.root, false);
            g.localPosition = wallPoint + normal * 0.03f;
            g.localRotation = Facing(-normal);
            // In the clock's space +Z goes into the wall and the face looks down -Z; the viewer's right is the clock's -X.
            Part(k, g, "Rim", k.meshes.Cylinder(0.16f, 0.16f, 0.04f, 32), new Color(0.2f, 0.21f, 0.23f), new Vector3(0f, 0f, -0.02f), Quaternion.Euler(90f, 0f, 0f));
            Part(k, g, "Face", k.meshes.Disc(0.145f, 32), new Color(0.94f, 0.93f, 0.9f), new Vector3(0f, 0f, -0.021f), Quaternion.Euler(-90f, 0f, 0f), false);
            for (int i = 0; i < 12; i++)
            {
                var r = Quaternion.Euler(0f, 0f, i * 30f);
                Part(k, g, "Tick", k.meshes.Box(new Vector3(0.008f, 0.022f, 0.004f)), Shoe, r * new Vector3(0f, 0.125f, 0f) + new Vector3(0f, 0f, -0.024f), r, false);
            }
            float hourA = (hours % 12f) * 30f, minA = (hours * 60f % 60f) * 6f;
            var hr = Quaternion.Euler(0f, 0f, hourA);
            var mr = Quaternion.Euler(0f, 0f, minA);
            Part(k, g, "Hour hand", k.meshes.Box(new Vector3(0.012f, 0.075f, 0.004f)), Shoe, hr * new Vector3(0f, 0.032f, 0f) + new Vector3(0f, 0f, -0.028f), hr, false);
            Part(k, g, "Minute hand", k.meshes.Box(new Vector3(0.008f, 0.11f, 0.004f)), Shoe, mr * new Vector3(0f, 0.05f, 0f) + new Vector3(0f, 0f, -0.031f), mr, false);
        }

        /// <summary>A person stand-in in style E (until they have models): shoes, legs, hips, a torso in their clothes,
        /// arms and hands, a neck, a head with a nose, and hair.</summary>
        static Transform Person(Kit k, string name, Vector3 feet, float yaw, float height, Color clothes, Color legs, Color hair, bool bun = false, Color? collar = null)
        {
            var p = new GameObject(name).transform;
            p.SetParent(k.root, false);
            p.localPosition = feet;
            p.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float s = height / 1.7f;
            void Bit(string n, Mesh m, Color c, Vector3 at, Quaternion r) => Part(k, p, n, m, c, at * s, r);
            foreach (float x in new[] { -0.095f, 0.095f })
            {
                Bit("Shoe", k.meshes.RoundedBox(new Vector3(0.1f, 0.07f, 0.25f) * s, 0.03f * s, 3), Shoe, new Vector3(x, 0.035f, 0.04f), Quaternion.identity);
                Bit("Leg", k.meshes.Cylinder(0.068f * s, 0.058f * s, 0.78f * s, 12), legs, new Vector3(x, 0.06f, 0f), Quaternion.identity);
            }
            Bit("Hips", k.meshes.RoundedBox(new Vector3(0.36f, 0.2f, 0.22f) * s, 0.07f * s, 4), legs, new Vector3(0f, 0.86f, 0f), Quaternion.identity);
            Bit("Torso", k.meshes.RoundedBox(new Vector3(0.40f, 0.56f, 0.24f) * s, 0.1f * s, 5, 0.01f * s), clothes, new Vector3(0f, 1.18f, 0f), Quaternion.identity);
            if (collar.HasValue) Bit("Collar", k.meshes.RoundedBox(new Vector3(0.12f, 0.16f, 0.02f) * s, 0.01f * s, 2), collar.Value, new Vector3(0f, 1.36f, 0.115f), Quaternion.identity);
            foreach (float x in new[] { -0.245f, 0.245f })
            {
                Bit("Arm", k.meshes.Cylinder(0.05f * s, 0.055f * s, 0.56f * s, 10), clothes, new Vector3(x, 0.88f, 0f), Quaternion.Euler(0f, 0f, x < 0f ? -5f : 5f));
                Bit("Hand", k.meshes.RoundedBox(new Vector3(0.07f, 0.11f, 0.04f) * s, 0.02f * s, 3), VhWakingStyle.Skin, new Vector3(x * 1.05f, 0.83f, 0f), Quaternion.identity);
            }
            Bit("Neck", k.meshes.Cylinder(0.05f * s, 0.05f * s, 0.1f * s, 10), VhWakingStyle.Skin, new Vector3(0f, 1.44f, 0f), Quaternion.identity);
            Bit("Head", k.meshes.RoundedBox(new Vector3(0.18f, 0.23f, 0.21f) * s, 0.085f * s, 5, 0.01f * s), VhWakingStyle.Skin, new Vector3(0f, 1.6f, 0.005f), Quaternion.identity);
            Bit("Nose", k.meshes.RoundedBox(new Vector3(0.03f, 0.05f, 0.04f) * s, 0.012f * s, 2), VhWakingStyle.Skin * 0.96f, new Vector3(0f, 1.59f, 0.115f), Quaternion.identity);
            Bit("Hair", k.meshes.RoundedBox(new Vector3(0.2f, 0.12f, 0.225f) * s, 0.06f * s, 4), hair, new Vector3(0f, 1.69f, -0.008f), Quaternion.identity);
            Bit("Hair (back)", k.meshes.RoundedBox(new Vector3(0.19f, 0.16f, 0.06f) * s, 0.03f * s, 3), hair, new Vector3(0f, 1.6f, -0.095f), Quaternion.identity);
            if (bun) Bit("Bun", k.meshes.RoundedBox(new Vector3(0.09f, 0.09f, 0.09f) * s, 0.04f * s, 3), hair, new Vector3(0f, 1.66f, -0.14f), Quaternion.identity);
            var col = p.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.85f * s, 0f);
            col.height = 1.7f * s;
            col.radius = 0.25f * s;
            return p;
        }

        // ---------- St. Brigid's: the corridor, the station, room 14 ----------

        /// <summary>The ward in the evening. <paramref name="day2"/>: the second day's people and things (Rafael's
        /// question, the visitor from LeBaget, the round, her chair by the bed).</summary>
        public static void Hospital(VhArea a, VisitingHoursConfig c, bool day2)
        {
            var k = Begin(a, c);
            const float h = 2.7f;
            var floorTex = c.Art("tex_hospital_floor");

            // Floors (the corridor's sheet vinyl, the room's warmer one), the suspended ceilings and their lights.
            Box(k, "Floor (corridor)", new Vector3(0f, -0.05f, 13f), new Vector3(4f, 0.1f, 26f), Vinyl, true, 0f, null, floorTex, 0.5f);
            Box(k, "Floor (station)", new Vector3(-3.5f, -0.05f, 5.5f), new Vector3(3f, 0.1f, 5f), Vinyl, true, 0f, null, floorTex, 0.5f);
            Box(k, "Floor (room 14)", new Vector3(5.5f, -0.05f, 20f), new Vector3(7f, 0.1f, 8f), VinylRoom, true, 0f, null, floorTex, 0.5f);
            SuspendedCeiling(k, -2f, 2f, 0f, 26f, h);
            SuspendedCeiling(k, -5f, -2f, 3f, 8f, h);
            SuspendedCeiling(k, 2f, 9f, 16f, 24f, h);
            for (float z = 2.4f; z < 26f; z += 4.8f)
                CeilingLight(k, new Vector3(0f, h - 0.01f, z), new Vector2(0.6f, 1.2f), new Color(0.94f, 0.97f, 1f), 0.9f, 6.5f, z > 14f && z < 22f);
            CeilingLight(k, new Vector3(-3.6f, h - 0.01f, 5.5f), new Vector2(0.6f, 1.2f), new Color(1f, 0.95f, 0.86f), 0.8f, 5f, true);

            // The corridor, the station's alcove on the left, room 14 on the right, stairwell B at the end.
            Wall(k, -2f, 0f, -2f, 3f, h, WallPaint);
            Wall(k, -2f, 8f, -2f, 26f, h, WallPaint);
            Wall(k, -5f, 3f, -5f, 8f, h, WallPaint);
            Wall(k, -5f, 3f, -2f, 3f, h, WallPaint);
            Wall(k, -5f, 8f, -2f, 8f, h, WallPaint);
            Wall(k, 2f, 0f, 2f, 18f, h, WallPaint);
            Wall(k, 2f, 20f, 2f, 26f, h, WallPaint);
            Wall(k, -2f, 0f, 2f, 0f, h, WallPaint);
            Wall(k, -2f, 26f, 2f, 26f, h, WallPaint);
            Wall(k, 9f, 16f, 9f, 24f, h, WallRoom);
            Wall(k, 2.06f, 16f, 9f, 16f, h, WallRoom);
            Wall(k, 2.06f, 24f, 9f, 24f, h, WallRoom);
            Box(k, "Lintel", new Vector3(2f, (h + 2.16f) * 0.5f, 19f), new Vector3(0.12f, h - 2.16f, 2f), WallPaint);
            Box(k, "Door frame (room 14)", new Vector3(2f, 1.08f, 18.03f), new Vector3(0.16f, 2.16f, 0.06f), DoorFrame);
            Box(k, "Door frame (room 14)", new Vector3(2f, 1.08f, 19.97f), new Vector3(0.16f, 2.16f, 0.06f), DoorFrame);
            Box(k, "Door frame (room 14, top)", new Vector3(2f, 2.19f, 19f), new Vector3(0.16f, 0.06f, 2f), DoorFrame);
            // Room 14's door, open into the room against its wall; the plate on the corridor side.
            Box(k, "Door (room 14, open)", new Vector3(2.55f, 1.05f, 18.1f), new Vector3(0.95f, 2.1f, 0.04f), DoorBlue, true);
            Box(k, "Room 14 plate", new Vector3(1.925f, 1.6f, 17.55f), new Vector3(0.015f, 0.12f, 0.22f), Plastic);
            Box(k, "Room 14 plate (number)", new Vector3(1.915f, 1.6f, 17.55f), new Vector3(0.015f, 0.05f, 0.1f), Skirting);
            Sanitiser(k, new Vector3(1.94f, 1.25f, 20.5f), Vector3.left);

            // Other rooms' doors along the corridor, shut, and handrails between them.
            Door(k, new Vector3(1.94f, 0f, 4f), Vector3.left, 1.1f, DoorBlue, "11");
            Door(k, new Vector3(1.94f, 0f, 9.5f), Vector3.left, 1.1f, DoorBlue, "12");
            Door(k, new Vector3(1.94f, 0f, 14f), Vector3.left, 1.1f, DoorBlue, "13");
            Door(k, new Vector3(-1.94f, 0f, 12f), Vector3.right, 1.1f, DoorBlue, "15");
            Door(k, new Vector3(-1.94f, 0f, 19f), Vector3.right, 1.1f, DoorBlue, "16");
            Door(k, new Vector3(-1.94f, 0f, 23.5f), Vector3.right, 0.9f, new Color(0.62f, 0.62f, 0.6f));
            Handrail(k, new Vector3(1.94f, 0f, 5.2f), new Vector3(1.94f, 0f, 8.3f), Vector3.left * 0.07f);
            Handrail(k, new Vector3(1.94f, 0f, 10.7f), new Vector3(1.94f, 0f, 12.8f), Vector3.left * 0.07f);
            Handrail(k, new Vector3(1.94f, 0f, 20.9f), new Vector3(1.94f, 0f, 25.4f), Vector3.left * 0.07f);
            Handrail(k, new Vector3(-1.94f, 0f, 8.6f), new Vector3(-1.94f, 0f, 10.8f), Vector3.right * 0.07f);
            Handrail(k, new Vector3(-1.94f, 0f, 13.4f), new Vector3(-1.94f, 0f, 17.8f), Vector3.right * 0.07f);

            // Stairwell B: the fire doors, their push bar, the exit sign's green glow.
            var stair = new Vector3(0f, 0f, 25.94f);
            Box(k, "Stairwell door", stair + new Vector3(-0.36f, 1.05f, 0f), new Vector3(0.7f, 2.1f, 0.05f), DoorBlue);
            Box(k, "Stairwell door", stair + new Vector3(0.36f, 1.05f, 0f), new Vector3(0.7f, 2.1f, 0.05f), DoorBlue);
            Box(k, "Stairwell glass", stair + new Vector3(-0.36f, 1.5f, -0.03f), new Vector3(0.3f, 0.5f, 0.01f), Glass);
            Box(k, "Stairwell glass", stair + new Vector3(0.36f, 1.5f, -0.03f), new Vector3(0.3f, 0.5f, 0.01f), Glass);
            Box(k, "Stairwell frame", stair + new Vector3(0f, 2.13f, 0f), new Vector3(1.5f, 0.06f, 0.08f), DoorFrame);
            Box(k, "Push bar", stair + new Vector3(0f, 1.0f, -0.07f), new Vector3(1.2f, 0.05f, 0.05f), VhWakingStyle.Metal);
            Glow(k, "Exit sign", k.meshes.Box(new Vector3(0.4f, 0.16f, 0.05f)), new Color(0.2f, 0.5f, 0.3f), new Color(0.25f, 0.95f, 0.45f) * 1.4f, stair + new Vector3(0f, 2.38f, -0.05f), Quaternion.identity);
            AddLight(k, "Exit sign glow", stair + new Vector3(0f, 2.3f, -0.4f), new Color(0.4f, 1f, 0.55f), 0.2f, 2f);
            Spot(k, day2 ? "stairwell2" : "stairwell", new Vector3(0f, 1.1f, 25.8f), 0.8f);

            CorridorDressing(k, day2);
            Station(k, day2);
            Room14(k, day2);

            // People.
            if (!day2)
            {
                var raf = Person(k, "Rafael", new Vector3(-1.2f, 0f, 6.6f), 200f, 1.76f, TealScrubs, TealScrubs * 0.85f, DarkHair);
                Part(k, raf, "Lanyard", k.meshes.Tube(new List<Vector3> { new Vector3(-0.08f, 1.43f, 0.11f), new Vector3(0f, 1.2f, 0.135f), new Vector3(0.08f, 1.43f, 0.11f) }, 0.006f, 5), VhWakingStyle.Lanyard, Vector3.zero, Quaternion.identity, false);
                Part(k, raf, "Badge", k.meshes.Box(new Vector3(0.055f, 0.08f, 0.005f)), Plastic, new Vector3(0f, 1.16f, 0.138f), Quaternion.identity, false);
                Spot(k, "rafael", new Vector3(-1.2f, 1.4f, 6.6f), 0.6f);
            }
            else
            {
                Person(k, "Rafael", new Vector3(-1.0f, 0f, 9.5f), 160f, 1.76f, TealScrubs, TealScrubs * 0.85f, DarkHair);
                Spot(k, "rafael2", new Vector3(-1.0f, 1.4f, 9.5f), 0.6f);
                // The visitor from LeBaget, outside room 14, a basket of fruit in his hands.
                var v = Person(k, "The visitor (LeBaget)", new Vector3(1.0f, 0f, 16.6f), 250f, 1.82f, Suit, Suit * 0.9f, GreyHair * 0.75f, false, Shirt);
                Part(k, v, "Tie", k.meshes.Box(new Vector3(0.04f, 0.2f, 0.01f)), Tie, new Vector3(0f, 1.3f, 0.128f), Quaternion.identity, false);
                Part(k, v, "Fruit basket", k.meshes.RoundedBox(new Vector3(0.32f, 0.12f, 0.22f), 0.04f, 3), Laminate, new Vector3(0f, 0.95f, 0.24f), Quaternion.identity);
                Part(k, v, "Cellophane", k.meshes.RoundedBox(new Vector3(0.3f, 0.16f, 0.2f), 0.07f, 3, 0.02f), new Color(0.85f, 0.88f, 0.9f), new Vector3(0f, 1.06f, 0.24f), Quaternion.identity, false);
                Color[] fruit = { new Color(0.78f, 0.45f, 0.22f), new Color(0.62f, 0.2f, 0.18f), new Color(0.82f, 0.74f, 0.3f) };
                var fruitAt = new[] { new Vector3(-0.08f, 1.04f, 0.22f), new Vector3(0.06f, 1.05f, 0.2f), new Vector3(0f, 1.06f, 0.29f) };
                for (int i = 0; i < 3; i++)
                    Part(k, v, "Fruit", k.meshes.RoundedBox(Vector3.one * 0.075f, 0.036f, 3), fruit[i], fruitAt[i], Quaternion.identity, false);
                Spot(k, "visitor", new Vector3(1.0f, 1.4f, 16.6f), 0.6f);
            }

            VhWakingStyle.SetAmbient(new Color(0.34f, 0.36f, 0.37f), new Color(0.2f, 0.19f, 0.18f), new Color(0.2f, 0.22f, 0.23f), 0f, 0.25f);
            a.spawn = new Vector3(0f, 0.05f, 1.5f);
            a.spawnYaw = 0f;
        }

        static void CorridorDressing(Kit k, bool day2)
        {
            // The noticeboard: cork in a frame, notices pinned up (hand hygiene, the rota, a leaving card).
            var board = new Vector3(-1.94f, 1.45f, 2.0f);
            Box(k, "Noticeboard frame", board + Vector3.right * 0.02f, new Vector3(0.03f, 0.75f, 1.25f), Laminate);
            Box(k, "Noticeboard", board + Vector3.right * 0.04f, new Vector3(0.02f, 0.68f, 1.18f), Cork);
            Color[] papers = { new Color(0.92f, 0.91f, 0.86f), new Color(0.85f, 0.9f, 0.92f), new Color(0.94f, 0.88f, 0.72f), new Color(0.92f, 0.91f, 0.86f), new Color(0.88f, 0.8f, 0.82f) };
            var at = new[] { new Vector3(0f, 0.12f, -0.38f), new Vector3(0f, 0.1f, 0.05f), new Vector3(0f, -0.15f, -0.18f), new Vector3(0f, -0.12f, 0.36f), new Vector3(0f, 0.16f, 0.4f) };
            var sizes = new[] { new Vector2(0.21f, 0.3f), new Vector2(0.3f, 0.21f), new Vector2(0.15f, 0.2f), new Vector2(0.21f, 0.3f), new Vector2(0.14f, 0.14f) };
            for (int i = 0; i < papers.Length; i++)
                Box(k, "Notice", board + Vector3.right * (0.052f + i * 0.001f) + at[i], new Vector3(0.004f, sizes[i].y, sizes[i].x), papers[i], false, 0f, Quaternion.Euler((i - 2) * 1.5f, 0f, 0f));

            // A fire extinguisher on its bracket, its sign above.
            var ext = new Vector3(-1.86f, 0f, 8.5f);
            Part(k, k.root, "Extinguisher", k.meshes.Cylinder(0.075f, 0.075f, 0.5f, 16), ExtinguisherRed, ext + Vector3.up * 0.55f, Quaternion.identity);
            Part(k, k.root, "Extinguisher top", k.meshes.Cylinder(0.03f, 0.02f, 0.08f, 10), Shoe, ext + Vector3.up * 1.05f, Quaternion.identity);
            Part(k, k.root, "Extinguisher hose", k.meshes.Tube(new List<Vector3> { ext + new Vector3(0.02f, 1.1f, 0.03f), ext + new Vector3(0.06f, 0.95f, 0.09f), ext + new Vector3(0.07f, 0.7f, 0.08f) }, 0.01f, 6), Shoe, Vector3.zero, Quaternion.identity, false);
            Box(k, "Fire sign", new Vector3(-1.93f, 1.5f, 8.5f), new Vector3(0.01f, 0.2f, 0.2f), ExtinguisherRed);

            // A linen trolley parked by room 12: a steel frame, a blue bag.
            var t = new Vector3(1.45f, 0f, 11.6f);
            foreach (var o in new[] { new Vector3(-0.22f, 0f, -0.32f), new Vector3(0.22f, 0f, -0.32f), new Vector3(-0.22f, 0f, 0.32f), new Vector3(0.22f, 0f, 0.32f) })
            {
                Part(k, k.root, "Trolley post", k.meshes.Cylinder(0.012f, 0.012f, 0.9f, 8), VhWakingStyle.Metal, t + o + Vector3.up * 0.08f, Quaternion.identity);
                Part(k, k.root, "Castor", k.meshes.Cylinder(0.035f, 0.035f, 0.03f, 10), Shoe, t + o + new Vector3(0f, 0.035f, 0f), Quaternion.Euler(0f, 0f, 90f));
            }
            Box(k, "Trolley rim", t + Vector3.up * 0.98f, new Vector3(0.48f, 0.025f, 0.68f), VhWakingStyle.Metal);
            Box(k, "Linen bag", t + Vector3.up * 0.62f, new Vector3(0.44f, 0.66f, 0.62f), LinenBlue, true, 0.08f);
            Box(k, "Folded sheets", t + Vector3.up * 1.03f + Vector3.forward * 0.12f, new Vector3(0.3f, 0.08f, 0.24f), VhWakingStyle.Sheet, false, 0.02f);

            // A wheelchair parked against the wall past room 16.
            Wheelchair(k, new Vector3(-1.45f, 0f, 21.2f), 90f);
            if (!day2)
            {
                // A wet-floor sign: the floor's just been done.
                var sign = new Vector3(0.9f, 0f, 22.2f);
                Box(k, "Wet floor sign", sign + new Vector3(0f, 0.3f, 0.06f), new Vector3(0.28f, 0.6f, 0.012f), SharpsYellow, false, 0f, Quaternion.Euler(-12f, 20f, 0f));
                Box(k, "Wet floor sign", sign + new Vector3(0f, 0.3f, -0.06f), new Vector3(0.28f, 0.6f, 0.012f), SharpsYellow, false, 0f, Quaternion.Euler(12f, 20f, 0f));
            }
        }

        static void Wheelchair(Kit k, Vector3 at, float yaw)
        {
            var w = new GameObject("Wheelchair").transform;
            w.SetParent(k.root, false);
            w.localPosition = at;
            w.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var ring = new List<Vector3>();
            for (int i = 0; i <= 28; i++)
            {
                float a = i / 28f * Mathf.PI * 2f;
                ring.Add(new Vector3(0f, Mathf.Sin(a) * 0.29f, Mathf.Cos(a) * 0.29f));
            }
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                Part(k, w, "Wheel", k.meshes.Tube(ring, 0.018f, 8), Shoe, new Vector3(x, 0.3f, -0.08f), Quaternion.identity);
                Part(k, w, "Push rim", k.meshes.Tube(ring, 0.007f, 6), VhWakingStyle.Metal, new Vector3(x * 1.1f, 0.3f, -0.08f), Quaternion.identity, false);
                Part(k, w, "Hub", k.meshes.Cylinder(0.03f, 0.03f, 0.05f, 10), VhWakingStyle.Metal, new Vector3(x, 0.3f, -0.08f), Quaternion.Euler(0f, 0f, 90f), false);
                Part(k, w, "Castor", k.meshes.Cylinder(0.06f, 0.06f, 0.025f, 12), Shoe, new Vector3(x * 0.8f, 0.06f, 0.36f), Quaternion.Euler(0f, 0f, 90f));
                Part(k, w, "Frame", k.meshes.Tube(new List<Vector3> { new Vector3(x * 0.85f, 0.08f, 0.36f), new Vector3(x * 0.85f, 0.5f, 0.3f), new Vector3(x * 0.85f, 0.5f, -0.2f), new Vector3(x * 0.85f, 0.95f, -0.26f) }, 0.012f, 6), VhWakingStyle.Metal, Vector3.zero, Quaternion.identity);
            }
            Part(k, w, "Seat", k.meshes.RoundedBox(new Vector3(0.46f, 0.04f, 0.44f), 0.015f, 2), Shoe, new Vector3(0f, 0.5f, 0.06f), Quaternion.identity);
            Part(k, w, "Back", k.meshes.RoundedBox(new Vector3(0.46f, 0.4f, 0.03f), 0.015f, 2), Shoe, new Vector3(0f, 0.74f, -0.23f), Quaternion.Euler(-8f, 0f, 0f));
            Part(k, w, "Footplate", k.meshes.Box(new Vector3(0.36f, 0.015f, 0.14f)), VhWakingStyle.MetalDark, new Vector3(0f, 0.12f, 0.4f), Quaternion.identity);
            var col = w.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0.05f);
            col.size = new Vector3(0.66f, 0.9f, 0.9f);
        }

        static void Station(Kit k, bool day2)
        {
            // The counter: a work desk behind a raised ledge facing the corridor, a laminate front, a worktop.
            Box(k, "Counter front", new Vector3(-3.25f, 0.55f, 5.5f), new Vector3(0.08f, 1.1f, 3.6f), Laminate, true);
            Box(k, "Counter ledge", new Vector3(-3.2f, 1.12f, 5.5f), new Vector3(0.32f, 0.04f, 3.7f), Worktop, false, 0.01f);
            Box(k, "Desk", new Vector3(-3.7f, 0.73f, 5.5f), new Vector3(0.9f, 0.04f, 3.6f), Worktop, true, 0.01f);
            Box(k, "Desk (return)", new Vector3(-4.4f, 0.73f, 7.5f), new Vector3(1.2f, 0.04f, 0.6f), Worktop, true, 0.01f);
            Box(k, "Desk pedestal", new Vector3(-3.95f, 0.36f, 3.95f), new Vector3(0.42f, 0.7f, 0.5f), Laminate, true, 0.01f);
            Box(k, "Desk pedestal", new Vector3(-4.6f, 0.36f, 7.5f), new Vector3(0.5f, 0.7f, 0.5f), Laminate, true, 0.01f);
            for (int i = 0; i < 3; i++)
                Box(k, "Drawer pull", new Vector3(-3.73f, 0.6f - i * 0.2f, 3.95f), new Vector3(0.02f, 0.02f, 0.14f), VhWakingStyle.Metal);

            // Two monitors and their keyboards, the desk phone, a mug, the binders, the in-tray.
            for (int i = 0; i < 2; i++)
            {
                float z = 4.6f + i * 1.8f;
                var screen = k.style.Unique(Color.black, null, "VH station screen");
                VhWakingStyle.SetEmission(screen, (i == 0 ? new Color(0.36f, 0.5f, 0.62f) : new Color(0.42f, 0.48f, 0.5f)) * 0.75f);
                Box(k, "Monitor", new Vector3(-3.5f, 1.07f, z), new Vector3(0.04f, 0.32f, 0.52f), VhWakingStyle.MetalDark, false, 0.01f);
                VhMeshKit.Show(k.root, "Monitor screen", k.meshes.Quad(0.48f, 0.28f), screen, new Vector3(-3.525f, 1.075f, z), Quaternion.Euler(0f, 90f, 0f), false);
                Box(k, "Monitor stand", new Vector3(-3.46f, 0.84f, z), new Vector3(0.04f, 0.18f, 0.05f), VhWakingStyle.MetalDark);
                Box(k, "Monitor foot", new Vector3(-3.48f, 0.755f, z), new Vector3(0.18f, 0.01f, 0.2f), VhWakingStyle.MetalDark);
                Box(k, "Keyboard", new Vector3(-3.86f, 0.76f, z), new Vector3(0.15f, 0.02f, 0.44f), new Color(0.22f, 0.23f, 0.25f), false, 0.005f, Quaternion.Euler(0f, 0f, 3f));
                Box(k, "Mouse", new Vector3(-3.86f, 0.76f, z + 0.32f), new Vector3(0.1f, 0.025f, 0.06f), new Color(0.22f, 0.23f, 0.25f), false, 0.012f);
            }
            AddLight(k, "Screens", new Vector3(-3.95f, 1.15f, 5.5f), new Color(0.6f, 0.75f, 0.9f), 0.15f, 1.6f);
            Box(k, "Desk phone", new Vector3(-3.75f, 0.78f, 6.95f), new Vector3(0.2f, 0.06f, 0.22f), new Color(0.24f, 0.25f, 0.27f), false, 0.02f);
            Box(k, "Handset", new Vector3(-3.83f, 0.825f, 6.95f), new Vector3(0.05f, 0.04f, 0.2f), new Color(0.24f, 0.25f, 0.27f), false, 0.018f);
            Part(k, k.root, "Mug", k.meshes.Cylinder(0.04f, 0.04f, 0.095f, 14), VhWakingStyle.Mug, new Vector3(-3.5f, 0.75f, 7.15f), Quaternion.identity);
            Color[] binders = { new Color(0.3f, 0.4f, 0.58f), new Color(0.58f, 0.28f, 0.26f), new Color(0.3f, 0.48f, 0.38f), new Color(0.62f, 0.56f, 0.32f), new Color(0.3f, 0.4f, 0.58f), new Color(0.45f, 0.45f, 0.48f) };
            for (int i = 0; i < binders.Length; i++)
                Box(k, "Binder", new Vector3(-4.75f, 0.91f, 7.22f + i * 0.075f), new Vector3(0.28f, 0.32f, 0.065f), binders[i], false, 0.004f, Quaternion.Euler(i == 5 ? 10f : 0f, 0f, 0f));
            Box(k, "Paper tray", new Vector3(-4.0f, 0.79f, 3.85f), new Vector3(0.32f, 0.08f, 0.24f), PlasticGrey, false, 0.01f);
            Box(k, "Paper", new Vector3(-4.0f, 0.82f, 3.85f), new Vector3(0.29f, 0.02f, 0.21f), VhWakingStyle.Paper);

            // The patient whiteboard on the back wall (the beds, the names in marker), the wall clock, cupboards.
            var wb = new Vector3(-4.93f, 1.6f, 5.0f);
            Box(k, "Whiteboard frame", wb, new Vector3(0.03f, 0.9f, 1.6f), PlasticGrey);
            Box(k, "Whiteboard", wb + Vector3.right * 0.017f, new Vector3(0.006f, 0.84f, 1.54f), new Color(0.93f, 0.94f, 0.93f));
            for (int r = 0; r < 6; r++)
                Box(k, "Board line", wb + new Vector3(0.022f, 0.3f - r * 0.12f, 0f), new Vector3(0.002f, 0.006f, 1.5f), new Color(0.3f, 0.36f, 0.5f));
            for (int r = 0; r < 6; r++)
                Box(k, "Marker", wb + new Vector3(0.023f, 0.25f - r * 0.12f, -0.4f + (r % 3) * 0.06f), new Vector3(0.002f, 0.025f, 0.32f + (r % 2) * 0.12f), r == 3 ? new Color(0.6f, 0.2f, 0.2f) : new Color(0.15f, 0.2f, 0.32f));
            Clock(k, new Vector3(-4.94f, 2.3f, 6.6f), Vector3.right, day2 ? 9.25f : 8.83f);
            Box(k, "Wall cupboard", new Vector3(-4.82f, 2.05f, 7.3f), new Vector3(0.34f, 0.6f, 1.0f), Laminate, false, 0.01f);
            if (Model(k, "chair_desk", new Vector3(-4.3f, 0f, 4.7f), 80f, 0.95f) == null)
                Box(k, "Desk chair", new Vector3(-4.3f, 0.47f, 4.7f), new Vector3(0.48f, 0.08f, 0.48f), ChairBlue, true, 0.03f);
            if (Model(k, "chair_desk", new Vector3(-4.4f, 0f, 6.5f), 110f, 0.95f) == null)
                Box(k, "Desk chair", new Vector3(-4.4f, 0.47f, 6.5f), new Vector3(0.48f, 0.08f, 0.48f), ChairBlue, true, 0.03f);
            Model(k, "plant", new Vector3(-2.35f, 0f, 3.35f), 0f, 0.9f);
            Box(k, "Bin", new Vector3(-3.55f, 0.18f, 7.75f), new Vector3(0.26f, 0.36f, 0.26f), PlasticGrey, true, 0.03f);

            if (!day2)
            {
                // The police envelope on the ledge, and her phone face down by it.
                var env = k.config.Art("prop_police_envelope");
                Box(k, "Police envelope", new Vector3(-3.2f, 1.147f, 4.9f), new Vector3(0.24f, 0.012f, 0.33f), new Color(0.83f, 0.76f, 0.6f), false, 0f, Quaternion.Euler(0f, 8f, 0f));
                if (env != null)
                    VhMeshKit.Show(k.root, "Envelope face", k.meshes.Quad(0.22f, 0.3f), k.style.Mat(Color.white, env, 0f), new Vector3(-3.2f, 1.154f, 4.9f), Quaternion.Euler(90f, 8f, 0f), false);
                Spot(k, "police_report", new Vector3(-3.3f, 1.2f, 4.9f), 0.5f);
                Box(k, "Her phone", new Vector3(-3.2f, 1.145f, 4.25f), new Vector3(0.075f, 0.01f, 0.15f), VhWakingStyle.PhoneBody, false, 0.004f, Quaternion.Euler(0f, -14f, 0f));
                Spot(k, "phone", new Vector3(-3.2f, 1.2f, 4.25f), 0.4f);
            }
            Spot(k, "station", new Vector3(-3.7f, 1f, 6.4f), 0.6f);
        }

        // ---------- Room 14 ----------

        static void Room14(Kit k, bool day2)
        {
            var c = k.config;
            const float h = 2.7f;
            CeilingLight(k, new Vector3(5.6f, h - 0.01f, 19.4f), new Vector2(0.6f, 0.6f), new Color(0.95f, 0.96f, 1f), 0.55f, 5f, true, 0.8f);

            // The window (night outside), its blinds half down, the sill, the radiator, LeBaget's lilies.
            var night = k.style.Unique(new Color(0.05f, 0.07f, 0.12f), null, "VH room 14 window");
            VhWakingStyle.SetEmission(night, new Color(0.06f, 0.09f, 0.16f));
            VhMeshKit.Show(k.root, "Window (night)", k.meshes.Quad(1.6f, 1.2f), night, new Vector3(8.93f, 1.55f, 19.6f), Quaternion.Euler(0f, 90f, 0f), false);
            Box(k, "Window frame (top)", new Vector3(8.92f, 2.17f, 19.6f), new Vector3(0.08f, 0.05f, 1.7f), Plastic);
            Box(k, "Window frame (mullion)", new Vector3(8.92f, 1.55f, 19.6f), new Vector3(0.08f, 1.2f, 0.05f), Plastic);
            Box(k, "Window frame (side)", new Vector3(8.92f, 1.55f, 18.77f), new Vector3(0.08f, 1.3f, 0.05f), Plastic);
            Box(k, "Window frame (side)", new Vector3(8.92f, 1.55f, 20.43f), new Vector3(0.08f, 1.3f, 0.05f), Plastic);
            for (int i = 0; i < 9; i++)
                Box(k, "Blind slat", new Vector3(8.86f, 2.1f - i * 0.055f, 19.6f), new Vector3(0.05f, 0.006f, 1.6f), new Color(0.82f, 0.82f, 0.78f), false, 0f, Quaternion.Euler(0f, 0f, 30f));
            Box(k, "Blind bottom rail", new Vector3(8.86f, 1.6f, 19.6f), new Vector3(0.05f, 0.02f, 1.6f), new Color(0.82f, 0.82f, 0.78f));
            Box(k, "Sill", new Vector3(8.8f, 0.92f, 19.6f), new Vector3(0.3f, 0.04f, 1.8f), Plastic);
            Box(k, "Radiator", new Vector3(8.86f, 0.5f, 19.6f), new Vector3(0.1f, 0.55f, 1.3f), new Color(0.88f, 0.88f, 0.86f), true, 0.01f);
            for (int i = 0; i < 12; i++)
                Box(k, "Radiator fin", new Vector3(8.8f, 0.5f, 19.0f + i * 0.11f), new Vector3(0.02f, 0.52f, 0.03f), new Color(0.84f, 0.84f, 0.82f));
            Bouquet(k, new Vector3(8.78f, 0.94f, 19.25f));
            Spot(k, "bouquet", new Vector3(8.75f, 1.2f, 19.25f), 0.5f);
            AddLight(k, "Window cold", new Vector3(8.3f, 1.6f, 19.6f), new Color(0.55f, 0.65f, 0.95f), 0.25f, 3.5f);

            // The headwall behind the bed: the trunking with its outlets, the reading light, the oxygen.
            Box(k, "Headwall trunking", new Vector3(6.0f, 1.45f, 23.9f), new Vector3(2.8f, 0.24f, 0.1f), Plastic, false, 0.01f);
            for (int i = 0; i < 4; i++)
                Box(k, "Socket", new Vector3(5.0f + i * 0.22f, 1.45f, 23.84f), new Vector3(0.14f, 0.08f, 0.02f), Plastic * 0.95f);
            Part(k, k.root, "Oxygen outlet", k.meshes.Cylinder(0.035f, 0.035f, 0.04f, 12), new Color(0.82f, 0.82f, 0.85f), new Vector3(6.75f, 1.45f, 23.83f), Quaternion.Euler(90f, 0f, 0f), false);
            Part(k, k.root, "Flowmeter", k.meshes.Cylinder(0.02f, 0.02f, 0.16f, 10), new Color(0.75f, 0.85f, 0.85f), new Vector3(6.75f, 1.24f, 23.79f), Quaternion.identity, false);
            Part(k, k.root, "Suction", k.meshes.Cylinder(0.05f, 0.05f, 0.18f, 14), new Color(0.82f, 0.85f, 0.88f), new Vector3(7.1f, 1.12f, 23.8f), Quaternion.identity, false);
            Box(k, "Reading light", new Vector3(6.0f, 2.05f, 23.88f), new Vector3(1.1f, 0.06f, 0.12f), Plastic, false, 0.02f);
            AddLight(k, "Reading light (low)", new Vector3(6.0f, 1.9f, 23.5f), new Color(1f, 0.88f, 0.72f), 0.55f, 3.2f, true);

            Bed(k, new Vector3(6.0f, 0f, 22.5f));

            // Elias: a gown on his shoulders, the blanket folded back at his chest, his arms out on it, the cannula.
            var skin = VhWakingStyle.Skin * 0.97f;
            Part(k, k.root, "Elias (body, under the blanket)", k.meshes.RoundedBox(new Vector3(0.46f, 0.18f, 1.45f), 0.09f, 4, 0.03f), HospitalBlanket, new Vector3(6.0f, 0.73f, 22.15f), Quaternion.identity);
            Part(k, k.root, "Blanket", k.meshes.RoundedBox(new Vector3(0.92f, 0.06f, 1.55f), 0.03f, 3, 0.01f), HospitalBlanket, new Vector3(6.0f, 0.66f, 22.1f), Quaternion.identity);
            Part(k, k.root, "Sheet turned down", k.meshes.RoundedBox(new Vector3(0.9f, 0.05f, 0.18f), 0.02f, 3), VhWakingStyle.Sheet, new Vector3(6.0f, 0.8f, 22.85f), Quaternion.identity);
            Part(k, k.root, "Elias (shoulders, gown)", k.meshes.RoundedBox(new Vector3(0.44f, 0.16f, 0.3f), 0.07f, 4), Gown, new Vector3(6.0f, 0.78f, 23.05f), Quaternion.identity);
            Part(k, k.root, "Elias (neck)", k.meshes.Cylinder(0.05f, 0.05f, 0.12f, 10), skin, new Vector3(6.0f, 0.82f, 23.18f), Quaternion.Euler(90f, 0f, 0f));
            Part(k, k.root, "Elias (head)", k.meshes.RoundedBox(new Vector3(0.18f, 0.21f, 0.23f), 0.085f, 5, 0.01f), skin, new Vector3(6.0f, 0.86f, 23.38f), Quaternion.Euler(-72f, 0f, 0f));
            Part(k, k.root, "Elias (nose)", k.meshes.RoundedBox(new Vector3(0.03f, 0.04f, 0.05f), 0.012f, 2), skin * 0.96f, new Vector3(6.0f, 0.97f, 23.33f), Quaternion.Euler(-72f, 0f, 0f));
            Part(k, k.root, "Elias (hair)", k.meshes.RoundedBox(new Vector3(0.2f, 0.12f, 0.21f), 0.06f, 4), DarkHair, new Vector3(6.0f, 0.86f, 23.5f), Quaternion.Euler(-72f, 0f, 0f));
            foreach (float x in new[] { 5.72f, 6.28f })
            {
                Part(k, k.root, "Elias (arm, gown)", k.meshes.Cylinder(0.05f, 0.048f, 0.22f, 10), Gown, new Vector3(x, 0.79f, 23.06f), Quaternion.Euler(-90f, 0f, 0f));
                Part(k, k.root, "Elias (forearm)", k.meshes.Cylinder(0.04f, 0.036f, 0.42f, 10), skin, new Vector3(x, 0.79f, 22.84f), Quaternion.Euler(-90f, 0f, 0f));
                Part(k, k.root, "Elias (hand)", k.meshes.RoundedBox(new Vector3(0.07f, 0.035f, 0.1f), 0.015f, 3), skin, new Vector3(x, 0.78f, 22.37f), Quaternion.identity);
            }
            Part(k, k.root, "Wristband", k.meshes.Cylinder(0.044f, 0.044f, 0.022f, 12), new Color(0.92f, 0.92f, 0.9f), new Vector3(6.28f, 0.79f, 22.48f), Quaternion.Euler(90f, 0f, 0f), false);
            var cannula = new List<Vector3> { new Vector3(6.75f, 1.4f, 23.82f), new Vector3(6.55f, 1.0f, 23.7f), new Vector3(6.15f, 0.95f, 23.42f), new Vector3(6.04f, 0.95f, 23.3f), new Vector3(5.95f, 0.96f, 23.42f), new Vector3(5.85f, 0.9f, 23.5f) };
            Part(k, k.root, "Nasal cannula", k.meshes.Tube(cannula, 0.004f, 5), new Color(0.82f, 0.9f, 0.9f), Vector3.zero, Quaternion.identity, false);
            Box(k, "Nurse call", new Vector3(5.55f, 0.75f, 22.6f), new Vector3(0.05f, 0.03f, 0.14f), Plastic, false, 0.01f, Quaternion.Euler(0f, 20f, 0f));
            Spot(k, "elias", new Vector3(6.0f, 0.9f, 22.6f), 1.1f);
            Spot(k, day2 ? "wristband2" : "wristband", new Vector3(6.28f, 0.82f, 22.48f), 0.35f);

            // The chart, clipped to the foot of the bed.
            var chartArt = c.Art("prop_chart");
            Box(k, "Clipboard", new Vector3(6.0f, 0.68f, 21.4f), new Vector3(0.24f, 0.32f, 0.015f), Laminate, false, 0.005f, Quaternion.Euler(-8f, 0f, 0f));
            VhMeshKit.Show(k.root, "Chart page", k.meshes.Quad(0.21f, 0.28f), k.style.Mat(chartArt != null ? Color.white : VhWakingStyle.Paper, chartArt, 0f), new Vector3(6.0f, 0.675f, 21.39f), Quaternion.Euler(-8f, 0f, 0f), false);
            Box(k, "Clip", new Vector3(6.0f, 0.84f, 21.39f), new Vector3(0.08f, 0.025f, 0.02f), VhWakingStyle.Metal);
            Spot(k, "chart", new Vector3(6.0f, 0.75f, 21.38f), 0.45f);

            // The drip on its stand, the pump, the line to his hand.
            var drip = new Vector3(6.9f, 0f, 22.95f);
            Part(k, k.root, "Drip stand base", k.meshes.Cylinder(0.24f, 0.24f, 0.03f, 5), VhWakingStyle.MetalDark, drip + Vector3.up * 0.08f, Quaternion.identity);
            Part(k, k.root, "Drip stand", k.meshes.Cylinder(0.015f, 0.015f, 1.95f, 8), VhWakingStyle.Metal, drip + Vector3.up * 0.08f, Quaternion.identity);
            Part(k, k.root, "Drip hooks", k.meshes.Box(new Vector3(0.28f, 0.012f, 0.012f)), VhWakingStyle.Metal, drip + Vector3.up * 2.02f, Quaternion.identity);
            Part(k, k.root, "Drip bag", k.meshes.RoundedBox(new Vector3(0.14f, 0.22f, 0.035f), 0.03f, 3, 0.01f), new Color(0.86f, 0.9f, 0.92f), drip + new Vector3(0.1f, 1.86f, 0f), Quaternion.identity);
            Box(k, "Infusion pump", drip + new Vector3(0f, 1.22f, -0.07f), new Vector3(0.16f, 0.2f, 0.12f), Plastic, false, 0.02f);
            Glow(k, "Pump display", k.meshes.Quad(0.1f, 0.05f), Color.black, new Color(0.4f, 0.8f, 0.6f), drip + new Vector3(0f, 1.26f, -0.131f), Quaternion.identity);
            var line = new List<Vector3> { drip + new Vector3(0.1f, 1.74f, 0f), drip + new Vector3(0.02f, 1.35f, -0.05f), drip + new Vector3(0f, 1.1f, -0.1f), drip + new Vector3(-0.3f, 0.85f, -0.3f), new Vector3(6.3f, 0.8f, 22.42f) };
            Part(k, k.root, "Drip line", k.meshes.Tube(line, 0.0035f, 5), new Color(0.85f, 0.88f, 0.9f), Vector3.zero, Quaternion.identity, false);
            if (day2) Spot(k, "round2", drip + new Vector3(0f, 1.4f, 0f), 0.5f);

            // The patient monitor on its wall arm, keeping time.
            var mon = new Vector3(4.95f, 1.75f, 23.72f);
            Box(k, "Monitor arm", new Vector3(4.95f, 1.75f, 23.88f), new Vector3(0.06f, 0.06f, 0.18f), VhWakingStyle.MetalDark);
            Box(k, "Patient monitor", mon, new Vector3(0.38f, 0.3f, 0.1f), new Color(0.82f, 0.82f, 0.8f), false, 0.02f);
            Box(k, "Monitor bezel", mon + new Vector3(0f, 0.01f, -0.051f), new Vector3(0.32f, 0.22f, 0.004f), new Color(0.04f, 0.05f, 0.06f));
            var waveGo = new GameObject("Monitor traces").transform;
            waveGo.SetParent(k.root, false);
            waveGo.localPosition = mon + new Vector3(0f, 0.01f, -0.056f);
            waveGo.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Trace(k, waveGo, new Vector3(0f, 0.06f, 0f), new Color(0.3f, 1f, 0.55f) * 1.6f, 0);
            Trace(k, waveGo, Vector3.zero, new Color(0.35f, 0.75f, 1f) * 1.5f, 1);
            Trace(k, waveGo, new Vector3(0f, -0.06f, 0f), new Color(1f, 0.9f, 0.4f) * 1.4f, 2);
            AddLight(k, "Monitor glow", mon + new Vector3(0f, 0f, -0.3f), new Color(0.5f, 1f, 0.7f), 0.12f, 1.6f);
            Spot(k, day2 ? "vitals2" : "vitals", mon, 0.45f);
            k.area.monitor = mon;

            // The locker with the photo and a card; the over-bed table with water; the curtain on its track.
            if (Model(k, "nightstand", new Vector3(7.45f, 0f, 23.45f), 180f, 0.62f) == null)
                Box(k, "Locker", new Vector3(7.45f, 0.31f, 23.45f), new Vector3(0.48f, 0.62f, 0.44f), Laminate, true, 0.02f);
            var photoArt = c.Art("prop_family_photo");
            var framed = new GameObject("Framed photo").transform;
            framed.SetParent(k.root, false);
            framed.localPosition = new Vector3(7.5f, 0.63f, 23.5f);
            framed.localRotation = Quaternion.Euler(10f, 30f, 0f);
            Part(k, framed, "Frame", k.meshes.Box(new Vector3(0.17f, 0.22f, 0.015f)), VhWakingStyle.WoodDark, new Vector3(0f, 0.11f, 0f), Quaternion.identity);
            VhMeshKit.Show(framed, "Photo", k.meshes.Quad(0.14f, 0.18f), k.style.Mat(photoArt != null ? Color.white : VhWakingStyle.Paper, photoArt, 0f), new Vector3(0f, 0.11f, -0.009f), Quaternion.identity, false);
            Box(k, "Get-well card", new Vector3(7.3f, 0.7f, 23.55f), new Vector3(0.12f, 0.16f, 0.004f), new Color(0.88f, 0.84f, 0.7f), false, 0f, Quaternion.Euler(0f, 160f, 8f));
            Spot(k, "photo", new Vector3(7.5f, 0.75f, 23.5f), 0.45f);

            var tbl = new Vector3(4.95f, 0f, 21.4f);
            Box(k, "Over-bed table base", tbl + new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.04f, 0.4f), VhWakingStyle.MetalDark, false, 0.01f);
            Part(k, k.root, "Over-bed table post", k.meshes.Cylinder(0.025f, 0.025f, 0.72f, 10), VhWakingStyle.Metal, tbl + new Vector3(-0.18f, 0.06f, 0f), Quaternion.identity);
            Box(k, "Over-bed table top", tbl + new Vector3(0.1f, 0.8f, 0f), new Vector3(0.8f, 0.03f, 0.4f), Worktop, true, 0.01f);
            Part(k, k.root, "Water jug", k.meshes.Cylinder(0.05f, 0.045f, 0.2f, 14), new Color(0.78f, 0.84f, 0.88f), tbl + new Vector3(0.25f, 0.815f, 0.05f), Quaternion.identity);
            Part(k, k.root, "Cup", k.meshes.Cylinder(0.033f, 0.038f, 0.09f, 12), Plastic, tbl + new Vector3(0.05f, 0.815f, -0.08f), Quaternion.identity);
            Box(k, "Tissues", tbl + new Vector3(0.38f, 0.86f, -0.08f), new Vector3(0.12f, 0.08f, 0.22f), new Color(0.76f, 0.82f, 0.84f), false, 0.01f);

            var track = new List<Vector3> { new Vector3(4.6f, 2.6f, 23.95f), new Vector3(4.6f, 2.6f, 21.0f), new Vector3(7.6f, 2.6f, 21.0f), new Vector3(7.6f, 2.6f, 21.6f) };
            Part(k, k.root, "Curtain track", k.meshes.Tube(track, 0.012f, 6), VhWakingStyle.Metal, Vector3.zero, Quaternion.identity, false);
            var curtain = new Color(0.58f, 0.66f, 0.66f);
            for (int i = 0; i < 9; i++)
                Box(k, "Curtain (pulled back)", new Vector3(4.6f + (i % 2 == 0 ? 0.03f : -0.03f), 1.45f, 23.85f - i * 0.09f), new Vector3(0.03f, 2.25f, 0.11f), curtain, false, 0f, Quaternion.Euler(0f, i % 2 == 0 ? 35f : -35f, 0f));
            Box(k, "Curtain mesh top", new Vector3(4.6f, 2.5f, 23.5f), new Vector3(0.02f, 0.2f, 0.85f), new Color(0.8f, 0.82f, 0.82f));

            // The sink by the door, its mirror, the towel and glove dispensers, the sharps bin, a pedal bin.
            if (Model(k, "sink", new Vector3(3.35f, 0f, 16.3f), 0f, 0.95f) == null)
                Box(k, "Sink", new Vector3(3.35f, 0.82f, 16.3f), new Vector3(0.55f, 0.18f, 0.42f), Plastic, true, 0.04f);
            Box(k, "Mirror", new Vector3(3.35f, 1.55f, 16.07f), new Vector3(0.45f, 0.6f, 0.02f), new Color(0.55f, 0.6f, 0.64f));
            Box(k, "Paper towels", new Vector3(4.0f, 1.35f, 16.12f), new Vector3(0.28f, 0.34f, 0.12f), Plastic, false, 0.02f);
            Box(k, "Gloves", new Vector3(2.7f, 1.4f, 16.1f), new Vector3(0.36f, 0.14f, 0.1f), new Color(0.55f, 0.65f, 0.82f), false, 0.01f);
            Box(k, "Sharps bin", new Vector3(4.45f, 1.15f, 16.15f), new Vector3(0.2f, 0.26f, 0.15f), SharpsYellow, false, 0.015f);
            Box(k, "Sharps lid", new Vector3(4.45f, 1.29f, 16.15f), new Vector3(0.21f, 0.03f, 0.16f), ExtinguisherRed * 0.9f, false, 0.01f);
            Part(k, k.root, "Pedal bin", k.meshes.Cylinder(0.14f, 0.14f, 0.42f, 16), new Color(0.78f, 0.78f, 0.76f), new Vector3(4.9f, 0f, 16.3f), Quaternion.identity);

            // The visitors' chair: Day 1 with his effects bag on it; Day 2 drawn up to the bed for her.
            if (!day2)
            {
                if (Model(k, "armchair", new Vector3(8.35f, 0f, 17.25f), -60f, 0.85f) == null)
                    Box(k, "Visitors' chair", new Vector3(8.35f, 0.25f, 17.25f), new Vector3(0.65f, 0.5f, 0.65f), ChairBlue, true, 0.06f);
                Box(k, "Effects bag", new Vector3(8.2f, 0.72f, 17.33f), new Vector3(0.42f, 0.3f, 0.28f), new Color(0.85f, 0.86f, 0.88f), false, 0.07f, Quaternion.Euler(0f, 25f, 4f));
                Box(k, "Effects label", new Vector3(8.06f, 0.74f, 17.2f), new Vector3(0.1f, 0.06f, 0.004f), VhWakingStyle.Paper, false, 0f, Quaternion.Euler(0f, 25f, 4f));
                Spot(k, "effects", new Vector3(8.3f, 0.75f, 17.3f), 0.5f);
            }
            else
            {
                if (Model(k, "armchair", new Vector3(7.75f, 0f, 22.2f), -90f, 0.85f) == null)
                    Box(k, "Her chair", new Vector3(7.75f, 0.25f, 22.2f), new Vector3(0.65f, 0.5f, 0.65f), ChairBlue, true, 0.06f);
                Spot(k, "sit", new Vector3(7.75f, 0.7f, 22.2f), 0.5f);
                // His effects bag, moved to the floor by the locker.
                Box(k, "Effects bag", new Vector3(8.2f, 0.13f, 22.9f), new Vector3(0.36f, 0.26f, 0.24f), new Color(0.85f, 0.86f, 0.88f), false, 0.07f, Quaternion.Euler(0f, -20f, 0f));
            }
        }

        /// <summary>A hospital bed centred at <paramref name="at"/> (its head towards +Z): castors, the frame, the head and
        /// foot ends, the side rails, the mattress, the pillow.</summary>
        static void Bed(Kit k, Vector3 at)
        {
            float hw = 0.48f, hl = 1.05f;
            foreach (var o in new[] { new Vector3(-hw + 0.06f, 0f, -hl + 0.08f), new Vector3(hw - 0.06f, 0f, -hl + 0.08f), new Vector3(-hw + 0.06f, 0f, hl - 0.08f), new Vector3(hw - 0.06f, 0f, hl - 0.08f) })
            {
                Part(k, k.root, "Castor", k.meshes.Cylinder(0.05f, 0.05f, 0.04f, 12), Shoe, at + o + new Vector3(-0.02f, 0.05f, 0f), Quaternion.Euler(0f, 0f, 90f));
                Part(k, k.root, "Bed leg", k.meshes.Cylinder(0.025f, 0.025f, 0.3f, 8), VhWakingStyle.Metal, at + o + Vector3.up * 0.09f, Quaternion.identity);
            }
            Box(k, "Bed chassis", at + Vector3.up * 0.42f, new Vector3(hw * 2f, 0.12f, hl * 2f), VhWakingStyle.MetalDark, true, 0.02f);
            Box(k, "Bed deck", at + Vector3.up * 0.5f, new Vector3(hw * 2f - 0.04f, 0.04f, hl * 2f - 0.04f), BedFrame, false, 0.01f);
            Box(k, "Mattress", at + Vector3.up * 0.59f, new Vector3(0.88f, 0.14f, 1.98f), VhWakingStyle.Sheet, false, 0.05f);
            Box(k, "Head end", at + new Vector3(0f, 0.75f, hl + 0.02f), new Vector3(hw * 2f + 0.04f, 0.58f, 0.05f), BedFrame, false, 0.03f);
            Box(k, "Foot end", at + new Vector3(0f, 0.63f, -hl - 0.02f), new Vector3(hw * 2f + 0.04f, 0.42f, 0.05f), BedFrame, false, 0.03f);
            foreach (float x in new[] { -hw - 0.02f, hw + 0.02f })
            {
                Box(k, "Side rail", at + new Vector3(x, 0.8f, 0.45f), new Vector3(0.03f, 0.24f, 0.8f), BedFrame, false, 0.012f);
                Box(k, "Side rail window", at + new Vector3(x, 0.8f, 0.45f), new Vector3(0.032f, 0.12f, 0.6f), Glass * 1.6f);
            }
            Box(k, "Pillow", at + new Vector3(0f, 0.72f, 0.8f), new Vector3(0.62f, 0.13f, 0.38f), VhWakingStyle.Pillow, false, 0.06f, Quaternion.Euler(-12f, 0f, 0f));
        }

        /// <summary>One trace on a monitor (0 the heart's spikes, 1 the pleth's swell, 2 the breathing's slow wave).</summary>
        static void Trace(Kit k, Transform parent, Vector3 at, Color glow, int kind)
        {
            var wave = new List<Vector3>();
            for (int i = 0; i <= 40; i++)
            {
                float u = i / 40f;
                float y;
                if (kind == 0) { int m = i % 13; y = m == 5 ? 0.035f : m == 6 ? -0.02f : m == 7 ? 0.008f : 0f; }
                else if (kind == 1) y = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI * 6f)) * 0.02f;
                else y = Mathf.Sin(u * Mathf.PI * 2f) * 0.012f;
                wave.Add(at + new Vector3(-0.14f + u * 0.28f, y, 0f));
            }
            var mat = k.style.Unique(Color.black, null, "VH monitor trace");
            VhWakingStyle.SetEmission(mat, glow);
            VhMeshKit.Show(parent, "Trace", k.meshes.Tube(wave, 0.0028f, 4), mat, Vector3.zero, Quaternion.identity, false);
        }

        static void Bouquet(Kit k, Vector3 at)
        {
            Part(k, k.root, "Vase", k.meshes.Cylinder(0.06f, 0.075f, 0.22f, 16), new Color(0.55f, 0.64f, 0.68f), at, Quaternion.identity);
            var stem = k.style.Mat(Stem);
            var lily = k.style.Mat(Lily);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                var top = at + new Vector3(Mathf.Cos(a) * 0.1f, 0.45f + (i % 3) * 0.05f, Mathf.Sin(a) * 0.16f);
                var mid = Vector3.Lerp(at + Vector3.up * 0.18f, top, 0.5f) + new Vector3(Mathf.Cos(a) * 0.02f, 0f, Mathf.Sin(a) * 0.03f);
                VhMeshKit.Show(k.root, "Stem", k.meshes.Tube(new List<Vector3> { at + Vector3.up * 0.18f, mid, top }, 0.005f, 5), stem, Vector3.zero, Quaternion.identity, false);
                VhMeshKit.Show(k.root, "Leaf", k.meshes.RoundedBox(new Vector3(0.012f, 0.1f, 0.03f), 0.005f, 2), stem, mid, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 30f), false);
                // A lily: three petals open round the throat.
                var bloom = new GameObject("Lily").transform;
                bloom.SetParent(k.root, false);
                bloom.localPosition = top;
                bloom.localRotation = Quaternion.Euler(25f * Mathf.Cos(a), a * Mathf.Rad2Deg, 25f * Mathf.Sin(a));
                for (int p = 0; p < 3; p++)
                    VhMeshKit.Show(bloom, "Petal", k.meshes.RoundedBox(new Vector3(0.03f, 0.008f, 0.08f), 0.004f, 2, 0.003f), lily, Quaternion.Euler(0f, p * 120f, 0f) * new Vector3(0f, 0f, 0.03f), Quaternion.Euler(-25f, p * 120f, 0f), false);
            }
            // The card on its stick.
            Box(k, "Card stick", at + new Vector3(-0.07f, 0.28f, 0.02f), new Vector3(0.004f, 0.18f, 0.004f), Stem);
            Box(k, "Card", at + new Vector3(-0.07f, 0.38f, 0.02f), new Vector3(0.006f, 0.06f, 0.09f), new Color(0.95f, 0.94f, 0.9f));
        }

        // ---------- 11 PM: the booth in the closed sleep-research wing ----------

        public static void Booth(VhArea a, VisitingHoursConfig c, bool day2)
        {
            var k = Begin(a, c);
            const float h = 2.7f;
            var wall = new Color(0.66f, 0.66f, 0.62f);
            Box(k, "Floor", new Vector3(0f, -0.05f, 3f), new Vector3(6f, 0.1f, 6f), new Color(0.5f, 0.5f, 0.48f), true, 0f, null, c.Art("tex_hospital_floor"), 0.5f);
            SuspendedCeiling(k, -3f, 3f, 0f, 6f, h);
            Wall(k, -3f, 0f, -3f, 6f, h, wall);
            Wall(k, 3f, 0f, 3f, 6f, h, wall);
            Wall(k, -3f, 0f, 3f, 0f, h, wall);
            Wall(k, -3f, 6f, 3f, 6f, h, wall);
            // The closed wing's ceiling lights are off; the desk lamp is the room's warm light.
            Box(k, "Light (off)", new Vector3(-1.2f, h - 0.01f, 3f), new Vector3(0.6f, 0.02f, 1.2f), new Color(0.55f, 0.56f, 0.55f));
            Box(k, "Light (off)", new Vector3(1.2f, h - 0.01f, 3f), new Vector3(0.6f, 0.02f, 1.2f), new Color(0.55f, 0.56f, 0.55f));
            Door(k, new Vector3(-0.6f, 0f, 0.06f), Vector3.forward, 0.95f, DoorBlue);

            // The night window, the observation glass onto the dark lab.
            var night = k.style.Unique(new Color(0.04f, 0.06f, 0.1f), null, "VH booth window");
            VhWakingStyle.SetEmission(night, new Color(0.06f, 0.09f, 0.18f) * 0.8f);
            VhMeshKit.Show(k.root, "Window", k.meshes.Quad(2.4f, 1.1f), night, new Vector3(0f, 1.6f, 5.93f), Quaternion.identity, false);
            Box(k, "Window frame", new Vector3(0f, 2.16f, 5.92f), new Vector3(2.5f, 0.05f, 0.06f), Plastic);
            Box(k, "Window frame", new Vector3(0f, 1.04f, 5.92f), new Vector3(2.5f, 0.05f, 0.06f), Plastic);
            Box(k, "Window frame", new Vector3(0f, 1.6f, 5.92f), new Vector3(0.05f, 1.1f, 0.06f), Plastic);
            Box(k, "Sill", new Vector3(0f, 1.0f, 5.85f), new Vector3(2.5f, 0.04f, 0.2f), Plastic);
            var glass = k.style.Unique(new Color(0.1f, 0.12f, 0.15f), null, "VH observation glass");
            VhMeshKit.Show(k.root, "Observation glass", k.meshes.Quad(2.0f, 1.0f), glass, new Vector3(2.93f, 1.55f, 3.9f), Quaternion.Euler(0f, 90f, 0f), false);
            Box(k, "Glass frame", new Vector3(2.92f, 1.55f, 3.9f), new Vector3(0.06f, 1.1f, 2.1f), new Color(0.3f, 0.31f, 0.33f));

            // The desk: papers, Mags' mug, the lamp, a radio; her coat on the stand; the closed wing's boxes.
            Box(k, "Desk", new Vector3(2.4f, 0.73f, 2.1f), new Vector3(0.8f, 0.04f, 1.6f), Laminate * 0.85f, true, 0.01f);
            Box(k, "Desk pedestal", new Vector3(2.4f, 0.36f, 1.5f), new Vector3(0.7f, 0.7f, 0.42f), Laminate * 0.75f, true, 0.01f);
            Part(k, k.root, "Desk leg", k.meshes.Cylinder(0.025f, 0.025f, 0.72f, 8), VhWakingStyle.MetalDark, new Vector3(2.4f, 0f, 2.8f), Quaternion.identity);
            if (Model(k, "lamp", new Vector3(2.55f, 0.75f, 2.65f), -120f, 0.42f, false, false) == null)
                Part(k, k.root, "Lamp", k.meshes.Cylinder(0.1f, 0.05f, 0.14f, 14), VhWakingStyle.Lampshade, new Vector3(2.55f, 1.05f, 2.65f), Quaternion.identity);
            AddLight(k, "Desk lamp", new Vector3(2.45f, 1.15f, 2.55f), new Color(1f, 0.8f, 0.55f), 1.2f, 5.5f, true);
            Box(k, "Papers", new Vector3(2.3f, 0.76f, 2.0f), new Vector3(0.3f, 0.02f, 0.4f), VhWakingStyle.Paper, false, 0f, Quaternion.Euler(0f, 12f, 0f));
            Box(k, "Papers", new Vector3(2.45f, 0.77f, 1.75f), new Vector3(0.24f, 0.015f, 0.32f), new Color(0.84f, 0.82f, 0.74f), false, 0f, Quaternion.Euler(0f, -8f, 0f));
            Part(k, k.root, "Mags' mug", k.meshes.Cylinder(0.042f, 0.042f, 0.1f, 14), new Color(0.6f, 0.32f, 0.28f), new Vector3(2.2f, 0.75f, 2.5f), Quaternion.identity);
            Model(k, "radio", new Vector3(2.55f, 0.75f, 1.45f), -90f, 0.16f, false, false);
            Model(k, "coat_rack", new Vector3(2.55f, 0f, 0.45f), 0f, 1.7f);
            Model(k, "box", new Vector3(1.5f, 0f, 5.45f), 10f, 0.4f);
            Model(k, "box", new Vector3(1.55f, 0.4f, 5.5f), -5f, 0.32f, false, false);
            Model(k, "box", new Vector3(2.1f, 0f, 5.4f), 30f, 0.38f);
            Clock(k, new Vector3(0.9f, 2.25f, 0.06f), Vector3.forward, 11f);

            // The chair and its rig: a reclined seat, the halo over the head, cables to the console's screens.
            var chairCol = new Color(0.32f, 0.36f, 0.42f);
            var chair = new GameObject("The chair").transform;
            chair.SetParent(k.root, false);
            chair.localPosition = new Vector3(-1.1f, 0f, 4.1f);
            chair.localRotation = Quaternion.Euler(0f, 160f, 0f);
            Part(k, chair, "Base", k.meshes.Cylinder(0.28f, 0.2f, 0.32f, 18), VhWakingStyle.MetalDark, Vector3.zero, Quaternion.identity);
            Part(k, chair, "Seat", k.meshes.RoundedBox(new Vector3(0.62f, 0.14f, 0.62f), 0.06f, 4, 0.01f), chairCol, new Vector3(0f, 0.45f, 0f), Quaternion.identity);
            Part(k, chair, "Back", k.meshes.RoundedBox(new Vector3(0.62f, 0.12f, 0.88f), 0.06f, 4, 0.01f), chairCol, new Vector3(0f, 0.76f, -0.62f), Quaternion.Euler(-35f, 0f, 0f));
            Part(k, chair, "Headrest", k.meshes.RoundedBox(new Vector3(0.34f, 0.1f, 0.24f), 0.05f, 3), chairCol, new Vector3(0f, 1.08f, -1.02f), Quaternion.Euler(-35f, 0f, 0f));
            Part(k, chair, "Legrest", k.meshes.RoundedBox(new Vector3(0.56f, 0.12f, 0.62f), 0.05f, 3), chairCol, new Vector3(0f, 0.4f, 0.56f), Quaternion.Euler(15f, 0f, 0f));
            foreach (float x in new[] { -0.36f, 0.36f })
                Part(k, chair, "Armrest", k.meshes.RoundedBox(new Vector3(0.1f, 0.08f, 0.55f), 0.035f, 3), chairCol * 0.85f, new Vector3(x, 0.62f, -0.05f), Quaternion.identity);
            var halo = new List<Vector3>();
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f * Mathf.PI * 2f;
                halo.Add(new Vector3(Mathf.Cos(t) * 0.18f, Mathf.Sin(t) * 0.18f, 0f));
            }
            var rig = new GameObject("Rig").transform;
            rig.SetParent(chair, false);
            rig.localPosition = new Vector3(0f, 1.3f, -1.05f);
            rig.localRotation = Quaternion.Euler(55f, 0f, 0f);
            Part(k, rig, "Halo", k.meshes.Tube(halo, 0.018f, 8), VhWakingStyle.Metal, Vector3.zero, Quaternion.identity);
            Glow(k, "Halo lights", k.meshes.Tube(halo, 0.006f, 6), Color.black, new Color(0.45f, 0.65f, 1f) * 1.2f, new Vector3(0f, 0f, 0.012f), Quaternion.identity, rig);
            Part(k, chair, "Rig arm", k.meshes.Tube(new List<Vector3> { new Vector3(0.3f, 0.6f, -0.9f), new Vector3(0.3f, 1.45f, -1.25f), new Vector3(0.12f, 1.42f, -1.1f) }, 0.02f, 8), VhWakingStyle.MetalDark, Vector3.zero, Quaternion.identity);
            var chairBox = chair.gameObject.AddComponent<BoxCollider>();
            chairBox.center = new Vector3(0f, 0.5f, -0.2f);
            chairBox.size = new Vector3(0.8f, 1f, 1.8f);
            AddLight(k, "Rig glow", new Vector3(-1.0f, 1.4f, 5.0f), new Color(0.5f, 0.65f, 1f), 0.25f, 2f);
            AddLight(k, "Lab fill", new Vector3(-1.0f, 2.3f, 3.4f), new Color(0.6f, 0.7f, 0.95f), 0.35f, 4.5f);

            Box(k, "Console", new Vector3(-2.5f, 0.55f, 5.25f), new Vector3(0.75f, 1.1f, 0.55f), VhWakingStyle.MetalDark, true, 0.02f);
            Box(k, "Console top", new Vector3(-2.5f, 1.12f, 5.2f), new Vector3(0.8f, 0.04f, 0.62f), new Color(0.3f, 0.31f, 0.33f), false, 0.01f);
            for (int i = 0; i < 2; i++)
            {
                float x = -2.7f + i * 0.4f;
                Box(k, "Console screen", new Vector3(x, 1.38f, 5.35f), new Vector3(0.36f, 0.26f, 0.04f), VhWakingStyle.MetalDark, false, 0.01f);
                Box(k, "Console glass", new Vector3(x, 1.38f, 5.328f), new Vector3(0.32f, 0.22f, 0.002f), new Color(0.03f, 0.04f, 0.05f));
                var scr = new GameObject("Console traces").transform;
                scr.SetParent(k.root, false);
                scr.localPosition = new Vector3(x, 1.38f, 5.322f);
                scr.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Trace(k, scr, new Vector3(0f, 0.04f, 0f), new Color(0.4f, 0.6f, 1f) * 1.3f, i == 0 ? 2 : 1);
                Trace(k, scr, new Vector3(0f, -0.04f, 0f), new Color(0.6f, 0.5f, 1f) * 1.1f, 2);
            }
            for (int i = 0; i < 8; i++)
                Glow(k, "Console lamp", k.meshes.Box(new Vector3(0.025f, 0.012f, 0.025f)), Color.black, (i % 3 == 0 ? new Color(1f, 0.6f, 0.2f) : new Color(0.3f, 1f, 0.5f)) * 1.3f, new Vector3(-2.8f + i * 0.08f, 1.145f, 5.05f), Quaternion.identity);
            Part(k, k.root, "Cable", k.meshes.Tube(new List<Vector3> { new Vector3(-2.2f, 0.9f, 5.05f), new Vector3(-1.9f, 0.04f, 4.9f), new Vector3(-1.4f, 0.04f, 4.9f), new Vector3(-1.2f, 0.5f, 4.95f) }, 0.016f, 6), Shoe, Vector3.zero, Quaternion.identity, false);
            Part(k, k.root, "Cable", k.meshes.Tube(new List<Vector3> { new Vector3(-2.25f, 0.6f, 5.0f), new Vector3(-1.95f, 0.03f, 5.1f), new Vector3(-1.3f, 0.03f, 5.15f), new Vector3(-1.15f, 0.4f, 5.0f) }, 0.012f, 6), new Color(0.3f, 0.3f, 0.34f), Vector3.zero, Quaternion.identity, false);

            // The curtained bed in the corner (Mags: "Don't."), its curtain drawn all the way round.
            Box(k, "Curtained bed", new Vector3(-2.3f, 0.4f, 1.6f), new Vector3(0.9f, 0.5f, 1.9f), VhWakingStyle.Sheet * 0.8f, true, 0.04f);
            var trackB = new List<Vector3> { new Vector3(-2.95f, 2.5f, 2.75f), new Vector3(-1.65f, 2.5f, 2.75f), new Vector3(-1.65f, 2.5f, 0.5f), new Vector3(-2.95f, 2.5f, 0.5f) };
            Part(k, k.root, "Curtain track", k.meshes.Tube(trackB, 0.012f, 6), VhWakingStyle.Metal, Vector3.zero, Quaternion.identity, false);
            var curtain = new Color(0.52f, 0.58f, 0.56f);
            for (int i = 0; i < 14; i++)
                Box(k, "Curtain", new Vector3(-2.9f + i * 0.095f, 1.33f, 2.75f + (i % 2 == 0 ? 0.03f : -0.03f)), new Vector3(0.11f, 2.3f, 0.025f), curtain, false, 0f, Quaternion.Euler(0f, i % 2 == 0 ? 30f : -30f, 0f));
            for (int i = 0; i < 22; i++)
                Box(k, "Curtain", new Vector3(-1.65f + (i % 2 == 0 ? 0.03f : -0.03f), 1.33f, 2.7f - i * 0.1f), new Vector3(0.025f, 2.3f, 0.12f), curtain, false, 0f, Quaternion.Euler(0f, i % 2 == 0 ? 30f : -30f, 0f));
            var curtainWall = new GameObject("Curtain (collider)");
            curtainWall.transform.SetParent(k.root, false);
            curtainWall.transform.localPosition = new Vector3(-2.3f, 1.2f, 1.6f);
            curtainWall.AddComponent<BoxCollider>().size = new Vector3(1.4f, 2.4f, 2.4f);

            // Mags, by the desk.
            var mags = Person(k, "Mags", new Vector3(1.6f, 0f, 2.3f), 230f, 1.64f, Cardigan, new Color(0.3f, 0.28f, 0.26f), GreyHair, true, new Color(0.8f, 0.78f, 0.74f));
            Part(k, mags, "Glasses (left)", k.meshes.RoundedBox(new Vector3(0.05f, 0.032f, 0.006f), 0.008f, 2), new Color(0.55f, 0.5f, 0.45f), new Vector3(-0.033f, 1.565f, 0.112f), Quaternion.identity, false);
            Part(k, mags, "Glasses (right)", k.meshes.RoundedBox(new Vector3(0.05f, 0.032f, 0.006f), 0.008f, 2), new Color(0.55f, 0.5f, 0.45f), new Vector3(0.033f, 1.565f, 0.112f), Quaternion.identity, false);

            VhWakingStyle.SetAmbient(new Color(0.12f, 0.14f, 0.19f), new Color(0.06f, 0.06f, 0.07f), new Color(0.05f, 0.06f, 0.08f), 0f, 0.15f);
            a.spawn = new Vector3(-0.8f, 0.05f, 1.2f);
            a.spawnYaw = 45f;
        }
    }
}
