using System;
using System.Collections.Generic;

namespace JamKit.Doomlike
{
    /// <summary>One cell of a parsed map.</summary>
    public struct MapCell
    {
        public char ch;
        public LegendEntry entry;

        public CellKind Kind => entry.kind;
    }

    /// <summary>A thing placed on the map: an entity, a key, the start or an exit, at a cell.</summary>
    public struct MapThing
    {
        public char ch;
        public int x, y;
        public LegendEntry entry;
    }

    /// <summary>
    /// A parsed level: the grid (x = column from the left, y = row from the top of the file), the
    /// header values and the things on it. World space: +X is east (right in the file), +Z north
    /// (up in the file); see <see cref="GridMath"/>.
    /// </summary>
    public sealed class MapData
    {
        public string name = "Untitled";
        /// <summary>Second line of the level card ("Find the red key").</summary>
        public string subtitle = "";
        /// <summary>A theme name or index from the game's list (wall tints, fog). Empty = the first.</summary>
        public string theme = "";
        /// <summary>Music id; empty = the game's default.</summary>
        public string music = "";
        /// <summary>Ceiling height in metres; 0 = the game's default.</summary>
        public float ceiling;
        /// <summary>Brightness, 0.2..1.5 (1 = normal).</summary>
        public float light = 1f;
        /// <summary>Par time in seconds for the tally; 0 = none.</summary>
        public float parSeconds;
        /// <summary>Start facing as a 2D angle in degrees (0 = east, 90 = north); NaN = automatic
        /// (the longest open view from the start).</summary>
        public float startFacingDegrees = float.NaN;

        public int width, height;
        public MapCell[] cells = new MapCell[0];
        public readonly List<MapThing> things = new List<MapThing>();
        public int startX = -1, startY = -1;
        public readonly List<MapThing> exits = new List<MapThing>();

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        /// <summary>The cell, or a Void cell outside the map.</summary>
        public MapCell Get(int x, int y)
        {
            if (!InBounds(x, y)) return new MapCell { ch = ' ', entry = new LegendEntry(CellKind.Void, 0, true) };
            return cells[y * width + x];
        }

        public CellKind KindAt(int x, int y) => Get(x, y).entry.kind;

        /// <summary>Floor and ceiling are drawn here (floor, doors, things...).</summary>
        public bool IsOpen(int x, int y) => Get(x, y).entry.IsOpenSpace;

        /// <summary>Solid for the purpose of drawing walls: walls, void and outside the map. Doors,
        /// secret doors and closet walls are separate moving objects, so they count as open.</summary>
        public bool IsSolidGeometry(int x, int y) => !IsOpen(x, y);

        /// <summary>The wall texture to show at a solid cell (void and outside use texture 0).</summary>
        public int WallVariantAt(int x, int y)
        {
            var c = Get(x, y);
            return c.entry.kind == CellKind.Wall ? c.entry.variant : 0;
        }

        public int Count(CellKind kind)
        {
            int n = 0;
            for (int i = 0; i < cells.Length; i++) if (cells[i].entry.kind == kind) n++;
            return n;
        }

        public int CountThings(EntityCategory category)
        {
            int n = 0;
            for (int i = 0; i < things.Count; i++)
                if (things[i].entry.kind == CellKind.Entity && things[i].entry.category == category) n++;
            return n;
        }

        /// <summary>"row 3, column 7" (1-based, as a text editor shows it).</summary>
        public static string Where(int x, int y) => "row " + (y + 1) + ", column " + (x + 1);

        /// <summary>The start facing in degrees: the header's, or the cardinal direction with the
        /// longest run of open cells from the start (ties: east, north, west, south).</summary>
        public float ResolveStartFacingDegrees()
        {
            if (!float.IsNaN(startFacingDegrees)) return startFacingDegrees;
            if (startX < 0) return 90f;
            int best = -1;
            float facing = 90f;
            int[] dx = { 1, 0, -1, 0 };
            int[] dy = { 0, -1, 0, 1 }; // north is up in the file (y - 1)
            float[] angle = { 0f, 90f, 180f, 270f };
            for (int d = 0; d < 4; d++)
            {
                int run = 0;
                int x = startX + dx[d], y = startY + dy[d];
                while (IsOpen(x, y) && run < 256)
                {
                    run++;
                    x += dx[d];
                    y += dy[d];
                }
                if (run > best)
                {
                    best = run;
                    facing = angle[d];
                }
            }
            return facing;
        }

        /// <summary>Parses "north", "e", "south-west" or a number of degrees (0 = east, 90 = north).</summary>
        public static bool TryParseFacing(string text, out float degrees)
        {
            degrees = float.NaN;
            if (string.IsNullOrEmpty(text)) return false;
            string t = text.Trim().ToLowerInvariant().Replace("-", "").Replace(" ", "");
            switch (t)
            {
                case "e": case "east": degrees = 0f; return true;
                case "ne": case "northeast": degrees = 45f; return true;
                case "n": case "north": degrees = 90f; return true;
                case "nw": case "northwest": degrees = 135f; return true;
                case "w": case "west": degrees = 180f; return true;
                case "sw": case "southwest": degrees = 225f; return true;
                case "s": case "south": degrees = 270f; return true;
                case "se": case "southeast": degrees = 315f; return true;
            }
            return float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out degrees);
        }

        public override string ToString() => name + " (" + width + "x" + height + ")";
    }

    /// <summary>Errors stop a level from loading; warnings are worth a look.</summary>
    public sealed class MapProblems
    {
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();

        public bool Ok => errors.Count == 0;

        public void Error(string s) => errors.Add(s);
        public void Warn(string s) => warnings.Add(s);

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (var e in errors) parts.Add("ERROR: " + e);
            foreach (var w in warnings) parts.Add("warning: " + w);
            return string.Join(Environment.NewLine, parts);
        }
    }
}
