using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using JamKit.DreamEscape;

// Night 2's house plan as JSON: the rooms, the main floor's and the attic's pieces, Theo's walking graph (with its
// edges), the hiding spots, the memories, the stairs and the hatch. For tools/art/vh_house_plan.py.
static class Program
{
    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => "[" + F(v.X) + "," + F(v.Y) + "," + F(v.Z) + "]";
    static string S(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    static int Main(string[] args)
    {
        var h = DreamHouse.Build();
        var sb = new StringBuilder("{");
        sb.Append("\"ground\":" + F(DreamHouse.Ground) + ",\"attic\":" + F(DreamHouse.AtticFloor) + ",");
        sb.Append("\"hub\":[" + F(DreamHouse.HubX0) + "," + F(DreamHouse.HubX1) + "," + F(DreamHouse.HubZ0) + "," + F(DreamHouse.HubZ1) + "],");
        sb.Append("\"hatch\":[" + F(DreamHouse.HatchX0) + "," + F(DreamHouse.HatchX1) + "," + F(DreamHouse.HatchZ0) + "," + F(DreamHouse.HatchZ1) + "],");
        sb.Append("\"stairs\":[" + F(DreamHouse.StairX0) + "," + F(DreamHouse.StairX1) + "," + F(DreamHouse.StairZ0) + "," + F(DreamHouse.StairZ1) + "],");
        sb.Append("\"ridgeZ\":" + F(DreamHouse.RidgeZ) + ",\"dormer\":[" + F(DreamHouse.DormerX0) + "," + F(DreamHouse.DormerX1) + "," + F(DreamHouse.DormerZ0) + "],\"obsX1\":" + F(DreamHouse.ObsX1) + ",\"obsHeight\":" + F(DreamHouse.ObsHeight) + ",\"obsZ\":[" + F(DreamHouse.ObsZ0) + "," + F(DreamHouse.ObsZ1) + "],\"sky\":[" + F(DreamHouse.SkyX) + "," + F(DreamHouse.SkyZ) + "," + F(DreamHouse.SkyR) + "],");
        sb.Append("\"rooms\":[");
        for (int i = 0; i < h.rooms.Count; i++)
        {
            var r = h.rooms[i];
            sb.Append((i > 0 ? "," : "") + "{\"name\":" + S(r.name) + ",\"x0\":" + F(r.x0) + ",\"x1\":" + F(r.x1) + ",\"z0\":" + F(r.z0) + ",\"z1\":" + F(r.z1) + ",\"floor\":" + F(r.floorY) + ",\"ceiling\":" + F(r.ceiling) + "}");
        }
        sb.Append("],\"pieces\":[");
        bool first = true;
        for (int i = 0; i < h.course.pieces.Count; i++)
        {
            var p = h.course.pieces[i];
            if (p.Max.Z < DreamHouse.HubZ0 - 1.5f || p.Min.Z > DreamHouse.HubZ1 + 0.5f) continue;
            sb.Append((first ? "" : ",") + "{\"name\":" + S(p.name) + ",\"look\":" + S(h.looks[i].ToString()) + ",\"kind\":" + S(p.kind.ToString()) + ",\"solid\":" + (p.solid ? "true" : "false") + ",\"min\":" + V(p.Min) + ",\"max\":" + V(p.Max) + "}");
            first = false;
        }
        sb.Append("],\"nodes\":[");
        for (int i = 0; i < h.nodes.Count; i++) sb.Append((i > 0 ? "," : "") + V(h.nodes[i]));
        var world = h.StealthWorld();
        var g = h.Graph(world);
        sb.Append("],\"edges\":[");
        first = true;
        for (int a = 0; a < g.Count; a++)
            foreach (int b in g.Neighbours(a))
                if (b > a) { sb.Append((first ? "" : ",") + "[" + a + "," + b + "]"); first = false; }
        sb.Append("],\"spots\":[");
        for (int i = 0; i < h.spots.Count; i++)
        {
            var s = h.spots[i];
            sb.Append((i > 0 ? "," : "") + "{\"name\":" + S(s.name) + ",\"kind\":" + S(s.kind.ToString()) + ",\"center\":" + V(s.Center) + ",\"size\":" + V(s.Size) + ",\"approach\":" + V(s.Approach) + "}");
        }
        sb.Append("],\"memories\":[");
        for (int i = 0; i < h.memories.Count; i++)
        {
            var m = h.memories[i];
            sb.Append((i > 0 ? "," : "") + "{\"name\":" + S(m.name) + ",\"at\":" + V(m.position) + ",\"stand\":" + V(m.standAt) + "}");
        }
        sb.Append("],\"theo\":" + V(h.theoStart) + ",\"lena\":" + V(h.lenaAt) + ",\"arrive\":" + V(h.kitchenStart) + "}");
        File.WriteAllText(args.Length > 0 ? args[0] : "house-plan.json", sb.ToString());
        Console.WriteLine("house plan written");
        return 0;
    }
}
