using JamKit.Look.EditorTools;
using JamKit.Movement;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The Lasname house as it stands on the hill (docs/redesign/house.md): <see cref="Build"/> builds the whole house in its
    /// own frame at the root's origin (the front door's threshold, the front facing -z): its structure inside and out, its
    /// furniture, its lamps and the outside details (the gables, the gutters, the tower's slate roof and finial, the porch
    /// lantern, the open front door), but not the lot's ground, fences or the street (World's HillAnchors: the hill builds
    /// those). The World's street builder calls <see cref="Build"/> by name. Standing alone, <see cref="Standalone"/> adds the
    /// stand-ins: the lot, Rowan Street, Chapel Row above the back fence with the dream's door Nadia bursts out of.
    /// </summary>
    public static class DreamHouseExterior
    {
        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>The whole house (no game wiring) at <paramref name="root"/>'s origin.</summary>
        public static void Build(Transform root)
        {
            var h = DreamHouse.Build();
            HouseMaterials.ClearFinishes();
            HouseGeometry.Build(h, root, false);
            HouseFurnishing.Build(h, root, false);
            HouseFurnishing.Lamps(h, root);
            Details(h, root);
        }

        /// <summary>The outside details over the structure.</summary>
        public static void Details(DreamHouse h, Transform root)
        {
            var b = new MeshBuilder { Kind = DrawnKind.Mounted, Owner = "Outside details" };
            var siding = HouseMaterials.Siding();
            var planks = HouseMaterials.Planks();
            var trim = HouseMaterials.TrimWhite();
            const float e = DreamHouse.ExtT * 0.5f;
            float slope = (DreamHouse.RidgeHeight - DreamHouse.KneeWall) / (DreamHouse.RidgeZ - DreamHouse.Z0);
            float Line(float z) => DreamHouse.Attic + (z < DreamHouse.Z0 ? DreamHouse.KneeWall - (DreamHouse.Z0 - z) * slope : z > DreamHouse.Z1 ? DreamHouse.KneeWall - (z - DreamHouse.Z1) * slope : DreamHouse.RoofAt(z));
            float knee = DreamHouse.Attic + DreamHouse.RoofAt(DreamHouse.Z0 - e) - 0.02f;
            // The gables: the triangles over the stepped walls, outside (clapboard) and inside (attic boards), a hair proud.
            void Gable(float x, float z0, float z1, int outward)
            {
                foreach (var (za, zb) in new[] { (z0, Mathf.Min(z1, DreamHouse.RidgeZ)), (Mathf.Max(z0, DreamHouse.RidgeZ), z1) })
                {
                    if (zb - za < 0.01f) continue;
                    float xo = x + outward * (e + 0.004f), xi = x - outward * (e + 0.004f);
                    var a = new Vector3(xo, knee, za); var bb = new Vector3(xo, Line(za) - 0.01f, za); var c = new Vector3(xo, Line(zb) - 0.01f, zb); var d = new Vector3(xo, knee, zb);
                    if (outward > 0) { b.Tri(a, bb, c, siding); b.Tri(a, c, d, siding); } else { b.Tri(d, c, bb, siding); b.Tri(d, bb, a, siding); }
                    var ai = new Vector3(xi, knee, za); var bi = new Vector3(xi, Line(za) - 0.01f, za); var ci = new Vector3(xi, Line(zb) - 0.01f, zb); var di = new Vector3(xi, knee, zb);
                    if (outward > 0) { b.Tri(di, ci, bi, planks); b.Tri(di, bi, ai, planks); } else { b.Tri(ai, bi, ci, planks); b.Tri(ai, ci, di, planks); }
                }
            }
            Gable(DreamHouse.X1, DreamHouse.Z0 + e, DreamHouse.Z1 - e, 1);
            Gable(DreamHouse.X0, DreamHouse.Z0 + e, DreamHouse.TowerZ0 - e, -1);
            Gable(DreamHouse.X0, DreamHouse.TowerZ1 + e, DreamHouse.Z1 - e, -1);
            // The gutters under the eaves, south and north, and their downpipes at the corners.
            float ge = Line(DreamHouse.Z0 - 0.6f) + 0.0f;
            var gutter = HouseMaterials.Paint("Gutter", new Color(0.92f, 0.92f, 0.9f));
            b.Box(new Vector3(DreamHouse.X0 - 0.4f, ge - 0.14f, DreamHouse.Z0 - 0.74f), new Vector3(DreamHouse.X1 + 0.4f, ge - 0.02f, DreamHouse.Z0 - 0.61f), gutter);
            b.Box(new Vector3(DreamHouse.X0 - 0.4f, ge - 0.14f, DreamHouse.Z1 + 0.61f), new Vector3(DreamHouse.DormerX0 - 0.1f, ge - 0.02f, DreamHouse.Z1 + 0.74f), gutter);
            b.Box(new Vector3(DreamHouse.DormerX1 + 0.1f, ge - 0.14f, DreamHouse.Z1 + 0.61f), new Vector3(DreamHouse.X1 + 0.4f, ge - 0.02f, DreamHouse.Z1 + 0.74f), gutter);
            foreach (float x in new[] { DreamHouse.X0 - 0.3f, DreamHouse.X1 + 0.25f })
            {
                b.Box(new Vector3(x - 0.04f, DreamHouse.Yard, DreamHouse.Z0 - 0.3f), new Vector3(x + 0.04f, ge - 0.14f, DreamHouse.Z0 - 0.22f), gutter);
            }
            // The tower's slate roof (a steep pyramid over its ceiling slab) and its brass finial.
            float tt = DreamHouse.Attic + DreamHouse.TowerH + 0.3f;
            var slate = HouseMaterials.Roof();
            Vector3 t0 = new Vector3(DreamHouse.X0 - e, tt, DreamHouse.TowerZ0 - e), t1 = new Vector3(DreamHouse.TowerX1 + e, tt, DreamHouse.TowerZ0 - e);
            Vector3 t2 = new Vector3(DreamHouse.TowerX1 + e, tt, DreamHouse.TowerZ1 + e), t3 = new Vector3(DreamHouse.X0 - e, tt, DreamHouse.TowerZ1 + e);
            var apex = new Vector3((t0.x + t1.x) * 0.5f, tt + 2.6f, (t0.z + t3.z) * 0.5f);
            b.Tri(t1, t0, apex, slate); b.Tri(t2, t1, apex, slate); b.Tri(t3, t2, apex, slate); b.Tri(t0, t3, apex, slate);
            var brass = HouseMaterials.Paint("Brass", new Color(0.78f, 0.6f, 0.3f), 1.7f);
            b.Box(apex + new Vector3(-0.06f, -0.05f, -0.06f), apex + new Vector3(0.06f, 0.7f, 0.06f), brass);
            // The porch light by the door (a real wall lantern: it was a black cube, house-1.md), the house number.
            var lantern = HouseMaterials.Model("porch.lantern", out string lanternId);
            if (lantern != null)
            {
                var go = HouseFurnishing.Fit(lantern, lanternId, root, new Vector3(1.03f, 1.9f, -0.42f), new Vector3(1.21f, 2.3f, -0.151f), 180f, "Porch lantern", true);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            b.Box(new Vector3(-1.05f, 1.95f, -0.17f), new Vector3(-0.8f, 2.12f, -0.15f), brass);
            b.Build("Outside details", root);
            FrontDoor(root);
        }

        /// <summary>The front door, standing open into the hall (a leaf with its collider, swung back against the hall's side).</summary>
        static void FrontDoor(Transform root)
        {
            var b = new MeshBuilder();
            var red = HouseMaterials.Paint("FrontDoorRed", new Color(0.66f, 0.16f, 0.14f), 1.4f);
            var mn = new Vector3(0.6f, 0f, 0.17f);
            var mx = new Vector3(0.65f, 2.22f, 1.37f);
            b.Box(mn, mx, red);
            b.Box(new Vector3(0.65f, 1.0f, 1.2f), new Vector3(0.69f, 1.06f, 1.26f), HouseMaterials.Paint("Brass", new Color(0.78f, 0.6f, 0.3f), 1.7f));
            var go = b.Build("The front door (open)", root);
            var c = go.AddComponent<BoxCollider>();
            c.center = (mn + mx) * 0.5f;
            c.size = mx - mn;
            go.AddComponent<MovementSurface>().runnable = false;
        }

        /// <summary>Theo's door, shut while his room is the base (the game opens it): a leaf in his doorway, with a collider.</summary>
        public static GameObject TheosDoor(Transform root)
        {
            var b = new MeshBuilder();
            var mn = new Vector3(-1.72f, DreamHouse.Upper, 1.0f);
            var mx = new Vector3(-1.68f, DreamHouse.Upper + 2.1f, 1.9f);
            b.Box(mn, mx, HouseMaterials.Paint("Doors", new Color(0.94f, 0.92f, 0.87f)));
            // A paper sign on it: KEEP OUT (BASE).
            b.Box(new Vector3(-1.68f, DreamHouse.Upper + 1.3f, 1.25f), new Vector3(-1.672f, DreamHouse.Upper + 1.6f, 1.65f), HouseMaterials.Paint("PaperSign", new Color(0.98f, 0.96f, 0.8f)));
            var go = b.Build("Theo's door (shut: the base)", root);
            var c = go.AddComponent<BoxCollider>();
            c.center = (mn + mx) * 0.5f;
            c.size = mx - mn;
            go.AddComponent<MovementSurface>().runnable = false;
            return go;
        }

        /// <summary>Standing alone: the hill round the house as a stand-in for World's (the ground all round, the terraces,
        /// the far ring) and Chapel Row above the back fence, a real street, with the dream's door she bursts out of
        /// (<see cref="HouseEscapeChapelRow"/>).</summary>
        public static Transform Standalone(DreamHouse h, Transform root) => HouseEscapeChapelRow.Build(h, root);
    }
}
