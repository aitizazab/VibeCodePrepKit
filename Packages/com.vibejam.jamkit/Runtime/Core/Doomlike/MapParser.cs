using System;
using System.Collections.Generic;
using System.Globalization;

namespace JamKit.Doomlike
{
    /// <summary>
    /// Reads a level text file: an optional header of <c>key: value</c> lines, a line of three
    /// dashes, then the map, one character per cell:
    /// <code>
    /// name: The Pump Station
    /// subtitle: Find the red key
    /// theme: rust
    /// ceiling: 3.5
    /// ---
    /// #########
    /// #P..m..E#
    /// #########
    /// </code>
    /// Header keys: name, subtitle, theme (or palette), ceiling, light, par (seconds or m:ss), music,
    /// facing (north/east/south/west or degrees). <c>//</c> starts a comment line in the header.
    /// Short rows are padded with nothing (space). Errors name the row and column as a text editor
    /// shows them.
    /// </summary>
    public static class MapParser
    {
        public const int MaxSize = 160;
        public const string Separator = "---";

        public static MapData Parse(string text, MapLegend legend, MapProblems problems)
        {
            if (legend == null) throw new ArgumentNullException(nameof(legend));
            if (problems == null) problems = new MapProblems();
            var map = new MapData();
            if (string.IsNullOrEmpty(text))
            {
                problems.Error("The map is empty.");
                return map;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int sep = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Trim() == Separator) { sep = i; break; }

            int first = 0;
            if (sep >= 0)
            {
                for (int i = 0; i < sep; i++) ReadHeaderLine(lines[i], i, map, problems);
                first = sep + 1;
            }

            // The map rows: skip blank lines before and after, keep blank lines inside (void rows).
            int last = lines.Length - 1;
            while (first <= last && lines[first].Trim().Length == 0) first++;
            while (last >= first && lines[last].Trim().Length == 0) last--;
            if (first > last)
            {
                problems.Error("There are no map rows" + (sep >= 0 ? " after the --- line." : "."));
                return map;
            }

            int height = last - first + 1;
            int width = 0;
            for (int i = first; i <= last; i++) width = Math.Max(width, lines[i].TrimEnd().Length);
            if (width > MaxSize || height > MaxSize)
            {
                problems.Error($"The map is {width} x {height}; the most is {MaxSize} x {MaxSize}.");
                return map;
            }

            map.width = width;
            map.height = height;
            map.cells = new MapCell[width * height];
            legend.TryGet(MapLegend.Void, out var voidEntry);
            int starts = 0;
            for (int y = 0; y < height; y++)
            {
                string row = lines[first + y].TrimEnd();
                for (int x = 0; x < width; x++)
                {
                    char c = x < row.Length ? row[x] : MapLegend.Void;
                    LegendEntry entry;
                    if (c == '\t')
                    {
                        problems.Error($"A tab at {MapData.Where(x, y)}: use spaces (or nothing) instead.");
                        entry = voidEntry;
                        c = MapLegend.Void;
                    }
                    else if (!legend.TryGet(c, out entry))
                    {
                        problems.Error($"Unknown character '{c}' at {MapData.Where(x, y)}.");
                        entry = voidEntry;
                        c = MapLegend.Void;
                    }
                    map.cells[y * width + x] = new MapCell { ch = c, entry = entry };
                    var thing = new MapThing { ch = c, x = x, y = y, entry = entry };
                    switch (entry.kind)
                    {
                        case CellKind.Start:
                            starts++;
                            if (starts == 1) { map.startX = x; map.startY = y; }
                            break;
                        case CellKind.Exit:
                            map.exits.Add(thing);
                            break;
                        case CellKind.Entity:
                        case CellKind.Key:
                            map.things.Add(thing);
                            break;
                    }
                }
            }

            if (starts == 0) problems.Error($"No player start: put one '{MapLegend.Start}' on the map.");
            else if (starts > 1) problems.Error($"{starts} player starts ('{MapLegend.Start}'); there must be exactly one.");
            if (map.exits.Count == 0) problems.Error($"No exit: put at least one '{MapLegend.Exit}' on the map.");
            return map;
        }

        static void ReadHeaderLine(string raw, int index, MapData map, MapProblems problems)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) return;
            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                problems.Warn($"Header line {index + 1} isn't 'key: value' and was ignored: \"{line}\".");
                return;
            }
            string key = line.Substring(0, colon).Trim().ToLowerInvariant();
            string value = line.Substring(colon + 1).Trim();
            switch (key)
            {
                case "name":
                case "title":
                    map.name = value;
                    break;
                case "subtitle":
                case "objective":
                    map.subtitle = value;
                    break;
                case "theme":
                case "palette":
                    map.theme = value;
                    break;
                case "music":
                    map.music = value;
                    break;
                case "ceiling":
                    if (TryFloat(value, out float ceiling) && ceiling >= 2f && ceiling <= 20f) map.ceiling = ceiling;
                    else problems.Warn($"Header 'ceiling' should be metres between 2 and 20, not \"{value}\".");
                    break;
                case "light":
                    if (TryFloat(value, out float light) && light >= 0.2f && light <= 1.5f) map.light = light;
                    else problems.Warn($"Header 'light' should be between 0.2 and 1.5, not \"{value}\".");
                    break;
                case "par":
                    if (TryTime(value, out float par)) map.parSeconds = par;
                    else problems.Warn($"Header 'par' should be seconds or m:ss, not \"{value}\".");
                    break;
                case "facing":
                    if (MapData.TryParseFacing(value, out float facing)) map.startFacingDegrees = facing;
                    else problems.Warn($"Header 'facing' should be north, east, south, west or degrees, not \"{value}\".");
                    break;
                default:
                    problems.Warn($"Unknown header key '{key}' (line {index + 1}) was ignored.");
                    break;
            }
        }

        static bool TryFloat(string s, out float v) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>"95", "95.5" or "1:35".</summary>
        public static bool TryTime(string s, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            int colon = s.IndexOf(':');
            if (colon < 0) return TryFloat(s, out seconds) && seconds >= 0f;
            if (!int.TryParse(s.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int m)) return false;
            if (!TryFloat(s.Substring(colon + 1), out float sec)) return false;
            seconds = m * 60f + sec;
            return m >= 0 && sec >= 0f;
        }

        /// <summary>The map rows back as text (for tools and tests).</summary>
        public static List<string> Rows(MapData map)
        {
            var rows = new List<string>(map.height);
            var chars = new char[map.width];
            for (int y = 0; y < map.height; y++)
            {
                for (int x = 0; x < map.width; x++) chars[x] = map.cells[y * map.width + x].ch;
                rows.Add(new string(chars).TrimEnd());
            }
            return rows;
        }
    }
}
