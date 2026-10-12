using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using JamKit.DreamEscape;

// Night 2's house (core DreamHouse, docs/redesign/house.md) as JSON for tools/art/vh_house_plan.py: the storeys and
// their rooms, every part (walls, floors, stairs, furniture, the lot), the doors, windows and stairs, the hiding spots,
// Theo's walking nodes and their links, the memories and where the house re-hides them, the lamps, the story's points,
// the lint's overlaps; and the escape's rooms, corridor and beats along its route.
static class Program
{
    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => "[" + F(v.X) + "," + F(v.Y) + "," + F(v.Z) + "]";
    static string S(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string B(bool b) => b ? "true" : "false";

    static void Items<T>(StringBuilder sb, string key, IList<T> items, Func<T, string> one)
    {
        sb.Append(S(key) + ":[");
        for (int i = 0; i < items.Count; i++) sb.Append((i > 0 ? "," : "") + one(items[i]));
        sb.Append("],");
    }

    static int Main(string[] args)
    {
        var h = DreamHouse.Build();
        var e = HouseEscape.Build();
        var sb = new StringBuilder("{");
        sb.Append("\"storeys\":[[\"Basement\"," + F(DreamHouse.Basement) + "],[\"Ground floor\"," + F(DreamHouse.Ground) + "],[\"Upper floor\"," + F(DreamHouse.Upper) + "],[\"Attic\"," + F(DreamHouse.Attic) + "]],");
        sb.Append("\"footprint\":[" + F(DreamHouse.X0) + "," + F(DreamHouse.X1) + "," + F(DreamHouse.Z0) + "," + F(DreamHouse.Z1) + "],");
        sb.Append("\"garageX1\":" + F(DreamHouse.GarageX1) + ",");
        sb.Append("\"yard\":[" + F(DreamHouse.YardX0) + "," + F(DreamHouse.YardX1) + "," + F(DreamHouse.FrontZ0) + "," + F(DreamHouse.YardZ1) + "],\"streetZ0\":" + F(DreamHouse.StreetZ0) + ",");
        Items(sb, "rooms", h.rooms, r =>
        {
            var rs = new StringBuilder("[");
            for (int i = 0; i < r.rects.Count; i++) rs.Append((i > 0 ? "," : "") + "[" + F(r.rects[i].x0) + "," + F(r.rects[i].x1) + "," + F(r.rects[i].z0) + "," + F(r.rects[i].z1) + "]");
            rs.Append("]");
            return "{\"id\":" + S(r.id) + ",\"name\":" + S(r.name) + ",\"storey\":" + r.storey + ",\"floor\":" + F(r.floorY) + ",\"ceiling\":" + F(r.ceilingY) + ",\"outside\":" + B(r.outside) + ",\"style\":" + S(r.style) + ",\"rects\":" + rs + "}";
        });
        Items(sb, "parts", h.parts, p => "{\"name\":" + S(p.piece.name) + ",\"look\":" + S(p.look.ToString()) + ",\"kind\":" + S(p.piece.kind.ToString()) + ",\"room\":" + S(p.room) + ",\"model\":" + S(p.model) + ",\"solid\":" + B(p.piece.solid) + ",\"runnable\":" + B(p.piece.runnable) + ",\"min\":" + V(p.piece.Min) + ",\"max\":" + V(p.piece.Max) + "}");
        Items(sb, "doors", h.doors, d => "{\"name\":" + S(d.name) + ",\"alongX\":" + B(d.alongX) + ",\"c\":" + F(d.c) + ",\"a\":" + F(d.a) + ",\"b\":" + F(d.b) + ",\"y0\":" + F(d.y0) + ",\"y1\":" + F(d.y1) + ",\"exterior\":" + B(d.exterior) + "}");
        Items(sb, "windows", h.windows, d => "{\"name\":" + S(d.name) + ",\"alongX\":" + B(d.alongX) + ",\"c\":" + F(d.c) + ",\"a\":" + F(d.a) + ",\"b\":" + F(d.b) + ",\"y0\":" + F(d.y0) + ",\"y1\":" + F(d.y1) + ",\"exterior\":" + B(d.exterior) + "}");
        Items(sb, "stairs", h.stairs, s => "{\"name\":" + S(s.name) + ",\"rect\":[" + F(s.rect.x0) + "," + F(s.rect.x1) + "," + F(s.rect.z0) + "," + F(s.rect.z1) + "],\"bottom\":" + F(s.bottomY) + ",\"top\":" + F(s.topY) + ",\"alongX\":" + B(s.alongX) + ",\"up\":" + s.up + "}");
        var spots = new List<int>();
        for (int i = 0; i < h.spots.Count; i++) spots.Add(i);
        Items(sb, "spots", spots, i => "{\"name\":" + S(h.spots[i].name) + ",\"kind\":" + S(h.spots[i].kind.ToString()) + ",\"room\":" + S(i < h.spotRooms.Count ? h.spotRooms[i] : null) + ",\"center\":" + V(h.spots[i].Center) + ",\"size\":" + V(h.spots[i].Size) + "}");
        Items(sb, "nodes", h.nodes, n => "{\"p\":" + V(n.p) + ",\"room\":" + S(n.room) + "}");
        Items(sb, "links", h.links, l => "[" + l.a + "," + l.b + "]");
        Items(sb, "memories", h.memories, m =>
        {
            var alts = new StringBuilder("[");
            for (int i = 0; i < m.altPositions.Count; i++) alts.Append((i > 0 ? "," : "") + "{\"p\":" + V(m.altPositions[i]) + ",\"room\":" + S(i < m.altRooms.Count ? m.altRooms[i] : null) + "}");
            alts.Append("]");
            return "{\"id\":" + S(m.id) + ",\"name\":" + S(m.name) + ",\"room\":" + S(m.room) + ",\"p\":" + V(m.position) + ",\"standAt\":" + V(m.standAt) + ",\"atTable\":" + B(m.atTable) + ",\"alts\":" + alts + "}";
        });
        Items(sb, "lamps", h.lamps, l => "{\"name\":" + S(l.name) + ",\"kind\":" + S(l.kind) + ",\"p\":" + V(l.position) + "}");
        sb.Append("\"points\":{\"arrival\":" + V(h.arrival) + ",\"frontDoor\":" + V(h.frontDoor) + ",\"table\":" + V(h.table) + ",\"nadiaChair\":" + V(h.nadiaChair)
            + ",\"theoStart\":" + V(h.theoStart) + ",\"lenaAt\":" + V(h.lenaAt) + ",\"lenaWindow\":" + V(h.lenaWindow) + ",\"exitDoor\":" + V(h.exitDoor) + ",\"trampoline\":" + V(h.trampoline) + "},");
        var lint = HouseLint.Check(h.parts);
        Items(sb, "lint", lint, x => S(HouseLint.Describe(h.parts, x)));
        // The escape, along its route s.
        sb.Append("\"escape\":{");
        Items(sb, "rooms", e.rooms, r => "{\"name\":" + S(r.name) + ",\"theme\":" + S(r.theme) + ",\"s0\":" + F(r.s0) + ",\"s1\":" + F(r.s1) + ",\"floor\":" + F(r.floorTop) + ",\"ceiling\":" + F(r.ceilingY) + ",\"roll\":" + F(r.roll) + ",\"open\":" + B(r.open) + "}");
        Items(sb, "beats", e.beats, b => "{\"name\":" + S(b.name) + ",\"kind\":" + S(b.kind) + ",\"s\":" + F(b.s) + ",\"value\":" + F(b.value) + ",\"seconds\":" + F(b.seconds) + "}");
        Items(sb, "checkpoints", e.course.checkpoints, c => "{\"name\":" + S(c.name) + ",\"s\":" + F(c.s) + "}");
        var co = e.corridor;
        sb.Append("\"corridor\":{\"startS\":" + F(co.startS) + ",\"triggerS\":" + F(co.triggerS) + ",\"doorS\":" + F(co.doorS) + ",\"bay\":" + F(co.bay) + ",\"slope\":" + F(co.slope) + ",\"threshold\":" + F(co.threshold) + "},");
        sb.Append("\"frontDoorS\":" + F(e.frontDoorS) + ",\"killY\":" + F(e.course.killY) + "}");
        sb.Append("}");
        string outPath = args.Length > 0 ? args[0] : "house-plan.json";
        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine("Wrote " + outPath + " (" + h.rooms.Count + " rooms, " + h.parts.Count + " parts, " + h.spots.Count + " hiding spots, " + h.memories.Count + " memories, " + lint.Count + " overlaps)");
        return lint.Count == 0 ? 0 : 2;
    }
}
