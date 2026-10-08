using System;
using System.Collections.Generic;

namespace JamKit.Doomlike
{
    /// <summary>What one character of a level map means.</summary>
    public enum CellKind
    {
        /// <summary>Nothing (a space): solid, never drawn. Walls are drawn where floor meets it.</summary>
        Void,
        /// <summary>A solid wall; the variant is the wall texture (0..3).</summary>
        Wall,
        /// <summary>Walkable floor; the variant is the floor texture (0 normal, 1 alternative).</summary>
        Floor,
        /// <summary>A door that opens when someone walks up to it.</summary>
        Door,
        /// <summary>A door that needs the key of its colour (the variant).</summary>
        LockedDoor,
        /// <summary>Looks like a wall; slides open when used or walked into.</summary>
        SecretDoor,
        /// <summary>A wall that sinks into the floor when a nearby ambush trigger fires (a monster closet).</summary>
        ClosetWall,
        /// <summary>A key card on the floor; the variant is its colour.</summary>
        Key,
        Start,
        Exit,
        /// <summary>Floor that hurts (slime, lava).</summary>
        Hazard,
        /// <summary>Floor that counts as a found secret when entered.</summary>
        Secret,
        /// <summary>Floor that springs an ambush: opens nearby closet walls and wakes nearby enemies.</summary>
        Trigger,
        /// <summary>Floor with a thing on it (enemy, pickup, weapon, decoration); the variant is the
        /// thing's index in the game's lists, the category says which list.</summary>
        Entity,
    }

    public enum KeyColor { Red, Blue, Yellow }

    public enum EntityCategory { None, Enemy, Pickup, Weapon, Decoration }

    /// <summary>The meaning of one map character.</summary>
    public struct LegendEntry
    {
        public CellKind kind;
        /// <summary>Wall or floor texture, key colour, or entity index, depending on the kind.</summary>
        public int variant;
        public EntityCategory category;
        /// <summary>Blocks walking (walls, void, solid decorations). Doors don't: they open.</summary>
        public bool solid;
        /// <summary>For the legend listing in docs and error messages ("red key card").</summary>
        public string description;

        public LegendEntry(CellKind kind, int variant = 0, bool solid = false, string description = "",
            EntityCategory category = EntityCategory.None)
        {
            this.kind = kind;
            this.variant = variant;
            this.solid = solid;
            this.description = description;
            this.category = category;
        }

        /// <summary>Floor, doors and things on the floor: geometry draws floor and ceiling here.</summary>
        public bool IsOpenSpace => kind != CellKind.Void && kind != CellKind.Wall;
    }

    /// <summary>
    /// Map characters to meanings. <see cref="Default"/> has the geometry characters every level
    /// shares (walls, floor, doors, keys, start, exit, secrets, ambushes); the game adds one
    /// character per enemy, pickup, weapon and decoration from its data with <see cref="AddEntity"/>.
    /// </summary>
    public sealed class MapLegend
    {
        public const char Void = ' ';
        public const char Start = 'P';
        public const char Exit = 'E';

        readonly Dictionary<char, LegendEntry> _entries = new Dictionary<char, LegendEntry>();

        public IEnumerable<KeyValuePair<char, LegendEntry>> Entries => _entries;

        public static readonly string KeyChars = "rby";
        public static readonly string LockedDoorChars = "RBY";

        public static MapLegend Default()
        {
            var l = new MapLegend();
            l.Set(' ', new LegendEntry(CellKind.Void, 0, true, "nothing (solid)"));
            l.Set('#', new LegendEntry(CellKind.Wall, 0, true, "wall (texture 1)"));
            l.Set('%', new LegendEntry(CellKind.Wall, 1, true, "wall (texture 2)"));
            l.Set('&', new LegendEntry(CellKind.Wall, 2, true, "wall (texture 3)"));
            l.Set('=', new LegendEntry(CellKind.Wall, 3, true, "wall (texture 4)"));
            l.Set('.', new LegendEntry(CellKind.Floor, 0, false, "floor"));
            l.Set(',', new LegendEntry(CellKind.Floor, 1, false, "floor (other tiles)"));
            l.Set('~', new LegendEntry(CellKind.Hazard, 0, false, "slime: hurts to stand in"));
            l.Set('D', new LegendEntry(CellKind.Door, 0, false, "door"));
            l.Set('R', new LegendEntry(CellKind.LockedDoor, (int)KeyColor.Red, false, "red door (needs the red key)"));
            l.Set('B', new LegendEntry(CellKind.LockedDoor, (int)KeyColor.Blue, false, "blue door (needs the blue key)"));
            l.Set('Y', new LegendEntry(CellKind.LockedDoor, (int)KeyColor.Yellow, false, "yellow door (needs the yellow key)"));
            l.Set('r', new LegendEntry(CellKind.Key, (int)KeyColor.Red, false, "red key"));
            l.Set('b', new LegendEntry(CellKind.Key, (int)KeyColor.Blue, false, "blue key"));
            l.Set('y', new LegendEntry(CellKind.Key, (int)KeyColor.Yellow, false, "yellow key"));
            l.Set('?', new LegendEntry(CellKind.SecretDoor, 0, false, "secret door (looks like a wall)"));
            l.Set('S', new LegendEntry(CellKind.Secret, 0, false, "secret spot (counts as a secret)"));
            l.Set('!', new LegendEntry(CellKind.Trigger, 0, false, "ambush trigger"));
            l.Set('|', new LegendEntry(CellKind.ClosetWall, 0, false, "closet wall (drops when an ambush fires)"));
            l.Set(Start, new LegendEntry(CellKind.Start, 0, false, "player start"));
            l.Set(Exit, new LegendEntry(CellKind.Exit, 0, false, "exit"));
            return l;
        }

        public void Set(char c, LegendEntry entry) => _entries[c] = entry;

        /// <summary>Adds a thing character. Returns false (and changes nothing) if the character is
        /// already taken, so two assets can't silently share one.</summary>
        public bool AddEntity(char c, EntityCategory category, int index, bool solid, string description)
        {
            if (_entries.ContainsKey(c)) return false;
            _entries[c] = new LegendEntry(CellKind.Entity, index, solid, description, category);
            return true;
        }

        public bool TryGet(char c, out LegendEntry entry) => _entries.TryGetValue(c, out entry);

        public bool Contains(char c) => _entries.ContainsKey(c);

        public static string KeyName(int color)
        {
            switch ((KeyColor)color)
            {
                case KeyColor.Red: return "red";
                case KeyColor.Blue: return "blue";
                case KeyColor.Yellow: return "yellow";
                default: return "unknown";
            }
        }

        /// <summary>A copy, so a test or tool can add to it without touching the original.</summary>
        public MapLegend Clone()
        {
            var l = new MapLegend();
            foreach (var pair in _entries) l._entries[pair.Key] = pair.Value;
            return l;
        }

        public static bool IsKeyColor(int v) => v >= 0 && v <= 2;

        /// <summary>Throws on characters a text editor would mangle (tabs, newlines).</summary>
        public static void CheckUsable(char c)
        {
            if (c == '\t' || c == '\n' || c == '\r') throw new ArgumentException("Tabs and newlines can't be map characters.");
        }
    }
}
