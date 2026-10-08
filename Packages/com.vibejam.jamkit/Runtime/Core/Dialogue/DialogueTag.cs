using System;
using System.Globalization;

namespace JamKit.Dialogue
{
    /// <summary>
    /// One ink tag split into a key and a value: "#speaker: Ava" gives key "speaker", value "Ava".
    /// Keys are lower-cased; a tag without a colon ("#flash") has an empty value.
    /// </summary>
    public struct DialogueTag
    {
        public string key;
        public string value;

        public DialogueTag(string key, string value)
        {
            this.key = key;
            this.value = value;
        }

        /// <summary>False for null, empty or whitespace tags.</summary>
        public static bool TryParse(string raw, out DialogueTag tag)
        {
            tag = default;
            if (raw == null) return false;
            string s = raw.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1).Trim();
            if (s.Length == 0) return false;
            int colon = s.IndexOf(':');
            if (colon < 0)
            {
                tag = new DialogueTag(s.ToLowerInvariant(), "");
                return true;
            }
            string key = s.Substring(0, colon).Trim().ToLowerInvariant();
            if (key.Length == 0) return false;
            tag = new DialogueTag(key, s.Substring(colon + 1).Trim());
            return true;
        }

        /// <summary>The value as a number (invariant culture: "0.5", never "0,5"), or the fallback.</summary>
        public float FloatOr(float fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)
                && !float.IsNaN(f) && !float.IsInfinity(f) ? f : fallback;
        }

        public override string ToString() => value.Length == 0 ? key : key + ":" + value;
    }

    public enum PortraitSide { Keep, Left, Right }

    /// <summary>
    /// "#portrait: Ava happy left": character id, then any of an expression name, a side (left or
    /// right) and "hide", in any order. Without a side the portrait stays where it was (left the
    /// first time); without an expression it shows the base sprite only.
    /// </summary>
    public struct PortraitCommand
    {
        public string character;
        /// <summary>Empty = the base sprite with no expression layer.</summary>
        public string expression;
        public PortraitSide side;
        public bool hide;

        public static bool TryParse(string value, out PortraitCommand cmd)
        {
            cmd = default;
            cmd.expression = "";
            if (string.IsNullOrEmpty(value)) return false;
            string[] parts = value.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            cmd.character = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string p = parts[i].ToLowerInvariant();
                if (p == "left") cmd.side = PortraitSide.Left;
                else if (p == "right") cmd.side = PortraitSide.Right;
                else if (p == "hide" || p == "off" || p == "none") cmd.hide = true;
                else cmd.expression = parts[i];
            }
            return true;
        }
    }
}
