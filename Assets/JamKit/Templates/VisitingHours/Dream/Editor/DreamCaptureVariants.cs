using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The house's finish options, captured for the creator to choose from (2026-10-12, round 3: "a spread of different
    /// choices for the wall pattern, poised towards darker colours, and a pattern for the roof, or maybe something like a
    /// bezel for the edges... Maybe higher roofs as well?"). Each option is applied to the opened house scene in memory and
    /// shot from the same spots; the scene is never saved, so nothing becomes the default until one is picked.
    /// Run: tools/dream-capture.ps1 -Variants all (or walls,ceilings,cornices,heights,attic; "only" for these alone).
    /// Stills: wall-&lt;n&gt;-&lt;id&gt;-&lt;spot&gt;, ceiling-..., cornice-..., height-..., attic-... next to the usual ones.
    /// </summary>
    public static partial class DreamCapture
    {
        static string VariantsArg()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-houseVariants") return args[i + 1];
            return null;
        }

        // ---------- The options ----------

        /// <summary>A wallpaper (a colour tile, tools/art/vh_house_patterns.py) and the wainscot that goes with it.</summary>
        sealed class WallOption
        {
            public string id; public Color wainscot; public float tile;
            public WallOption(string id, int r, int g, int b, float tile) { this.id = id; wainscot = new Color(r / 255f, g / 255f, b / 255f); this.tile = tile; }
        }

        static readonly WallOption[] WallOptions =
        {
            new WallOption("forest-damask", 226, 214, 188, 0.7f),
            new WallOption("oxblood-stripe", 88, 60, 44, 0.5f),
            new WallOption("navy-sprig", 236, 234, 228, 0.45f),
            new WallOption("plum-trellis", 170, 160, 176, 0.5f),
            new WallOption("teal-botanical", 196, 150, 80, 0.8f),
            new WallOption("umber-fans", 40, 70, 56, 0.55f),
            new WallOption("sage-stripe", 44, 78, 60, 0.5f),
            new WallOption("rose-sprig", 106, 34, 40, 0.45f),
        };

        // Round 4: the creator's picks are built in (the first option of each list: "...-chosen"); the rest still show.
        static readonly string[] CeilingOptions = { "beams-chosen", "plain", "pressed-tin", "medallions", "coffers", "starry-paper" };
        static readonly string[] CorniceOptions = { "stepped-chosen", "plain-strip", "cove", "dentil" };
        static readonly float[] HeightOptions = { 0f, 0.4f };   // on top of the +0.4 m built in (round 4)
        static readonly string[] AtticOptions = { "painted-chosen", "plain-boards", "dark-boards" };

        static List<Spot> WallSpots() => new List<Spot>
        {
            new Spot("living", new Vector3(3.2f, F + Eye, 72.9f), new Vector3(7.4f, F + 1.0f, 76.4f)),
            new Spot("hall", new Vector3(-5.0f, F + Eye, 79.4f), new Vector3(8f, F + 1.2f, 79.3f)),
            new Spot("theos-room", new Vector3(3.4f, F + Eye, 83.4f), new Vector3(7.6f, F + 1.2f, 82.6f)),
        };

        // Looking a little up, so the ceiling shows.
        static List<Spot> CeilingSpots() => new List<Spot>
        {
            new Spot("living", new Vector3(3.0f, F + Eye, 72.8f), new Vector3(7.0f, F + 2.0f, 76.6f)),
            new Spot("hall", new Vector3(-5.0f, F + Eye, 79.4f), new Vector3(8f, F + 1.75f, 79.3f)),
            new Spot("theos-room", new Vector3(3.4f, F + Eye, 83.4f), new Vector3(7.6f, F + 1.9f, 82.6f)),
        };

        static List<Spot> CorniceSpots() => new List<Spot>
        {
            new Spot("living-corner", new Vector3(5.6f, F + Eye, 75.8f), new Vector3(2.9f, F + 2.8f, 72.1f)),
            new Spot("kitchen-corner", new Vector3(-3.6f, F + Eye, 76.6f), new Vector3(-6.9f, F + 2.6f, 72.1f)),
        };

        static List<Spot> AtticSpots() => new List<Spot>
        {
            new Spot("arrival", new Vector3(2.2f, DreamHouse.AtticFloor + Eye, 80.4f), new Vector3(-6.5f, DreamHouse.AtticFloor + 1.6f, 78f)),
            new Spot("storage", new Vector3(-0.6f, DreamHouse.AtticFloor + Eye, 78.6f), new Vector3(5.0f, DreamHouse.AtticFloor + 1.0f, 75f)),
        };

        static List<Spot> Named(List<Spot> spots, string prefix)
        {
            var list = new List<Spot>();
            foreach (var s in spots) list.Add(new Spot(prefix + "-" + s.name, s.eye, s.look));
            return list;
        }

        static void HouseVariants(string dir, string which)
        {
            bool all = which == "all";
            bool Has(string k) => all || which.Contains(k);
            string path = DreamHouseBuilder.ScenePath;
            if (Has("walls"))
            {
                Scene(path, Named(WallSpots(), "wall-0-current"), dir);
                for (int k = 0; k < WallOptions.Length; k++)
                {
                    var w = WallOptions[k];
                    Scene(path, Named(WallSpots(), $"wall-{k + 1}-{w.id}"), dir, () => ApplyWalls(w));
                }
            }
            if (Has("ceilings"))
                for (int k = 0; k < CeilingOptions.Length; k++)
                {
                    string c = CeilingOptions[k];
                    Scene(path, Named(CeilingSpots(), $"ceiling-{k}-{c}"), dir, () => ApplyCeiling(c));
                }
            if (Has("cornices"))
                for (int k = 0; k < CorniceOptions.Length; k++)
                {
                    string c = CorniceOptions[k];
                    Scene(path, Named(CorniceSpots(), $"cornice-{k}-{c}"), dir, () => ApplyCornice(c));
                }
            if (Has("heights"))
                foreach (float raise in HeightOptions)
                    Scene(path, Named(CeilingSpots(), $"height-plus{Mathf.RoundToInt((DreamHouse.CeilingRaise + raise) * 100):000}cm"), dir, () => RaiseCeilings(raise));
            if (Has("attic"))
                for (int k = 0; k < AtticOptions.Length; k++)
                {
                    string a = AtticOptions[k];
                    Scene(path, Named(AtticSpots(), $"attic-{k}-{a}"), dir, () => ApplyAttic(a));
                }
        }

        // ---------- Helpers ----------

        static Texture2D Tile(string name, bool colour)
        {
            string p = DreamArt.ArtDir + "/" + name + ".png";
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null) { Debug.LogWarning("JAMKIT_DREAM_CAPTURE missing pattern " + p); return null; }
            if (imp.wrapMode != TextureWrapMode.Repeat || imp.sRGBTexture != colour || !imp.mipmapEnabled)
            {
                imp.textureType = TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.sRGBTexture = colour;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        /// <summary>Every renderer of the open scene whose material's name starts with <paramref name="prefix"/> gets a
        /// copy of it made by <paramref name="make"/> (one copy per original material).</summary>
        static void Remat(string prefix, Func<Material, Material> make)
        {
            var copies = new Dictionary<Material, Material>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.name.StartsWith(prefix)) continue;
                    if (!copies.TryGetValue(m, out var c)) { c = make(new Material(m)); copies[m] = c; }
                    mats[i] = c;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static Material Trim()
        {
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial != null && r.sharedMaterial.name == "Pastel_RoomTrim") return r.sharedMaterial;
            return new Material(Shader.Find("JamKit/Dream/Pastel"));
        }

        static Transform _variantRoot;
        static Transform VariantRoot()
        {
            if (_variantRoot == null) _variantRoot = new GameObject("Finish variant (capture only)").transform;
            return _variantRoot;
        }

        static GameObject Part(PrimitiveType type, Vector3 at, Vector3 scale, Material m, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(VariantRoot(), false);
            go.transform.position = at;
            go.transform.localScale = scale;
            if (rot.HasValue) go.transform.rotation = rot.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Material Coloured(Material src, Color c)
        {
            var m = new Material(src);
            m.SetColor("_Color", c);
            return m;
        }

        // ---------- Walls ----------

        static void ApplyWalls(WallOption w)
        {
            _variantRoot = null;
            var tex = Tile("dream_wall_" + w.id, true);
            Material Paper(Material m)
            {
                m.SetColor("_Color", Color.white);
                m.SetTexture("_MainTex", tex);
                m.SetFloat("_TexScale", w.tile);
                m.SetFloat("_TexMean", 1f);
                m.SetFloat("_TexStrength", tex != null ? 1f : 0f);
                return m;
            }
            Remat("Pastel_RoomPaper_", Paper);
            Remat("Pastel_HouseWallpaper", Paper);   // the walls' own faces (door jambs, the backs of headers)
            Remat("Pastel_RoomWainscot_", m => { m.SetColor("_Color", w.wainscot); return m; });
        }

        // ---------- Ceilings ----------

        static IEnumerable<HouseRoom> CeilingRooms()
        {
            foreach (var r in DreamHouse.Build().rooms)
                if (r.name != "Attic" && r.name != "Attic stairs") yield return r;
        }

        static void ApplyCeiling(string option)
        {
            _variantRoot = null;
            if (option == "beams-chosen") return;
            var builtBeams = GameObject.Find("Ceiling beams");
            if (builtBeams != null) builtBeams.SetActive(false);
            if (option == "plain") return;
            if (option == "pressed-tin" || option == "starry-paper")
            {
                bool tin = option == "pressed-tin";
                var tex = Tile(tin ? "dream_ceiling_tin" : "dream_ceiling_paper", false);
                Remat("Pastel_HouseCeiling", m =>
                {
                    m.SetColor("_Color", tin ? new Color(0.9f, 0.89f, 0.85f) : new Color(0.86f, 0.86f, 0.96f));
                    m.SetTexture("_MainTex", tex);
                    m.SetFloat("_TexScale", tin ? 0.6f : 0.55f);
                    m.SetFloat("_TexMean", 0.5f);
                    m.SetFloat("_TexStrength", tex != null ? (tin ? 0.7f : 1f) : 0f);
                    return m;
                });
                return;
            }
            var trim = Trim();
            if (option == "medallions")
            {
                // A plaster rose round every pendant's cord: rings stepping down from the ceiling, a bead ring round it.
                var rose = Coloured(trim, new Color(0.98f, 0.96f, 0.92f));
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r.name != "Pendant cord") continue;
                    var top = new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z);
                    float[] radii = { 0.5f, 0.4f, 0.28f, 0.14f };
                    for (int i = 0; i < radii.Length; i++)
                        Part(PrimitiveType.Cylinder, top - Vector3.up * (0.012f + 0.022f * i), new Vector3(radii[i] * 2f, 0.012f, radii[i] * 2f), rose);
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * Mathf.PI * 2f / 16f;
                        Part(PrimitiveType.Sphere, top + new Vector3(Mathf.Cos(a) * 0.45f, -0.03f, Mathf.Sin(a) * 0.45f), Vector3.one * 0.05f, rose);
                    }
                }
                return;
            }
            if (option == "coffers")
            {
                // A grid of shallow ribs (0.75 m panels) under a slightly deeper ceiling colour.
                var rib = Coloured(trim, new Color(0.97f, 0.95f, 0.9f));
                Remat("Pastel_HouseCeiling", m => { m.SetColor("_Color", new Color(0.82f, 0.8f, 0.9f)); return m; });
                foreach (var room in CeilingRooms())
                {
                    float top = room.floorY + room.ceiling;
                    int nx = Mathf.Max(1, Mathf.RoundToInt((room.x1 - room.x0) / 0.75f)), nz = Mathf.Max(1, Mathf.RoundToInt((room.z1 - room.z0) / 0.75f));
                    for (int i = 1; i < nx; i++)
                    {
                        float x = room.x0 + (room.x1 - room.x0) * i / nx;
                        Part(PrimitiveType.Cube, new Vector3(x, top - 0.045f, (room.z0 + room.z1) * 0.5f), new Vector3(0.09f, 0.09f, room.z1 - room.z0), rib);
                    }
                    for (int j = 1; j < nz; j++)
                    {
                        float z = room.z0 + (room.z1 - room.z0) * j / nz;
                        Part(PrimitiveType.Cube, new Vector3((room.x0 + room.x1) * 0.5f, top - 0.045f, z), new Vector3(room.x1 - room.x0, 0.09f, 0.09f), rib);
                    }
                }
            }
        }

        // ---------- Cornices (the "bezel" where the walls meet the ceiling) ----------

        static void ApplyCornice(string option)
        {
            _variantRoot = null;
            if (option == "stepped-chosen") return;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.name == "Cornice step") r.enabled = false;
            var trim = Trim();
            var cove = Coloured(trim, new Color(0.99f, 0.97f, 0.94f));
            cove.SetFloat("_OutlineWidth", 0f);
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith("Cornice (")) continue;
                var parts = r.name.Substring(9).TrimEnd(')').Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int nx) || !int.TryParse(parts[1], out int nz)) continue;
                var n = new Vector3(nx, 0f, nz);
                var b = r.bounds;
                bool alongZ = nx != 0;
                float depth = alongZ ? b.size.x : b.size.z;
                float plane = alongZ ? b.center.x - n.x * depth * 0.5f : b.center.z - n.z * depth * 0.5f;
                float along = alongZ ? b.center.z : b.center.x, len = alongZ ? b.size.z : b.size.x, top = b.max.y;
                Vector3 At(float off, float y, float a) => alongZ ? new Vector3(plane + n.x * off, y, a) : new Vector3(a, y, plane + n.z * off);
                Vector3 Size(float l, float h, float depth) => alongZ ? new Vector3(depth, h, l) : new Vector3(l, h, depth);
                r.enabled = false;
                switch (option)
                {
                    case "cove":
                        // A big rounded cove filling the corner (no ink line on its curve, so it reads as one soft
                        // surface), a fillet along each edge.
                        Part(PrimitiveType.Cylinder, At(0f, top, along), new Vector3(0.3f, len * 0.5f, 0.3f), cove, alongZ ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, 0f, 90f));
                        Part(PrimitiveType.Cube, At(0.012f, top - 0.165f, along), Size(len, 0.03f, 0.024f), trim);
                        Part(PrimitiveType.Cube, At(0.165f, top - 0.012f, along), Size(len, 0.024f, 0.03f), trim);
                        break;
                    case "plain-strip":
                        Part(PrimitiveType.Cube, At(0.04f, top - 0.05f, along), Size(len, 0.1f, 0.08f), trim);
                        break;
                    case "dentil":
                        Part(PrimitiveType.Cube, At(0.035f, top - 0.06f, along), Size(len, 0.12f, 0.07f), trim);
                        Part(PrimitiveType.Cube, At(0.06f, top - 0.02f, along), Size(len, 0.04f, 0.12f), trim);
                        for (float a = along - len * 0.5f + 0.06f; a < along + len * 0.5f - 0.04f; a += 0.1f)
                            Part(PrimitiveType.Cube, At(0.03f, top - 0.12f - 0.03f, a), Size(0.05f, 0.06f, 0.06f), trim);
                        break;
                }
            }
        }

        // ---------- Higher ceilings (a preview: everything from the ceilings up moves up; the walls grow) ----------

        static void RaiseCeilings(float raise)
        {
            _variantRoot = null;
            if (raise <= 0f) return;
            const float WallTop = F + 2.28f + DreamHouse.CeilingRaise;   // the lowest ceiling (2.3 m on the plan, raised)
            var moved = new HashSet<Transform>();
            bool AncestorMoved(Transform t)
            {
                for (var p = t.parent; p != null; p = p.parent) if (moved.Contains(p)) return true;
                return false;
            }
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var plan = new List<(Renderer r, bool stretch)>();
            foreach (var r in renderers)
            {
                var b = r.bounds;
                if (b.center.x < DreamHouse.HubX0 - 0.6f || b.center.x > DreamHouse.HubX1 + 0.6f || b.center.z < DreamHouse.BayZ0 - 0.6f || b.center.z > DreamHouse.HubZ1 + 0.6f) continue;
                if (b.max.y < F || b.min.y > F + 9f) continue;
                string n = r.name;
                bool core = r.transform.parent != null && r.transform.parent.name == "The house";
                bool wallFace = n.EndsWith("(paper)") || n.EndsWith("(brick)");
                if (n.StartsWith("Pendant")) plan.Add((r, false));
                else if (core) { if (b.min.y >= WallTop) plan.Add((r, false)); else if (b.max.y > WallTop) plan.Add((r, true)); }
                else if (wallFace && b.max.y > WallTop && b.min.y < WallTop) plan.Add((r, true));
                else if (b.min.y >= F + 2.15f + DreamHouse.CeilingRaise) plan.Add((r, false));
            }
            foreach (var (r, stretch) in plan)
            {
                var t = r.transform;
                if (AncestorMoved(t)) continue;
                if (!stretch) { t.position += Vector3.up * raise; moved.Add(t); continue; }
                if (Vector3.Dot(t.up, Vector3.up) < 0.99f) continue;
                float h = r.bounds.size.y;
                if (h < 1e-3f) continue;
                var s = t.localScale;
                s.y *= (h + raise) / h;
                t.localScale = s;
                t.position += Vector3.up * (raise * 0.5f);
            }
        }

        // ---------- The attic's underside ----------

        static void ApplyAttic(string option)
        {
            _variantRoot = null;
            if (option == "painted-chosen") return;
            var tex = Tile(DreamHouseDressing.PatternFloorboards, false);
            bool plain = option == "plain-boards";
            Remat("Pastel_AtticBoards", m =>
            {
                m.SetColor("_Color", plain ? new Color(0.84f, 0.7f, 0.58f) : new Color(0.5f, 0.36f, 0.28f));
                m.SetTexture("_MainTex", tex);
                m.SetFloat("_TexScale", 2.4f);
                m.SetFloat("_TexMean", 0.5f);
                m.SetFloat("_TexStrength", plain || tex == null ? 0f : 0.45f);
                return m;
            });
        }
    }
}
