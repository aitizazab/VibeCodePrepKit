using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>One palette row: the flat painted colour a look-library material takes in the comic-ink style.</summary>
    public struct InkEntry
    {
        /// <summary>The flat colour (sRGB, as the style sheet writes it). Ignored when <see cref="keepTexture"/> (the
        /// converter then keeps the source's own colour and map).</summary>
        public Color color;
        /// <summary>0..1: towards grey (the colour-free zones are made by the scene builder, not by the table).</summary>
        public float desaturate;
        /// <summary>The deep-shade hatching strength (<c>_HatchStrength</c>); 0 for the far city.</summary>
        public float hatch;
        /// <summary>Keep the source's texture (props, products, banners, signs): the painted look comes from the shading.</summary>
        public bool keepTexture;
        /// <summary>The wall brightness at the floor (<c>_GradBottom</c>); 1 for floors, which stay flat.</summary>
        public float gradBottom;
        /// <summary>A glow: lit by nothing, the source's colour and map as they are (<c>_Unlit</c>).</summary>
        public bool emissive;
    }

    /// <summary>
    /// The palette for Skyline's comic-ink look (docs/SKYLINE-PLAN-STYLE.md, Task 3): the flat, TF2-like muted colour of every
    /// look-library material, looked up by exact name, then by prefix (the longest prefix wins, so
    /// <c>SKL_Plaster_White__Mint</c> gets mint, not white). The far city's facades lerp towards the sky's horizon colour by
    /// band and haze number (layered depth: each further layer lighter and lower in contrast).
    /// </summary>
    public static class SkylineInkPalette
    {
        public const float DefaultGradBottom = 0.78f, DefaultHatch = 0.08f;   // fix round 1: hatching 0.15 read as moire
        /// <summary>The sky's horizon colour (<c>Skyline/InkSky</c>), the far city fades towards it.</summary>
        public static readonly Color Horizon = Hex("CFE0EE");

        static Color Hex(string rgb)
        {
            int v = Convert.ToInt32(rgb, 16);
            return new Color32((byte)((v >> 16) & 255), (byte)((v >> 8) & 255), (byte)(v & 255), 255);
        }

        /// <summary>Fix round 1 (washed out, "a white box with lines"): the tints, shutters, woods and metals take 18% more chroma and
        /// are 6% darker than the first table; the two base surfaces (the plaster white and the floors) are the exact values the
        /// creator's concept asks for (warm cream, a cream-gold floor) and are not boosted.</summary>
        static Color Boost(Color c)
        {
            float lum = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            Color o = new Color(lum + (c.r - lum) * 1.18f, lum + (c.g - lum) * 1.18f, lum + (c.b - lum) * 1.18f, 1f);
            return new Color(Mathf.Clamp01(o.r * 0.94f), Mathf.Clamp01(o.g * 0.94f), Mathf.Clamp01(o.b * 0.94f), 1f);
        }

        static InkEntry Exact(string hex, float gradBottom = DefaultGradBottom, float hatch = DefaultHatch) =>
            new InkEntry { color = Hex(hex), gradBottom = gradBottom, hatch = hatch };

        static InkEntry Paint(string hex, float gradBottom = DefaultGradBottom, float hatch = DefaultHatch) =>
            new InkEntry { color = Boost(Hex(hex)), gradBottom = gradBottom, hatch = hatch };

        static InkEntry Keep() => new InkEntry { keepTexture = true, gradBottom = DefaultGradBottom, hatch = DefaultHatch, color = Color.white };

        static readonly Dictionary<string, InkEntry> Table = new Dictionary<string, InkEntry>
        {
            { "SKL_Plaster_White", Exact("E9EDF2") },   // fix round 2: walls a cool blue-white (round 1 was warm cream, too muted); floors stay warm
            { "SKL_Plaster_White__Blush", Exact("F0D5DA") },
            { "SKL_Plaster_White__Mint", Exact("D2EBDD") },
            { "SKL_Plaster_White__Sage", Exact("C3D7C2") },
            { "SKL_Plaster_White__Sand", Exact("E6DCC6") },
            { "SKL_Plaster_White__Sky", Exact("BFDAF2") },
            { "SKL_Plaster_White__Slate", Exact("8899B0") },
            { "SKL_Marble_Cream", Exact("CDBB9C", 1f) },
            { "SKL_Terrazzo_White", Exact("CDBB9C", 1f) },
            { "SKL_Parquet_Herringbone", Paint("A0603E", 1f) },
            { "SKL_Wood_Cherry", Paint("A0603E") },
            { "SKL_Wood_Pine", Paint("C99B62") },
            { "SKL_Gold_Brushed", Paint("D4A23F") },
            { "SKL_Steel_Brushed", Paint("A7ADB3") },
            { "SKL_Steel_Dark", Paint("4A4F57") },
            { "SKL_Concrete", Paint("B9B3A8") },
            { "SKL_Ceiling_Tile", Paint("B9B3A8") },
            { "SKL_Shutter_Painted", Paint("C9C3B6") },
            { "SKL_Shutter_Painted__Navy", Paint("3E4E6E") },
            { "SKL_Shutter_Painted__Sage", Paint("8FA27E") },
            { "SKL_Shutter_Painted__Sand", Paint("D2B98C") },
            { "SKL_Shutter_Painted__Lift", Paint("E0A33A") },
            { "SKL_Shutter_Painted__Lockers", Paint("6F7F8F") },
            { "SKL_Hazard_Orange", Paint("F07A2A") },
            { "SKL_Ground_", Paint("BFB49E") },
            // props, products, banners, soil: the painted look comes from the shading, the texture stays
            { "SKL_Prop_", Keep() },
            { "SKL_Products", Keep() },
            { "SKL_Banners", Keep() },
            { "SKL_Soil", Keep() },
            { "SKL_EntryMarker", Keep() },
            // the bots' and effects' resource materials keep their colours (the converter gives them role 1)
            { "SK_Bot_Paint", Keep() },
            { "SK_Fx_Acid", Keep() },
        };

        static readonly Dictionary<string, Color> Families = new Dictionary<string, Color>
        {
                { "Blue", Hex("7FA6C9") }, { "Bronze", Hex("B08A63") }, { "Dark", Hex("4E5866") }, { "Green", Hex("86B3A0") },
            { "Silver", Hex("B7C0C9") }, { "Stone", Hex("C9BBA5") }, { "White", Hex("E4E6EA") },
        };

        /// <summary>The row for a material name (a trailing " (Instance)" is ignored); false when the table has nothing for it.
        /// Exact name first, then the facade pattern, then the longest prefix.</summary>
        public static bool TryGet(string materialName, out InkEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(materialName)) return false;
            string n = materialName.EndsWith(" (Instance)", StringComparison.Ordinal) ? materialName.Substring(0, materialName.Length - 11) : materialName;
            if (n.StartsWith("SK_Emissive_", StringComparison.Ordinal))
            {
                entry = new InkEntry { keepTexture = true, emissive = true, gradBottom = 1f, hatch = 0f, color = Color.white };
                return true;
            }
            if (TryFacade(n, out entry)) return true;
            string best = null;
            foreach (var key in Table.Keys)
                if (n.StartsWith(key, StringComparison.Ordinal) && (best == null || key.Length > best.Length)) best = key;
            if (best == null) return false;
            entry = Table[best];
            return true;
        }

        /// <summary>SKL_Facade_&lt;Family&gt;_Band&lt;n&gt;_* and SKL_Facade_&lt;Family&gt;_Haze&lt;n&gt;: the family colour lerped towards
        /// the horizon by 0, 0.25 or 0.5 for bands 0, 1, 2 and by 0.6 + 0.1 n for haze n; no hatching, no gradient.</summary>
        static bool TryFacade(string n, out InkEntry entry)
        {
            entry = default;
            const string prefix = "SKL_Facade_";
            if (!n.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var parts = n.Substring(prefix.Length).Split('_');
            if (parts.Length < 2 || !Families.TryGetValue(parts[0], out var family)) return false;
            string tag = parts[1];
            float t;
            if (tag.StartsWith("Band", StringComparison.Ordinal) && int.TryParse(tag.Substring(4), out int band))
                t = Mathf.Clamp(band, 0, 2) * 0.25f;
            else if (tag.StartsWith("Haze", StringComparison.Ordinal) && int.TryParse(tag.Substring(4), out int haze))
                t = 0.6f + 0.1f * haze;
            else return false;
            entry = new InkEntry { color = Color.Lerp(family, Horizon, t), gradBottom = 1f, hatch = 0f };
            return true;
        }
    }
}
