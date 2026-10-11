using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Night 2's house as a lived-in home (art pass 5, 2026-10-12: the creator found it "not at all furnished... for the
    /// space Elias spent most of his life in, his memories of it are rather bland"). Decoration only: no colliders, nothing
    /// on Theo's walking paths, the hiding spots, the routes or the run walls; the gameplay pieces are unchanged.
    ///
    /// The rules used (docs/DREAM-HOUSE.md "Interior notes", from feng shui and ordinary interior-design practice):
    /// a focal point per room (the fireplace, the table, the bed, the desk); the main seat backed and facing the room's
    /// focal point; furniture in groups, each anchored by a rug; clear walkways (about a metre) along the paths; light in
    /// layers (a pendant over each room, table and floor lamps lower down); soft and round things against the hard edges
    /// (rugs, cushions, a teddy, plants); things at different heights (rug, table, mantel, mirror, shelf).
    /// </summary>
    public static class DreamHouseDecor
    {
        const float F = DreamHouse.Ground;
        const float Kenney = 2.0f;   // Kenney's furniture is about half size: this makes a chair about 0.95 m tall

        static Transform _root;
        static readonly List<string> _missing = new List<string>();

        public static void Decorate(Transform root)
        {
            _missing.Clear();
            _root = new GameObject("Home (decoration)").transform;
            _root.SetParent(root, false);
            Living();
            Kitchen();
            Hallway();
            TheosRoom();
            Study();
            Debug.Log("JAMKIT_HOUSE art: home decoration placed" + (_missing.Count > 0 ? "; models missing: " + string.Join(", ", _missing) : ""));
        }

        // ---------- The rooms ----------

        /// <summary>The living room (x 1.2..12, z 72..86): the sofa faces the fireplace (the focal point) across a rug and a
        /// coffee table; an armchair with its lamp and side table closes the group; a bookcase, plants, a mirror.</summary>
        static void Living()
        {
            var t = Group("Living room");
            Rug(t, "rugRounded", new Vector3(6.5f, F, 80.1f), 6.2f, 4.6f, new Color(0.86f, 0.55f, 0.5f));
            Model(t, "tableCoffee", new Vector3(6.5f, F, 80.4f), Vector3.forward);
            var mug = DreamProps.MeshLook("DecorMug", new Color(0.98f, 0.95f, 0.9f), 0.004f);
            Prim(t, "Mug", PrimitiveType.Cylinder, new Vector3(6.1f, F + 0.47f + 0.05f, 80.3f), new Vector3(0.09f, 0.05f, 0.09f), mug);
            Model(t, "books", new Vector3(6.9f, F + 0.47f, 80.5f), Vector3.forward, 1.6f);
            Model(t, "plantSmall2", new Vector3(6.5f, F + 0.47f, 80.15f), Vector3.zero, 1.6f);
            Fireplace(t, new Vector3(9.3f, F, 86f));
            Model(t, "loungeChair", new Vector3(10.65f, F, 81.0f), Vector3.left);
            Model(t, "sideTable", new Vector3(11.35f, F, 82.6f), Vector3.left, 1.5f);
            var lamp = Model(t, "lampRoundTable", new Vector3(11.35f, F + 0.58f, 82.45f), Vector3.zero, 1.6f);
            LampGlow(t, lamp, 0.22f);
            var floorLamp = Model(t, "lampRoundFloor", new Vector3(11.5f, F, 79.6f), Vector3.zero);
            LampGlow(t, floorLamp, 0.3f);
            Model(t, "bookcaseOpen", new Vector3(11.68f, F, 84.2f), Vector3.left);
            Books(t, new Vector3(11.62f, F, 84.2f), true, new[] { 0.42f, 0.86f, 1.3f });
            Model(t, "pottedPlant", new Vector3(11.4f, F, 72.7f), Vector3.zero);
            Model(t, "pottedPlant", new Vector3(1.75f, F, 85.4f), Vector3.zero);
            Pendant(t, new Vector3(6.5f, F, 80.2f));
        }

        /// <summary>The kitchen (x -12..1, z 72..86): the table on its rug under a pendant; the counter's clutter and its
        /// upper cupboards; the fridge's notes and the children's drawings; a clock; a plant.</summary>
        static void Kitchen()
        {
            var t = Group("Kitchen");
            Rug(t, "rugRectangle", new Vector3(-6f, F, 77.5f), 6.6f, 5.6f, new Color(0.62f, 0.72f, 0.88f));
            Pendant(t, new Vector3(-6f, F, 77.5f));
            const float top = F + 0.95f, cx = -11.6f;
            Model(t, "kitchenCoffeeMachine", new Vector3(cx, top, 77.3f), Vector3.right);
            Model(t, "toaster", new Vector3(cx, top, 78.4f), Vector3.right, 1.5f);
            var mug = DreamProps.MeshLook("DecorMugRed", new Color(0.88f, 0.42f, 0.45f), 0.004f);
            Prim(t, "Mug", PrimitiveType.Cylinder, new Vector3(cx + 0.05f, top + 0.05f, 77.85f), new Vector3(0.09f, 0.05f, 0.09f), mug);
            Prim(t, "Mug", PrimitiveType.Cylinder, new Vector3(cx - 0.12f, top + 0.05f, 77.95f), new Vector3(0.09f, 0.05f, 0.09f), DreamProps.MeshLook("DecorMug", new Color(0.98f, 0.95f, 0.9f), 0.004f));
            FruitBowl(t, new Vector3(cx, top, 79.35f));
            for (int i = 0; i < 3; i++)
                Model(t, "kitchenCabinetUpper", new Vector3(-11.78f, F + 1.55f, 77.45f + i * 0.9f), Vector3.right);
            // The fridge: notes and two children's drawings by the calendar (the calendar is a memory: left as it is).
            var front = -11.07f;
            Picture(t, "dream_pic_kids_drawing_1", new Vector3(front, F + 1.3f, 82.15f), Vector3.right, 0.3f, 0.4f, null);
            Picture(t, "dream_pic_kids_drawing_2", new Vector3(front, F + 0.7f, 81.3f), Vector3.right, 0.28f, 0.37f, null);
            Note(t, new Vector3(front, F + 0.8f, 82.1f), Vector3.right, new Color(1f, 0.92f, 0.55f));
            Note(t, new Vector3(front, F + 1.75f, 81.2f), Vector3.right, new Color(0.7f, 0.9f, 0.75f));
            Clock(t, new Vector3(-6f, F + 2.25f, 72.03f), Vector3.forward);
            Model(t, "pottedPlant", new Vector3(0.55f, F, 85.45f), Vector3.zero);
        }

        /// <summary>The hallway (z 86.2..89): a long runner; the children's photos along its north wall.</summary>
        static void Hallway()
        {
            var t = Group("Hallway");
            Rug(t, "rugRectangle", new Vector3(-0.3f, F, 87.6f), 17f, 1.4f, new Color(0.8f, 0.62f, 0.78f));
            for (int i = 0; i < 4; i++)
            {
                float x = new[] { -9.4f, -2.2f, 2.2f, 9.2f }[i];
                Picture(t, "dream_pic_childhood_" + (i + 1), new Vector3(x, F + 1.65f, 88.98f), Vector3.back, 0.64f, 0.5f, null, true);
            }
        }

        /// <summary>Theo's room (x -12..-0.1, z 89.2..100): the bed with its teddy and bedside lamp; a round rug; a toy
        /// chest; a low bookcase; the desk with its chair and drawings pinned above; toys in the corner.</summary>
        static void TheosRoom()
        {
            var t = Group("Theo's room");
            Rug(t, "rugRound", new Vector3(-6.9f, F, 95.0f), 3.2f, 3.2f, new Color(0.98f, 0.8f, 0.45f));
            Model(t, "bear", new Vector3(-10.6f, F + 0.55f, 98.0f), Vector3.back, 1.2f);
            Model(t, "cabinetBedDrawerTable", new Vector3(-9.15f, F, 99.45f), Vector3.back);
            var lamp = Model(t, "lampRoundTable", new Vector3(-9.15f, F + 0.53f, 99.45f), Vector3.zero, 1.6f);
            LampGlow(t, lamp, 0.2f);
            Model(t, "cardboardBoxClosed", new Vector3(-10.7f, F, 94.2f), Vector3.back, 2.6f, new Color(0.45f, 0.7f, 0.9f));
            Model(t, "bookcaseOpenLow", new Vector3(-6.9f, F, 99.6f), Vector3.back, 2.2f);
            Books(t, new Vector3(-6.9f, F, 99.55f), false, new[] { 0.42f });
            Model(t, "chairDesk", new Vector3(-7f, F, 90.8f), Vector3.back);
            Picture(t, "dream_pic_kids_drawing_2", new Vector3(-7.4f, F + 1.55f, 89.22f), Vector3.forward, 0.36f, 0.48f, null);
            Toys(t, new Vector3(-11.3f, F, 90.0f));
            Pendant(t, new Vector3(-6.5f, F, 95.0f));
        }

        /// <summary>The study (x 0.1..12, z 89.2..100): the desk on its rug, its chair, lamp, radio and books; a reading
        /// corner (armchair, floor lamp, bookcases); a plant. The photo wall stays as it is (a memory).</summary>
        static void Study()
        {
            var t = Group("Study");
            Rug(t, "rugRectangle", new Vector3(7.5f, F, 95.9f), 4.2f, 2.6f, new Color(0.6f, 0.78f, 0.62f));
            Model(t, "chairDesk", new Vector3(7.5f, F, 96.65f), Vector3.back);
            Model(t, "radio", new Vector3(8.5f, F + 0.75f, 95.35f), Vector3.back, 1.2f);
            var lamp = Model(t, "lampRoundTable", new Vector3(6.35f, F + 0.75f, 95.35f), Vector3.zero, 1.6f);
            LampGlow(t, lamp, 0.22f);
            Model(t, "books", new Vector3(7.3f, F + 0.75f, 95.3f), Vector3.back, 1.8f);
            Model(t, "loungeChair", new Vector3(11.0f, F, 91.0f), Vector3.left);
            var floorLamp = Model(t, "lampRoundFloor", new Vector3(11.55f, F, 89.85f), Vector3.zero);
            LampGlow(t, floorLamp, 0.3f);
            foreach (float z in new[] { 93.3f, 94.2f })
            {
                Model(t, "bookcaseOpen", new Vector3(11.68f, F, z), Vector3.left);
                Books(t, new Vector3(11.62f, F, z), true, new[] { 0.42f, 0.86f, 1.3f });
            }
            Model(t, "pottedPlant", new Vector3(0.6f, F, 99.4f), Vector3.zero);
            Pendant(t, new Vector3(6f, F, 94.6f));
        }

        // ---------- Pieces ----------

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root, false);
            return t;
        }

        /// <summary>A Kenney model with its bottom at <paramref name="at"/>, turned so its front faces
        /// <paramref name="facing"/> (zero: as it comes), at <paramref name="scale"/> times Kenney's size. Optional tint
        /// replaces its colours.</summary>
        static GameObject Model(Transform parent, string name, Vector3 at, Vector3 facing, float scale = Kenney, Color? tint = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DreamHouseDressing.KenneyDir + "/" + name + ".fbx");
            if (prefab == null) { if (!_missing.Contains(name)) _missing.Add(name); return null; }
            float turn = facing == Vector3.zero ? float.NaN : DreamHouseDressing.TurnFor(prefab, facing);
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            var model = Object.Instantiate(prefab);
            model.name = name;
            model.transform.SetParent(pivot, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, float.IsNaN(turn) ? 0f : turn, 0f);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            pivot.localScale = Vector3.one * scale;
            var b = DreamHouseDressing.WorldBounds(model);
            pivot.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            Restyle(model, tint);
            return model;
        }

        /// <summary>A rug: Kenney's rug squeezed to <paramref name="width"/> (x) by <paramref name="depth"/> (z), a hair above
        /// the floor, in its own colour.</summary>
        static void Rug(Transform parent, string name, Vector3 centre, float width, float depth, Color colour)
        {
            var rug = Model(parent, name, centre, Vector3.zero, 1f, colour);
            if (rug == null) return;
            var pivot = rug.transform.parent;
            var b = DreamHouseDressing.WorldBounds(rug);
            pivot.localScale = new Vector3(width / Mathf.Max(b.size.x, 1e-3f), 1.5f, depth / Mathf.Max(b.size.z, 1e-3f));
            b = DreamHouseDressing.WorldBounds(rug);
            pivot.position += new Vector3(centre.x - b.center.x, centre.y + 0.006f - b.min.y, centre.z - b.center.z);
        }

        static void Restyle(GameObject model, Color? tint)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++)
                    mats[k] = tint.HasValue ? DreamProps.MeshLook("DecorTint_" + ColorUtility.ToHtmlStringRGB(tint.Value), tint.Value, 0.008f)
                        : DreamHouseDressing.Pastelise(mats[k]);
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            GameObjectUtility.SetStaticEditorFlags(model, 0);
        }

        static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 at, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = DreamEscapeBuilder.Part(name, type, parent, Vector3.zero, scale, mat);
            go.transform.position = at;
            if (rot.HasValue) go.transform.rotation = rot.Value;
            return go;
        }

        /// <summary>A soft warm glow at a lamp's shade (the dream's lights are painted, not lit).</summary>
        static void LampGlow(Transform parent, GameObject lamp, float size)
        {
            if (lamp == null) return;
            var b = DreamHouseDressing.WorldBounds(lamp);
            Prim(parent, "Lamp glow", PrimitiveType.Sphere, new Vector3(b.center.x, b.max.y - b.size.y * 0.18f, b.center.z), Vector3.one * size * 3f,
                DreamEscapeBuilder.Glow("DecorLampGlow", new Color(1f, 0.86f, 0.6f), 0.55f, 0f, 0.1f));
        }

        /// <summary>A pendant lamp hanging from the 3 m ceiling over <paramref name="floor"/>: a cord, a cream shade, a glow.</summary>
        static void Pendant(Transform parent, Vector3 floor)
        {
            var cord = DreamProps.MeshLook("DecorCord", new Color(0.36f, 0.3f, 0.46f), 0f);
            var shade = DreamProps.MeshLook("DecorShade", new Color(0.99f, 0.93f, 0.8f), 0.01f);
            Prim(parent, "Pendant cord", PrimitiveType.Cylinder, floor + new Vector3(0f, 2.75f, 0f), new Vector3(0.02f, 0.25f, 0.02f), cord);
            Prim(parent, "Pendant shade", PrimitiveType.Cylinder, floor + new Vector3(0f, 2.42f, 0f), new Vector3(0.55f, 0.09f, 0.55f), shade);
            Prim(parent, "Pendant glow", PrimitiveType.Sphere, floor + new Vector3(0f, 2.3f, 0f), Vector3.one * 0.9f,
                DreamEscapeBuilder.Glow("DecorLampGlow", new Color(1f, 0.86f, 0.6f), 0.55f, 0f, 0.1f));
        }

        /// <summary>The fireplace on the living room's north wall (its back on the wall at z = <paramref name="wall"/>.z):
        /// a brick chimney breast, a dark firebox with logs and a fire's glow, a hearth, a white mantel with a clock, candles,
        /// the siblings' photo and a plant, and a mirror above.</summary>
        static void Fireplace(Transform parent, Vector3 wall)
        {
            var t = new GameObject("Fireplace").transform;
            t.SetParent(parent, false);
            float x = wall.x, z = wall.z;
            var brick = DreamEscapeBuilder.Pastel("DecorBrick", new Color(0.86f, 0.55f, 0.5f), 0.03f);
            var mortar = DreamEscapeBuilder.Pastel("DecorMortar", new Color(0.93f, 0.78f, 0.72f), 0f);
            var white = DreamEscapeBuilder.Pastel("HouseTrim", DreamEscapeBuilder.Palette[0], 0.03f);
            var soot = DreamEscapeBuilder.Pastel("DecorSoot", new Color(0.3f, 0.25f, 0.36f), 0.02f);
            DreamEscapeBuilder.Box("Chimney breast", t, new Vector3(x, F + 1.5f, z - 0.25f), new Vector3(2.4f, 3f, 0.5f), brick);
            for (float y = F + 0.25f; y < F + 3f; y += 0.25f)   // brick courses
                DreamEscapeBuilder.Box("Brick course", t, new Vector3(x, y, z - 0.505f), new Vector3(2.4f, 0.025f, 0.02f), mortar);
            DreamEscapeBuilder.Box("Firebox", t, new Vector3(x, F + 0.45f, z - 0.49f), new Vector3(1.2f, 0.9f, 0.04f), soot);
            DreamEscapeBuilder.Box("Hearth", t, new Vector3(x, F + 0.04f, z - 0.78f), new Vector3(2.8f, 0.08f, 0.6f), white);
            var log = DreamProps.MeshLook("DecorLog", new Color(0.55f, 0.36f, 0.3f), 0.006f);
            for (int i = 0; i < 3; i++)
                Prim(t, "Log", PrimitiveType.Cylinder, new Vector3(x - 0.25f + i * 0.25f, F + 0.14f + (i == 1 ? 0.12f : 0f), z - 0.6f), new Vector3(0.12f, 0.3f, 0.12f), log, Quaternion.Euler(0f, 0f, 90f));
            Prim(t, "Fire", PrimitiveType.Sphere, new Vector3(x, F + 0.35f, z - 0.62f), new Vector3(0.9f, 0.6f, 0.3f), DreamEscapeBuilder.Glow("DecorFire", new Color(1f, 0.62f, 0.35f), 1.2f, 0f, 0.35f));
            float mantel = F + 1.25f;
            DreamEscapeBuilder.Box("Mantel", t, new Vector3(x, mantel, z - 0.62f), new Vector3(2.8f, 0.09f, 0.32f), white);
            // On the mantel: a clock in the middle, the siblings' photo, two candles, a little plant.
            Clock(t, new Vector3(x, mantel + 0.26f, z - 0.66f), Vector3.back, 0.18f, true);
            Picture(t, "dream_pic_siblings", new Vector3(x - 0.75f, mantel + 0.22f, z - 0.66f), Vector3.back, 0.36f, 0.28f, null, true, standing: true);
            var wax = DreamProps.MeshLook("DecorCandle", new Color(0.99f, 0.96f, 0.88f), 0.004f);
            foreach (float dx in new[] { 0.55f, 0.75f })
            {
                Prim(t, "Candle", PrimitiveType.Cylinder, new Vector3(x + dx, mantel + 0.16f, z - 0.62f), new Vector3(0.06f, 0.12f, 0.06f), wax);
                Prim(t, "Candle flame", PrimitiveType.Sphere, new Vector3(x + dx, mantel + 0.31f, z - 0.62f), Vector3.one * 0.12f, DreamEscapeBuilder.Glow("DecorFlame", new Color(1f, 0.82f, 0.45f), 1.1f, 0f, 0.3f));
            }
            Model(t, "plantSmall1", new Vector3(x + 1.15f, mantel + 0.045f, z - 0.62f), Vector3.zero, 2.2f);
            // The mirror above.
            var pane = DreamEscapeBuilder.Pastel("DecorMirror", new Color(0.82f, 0.9f, 1f), 0f, 0.35f);
            DreamEscapeBuilder.Quad("Mirror", t, new Vector3(x, F + 2.15f, z - 0.515f), Vector3.back, 1.1f, 0.8f, pane);
            DreamEscapeBuilder.Box("Mirror frame", t, new Vector3(x, F + 2.15f + 0.42f, z - 0.53f), new Vector3(1.24f, 0.06f, 0.04f), white);
            DreamEscapeBuilder.Box("Mirror frame", t, new Vector3(x, F + 2.15f - 0.42f, z - 0.53f), new Vector3(1.24f, 0.06f, 0.04f), white);
            DreamEscapeBuilder.Box("Mirror frame", t, new Vector3(x - 0.59f, F + 2.15f, z - 0.53f), new Vector3(0.06f, 0.9f, 0.04f), white);
            DreamEscapeBuilder.Box("Mirror frame", t, new Vector3(x + 0.59f, F + 2.15f, z - 0.53f), new Vector3(0.06f, 0.9f, 0.04f), white);
        }

        /// <summary>A round wall clock (or a standing mantel clock) facing <paramref name="normal"/>.</summary>
        static void Clock(Transform parent, Vector3 at, Vector3 normal, float radius = 0.22f, bool standing = false)
        {
            var face = DreamProps.MeshLook("DecorClockFace", new Color(0.99f, 0.97f, 0.92f), 0.006f);
            var rim = DreamProps.MeshLook("DecorClockRim", new Color(0.55f, 0.4f, 0.55f), 0.006f);
            var ink = DreamProps.MeshLook("DecorInk", new Color(0.36f, 0.3f, 0.46f), 0f);
            var rot = Quaternion.LookRotation(normal) * Quaternion.Euler(90f, 0f, 0f);   // a cylinder's axis along the normal
            Prim(parent, "Clock rim", PrimitiveType.Cylinder, at, new Vector3(radius * 2.2f, 0.03f, radius * 2.2f), rim, rot);
            Prim(parent, "Clock face", PrimitiveType.Cylinder, at + normal * 0.012f, new Vector3(radius * 2f, 0.03f, radius * 2f), face, rot);
            var up = Vector3.up; var side = Vector3.Cross(normal, up);
            Prim(parent, "Clock hand (hour)", PrimitiveType.Cube, at + normal * 0.05f + up * radius * 0.25f, new Vector3(0.025f, radius * 0.5f, 0.01f), ink, Quaternion.LookRotation(normal));
            Prim(parent, "Clock hand (minute)", PrimitiveType.Cube, at + normal * 0.05f + side * radius * 0.35f, new Vector3(radius * 0.7f, 0.018f, 0.01f), ink, Quaternion.LookRotation(normal));
            if (standing) Prim(parent, "Clock foot", PrimitiveType.Cube, at - up * (radius + 0.03f), new Vector3(radius * 1.6f, 0.05f, 0.1f), rim);
        }

        /// <summary>A picture from Art/Dream/ (or nothing if it's missing) in a thin frame, on a wall facing
        /// <paramref name="normal"/>, or standing on a shelf (leaning back a little).</summary>
        static void Picture(Transform parent, string art, Vector3 at, Vector3 normal, float w, float h, Material fallback, bool framed = false, bool standing = false)
        {
            var mat = DreamHouseBuilder.Picture(art, fallback);
            if (mat == null) { if (!_missing.Contains(art)) _missing.Add(art); return; }
            var q = DreamEscapeBuilder.Quad(art, parent, at + normal * 0.006f, normal, w, h, mat);
            if (standing) q.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(-12f, 0f, 0f);
            if (!framed) return;
            var wood = DreamEscapeBuilder.Pastel("HousePhotoFrame", new Color(0.62f, 0.46f, 0.38f), 0.01f);
            var frame = new GameObject(art + " (frame)").transform;
            frame.SetParent(parent, false);
            frame.position = at;
            frame.rotation = q.transform.rotation;
            float bar = 0.04f;
            foreach (var (pos, size) in new[] {
                (new Vector3(0f, h * 0.5f + bar * 0.5f, 0f), new Vector3(w + bar * 2f, bar, 0.025f)),
                (new Vector3(0f, -h * 0.5f - bar * 0.5f, 0f), new Vector3(w + bar * 2f, bar, 0.025f)),
                (new Vector3(w * 0.5f + bar * 0.5f, 0f, 0f), new Vector3(bar, h, 0.025f)),
                (new Vector3(-w * 0.5f - bar * 0.5f, 0f, 0f), new Vector3(bar, h, 0.025f)) })
            {
                var b = DreamEscapeBuilder.Box("Frame", frame, Vector3.zero, size, wood);
                b.transform.localPosition = pos;
                b.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>A small square note held by a magnet.</summary>
        static void Note(Transform parent, Vector3 at, Vector3 normal, Color colour)
        {
            DreamEscapeBuilder.Quad("Note", parent, at + normal * 0.006f, normal, 0.16f, 0.16f, DreamEscapeBuilder.Pastel("DecorNote" + ColorUtility.ToHtmlStringRGB(colour), colour, 0f));
            Prim(parent, "Magnet", PrimitiveType.Sphere, at + normal * 0.02f + Vector3.up * 0.06f, Vector3.one * 0.04f, DreamProps.MeshLook("DecorMagnet", new Color(0.88f, 0.42f, 0.45f), 0f));
        }

        /// <summary>A bowl of fruit (a cream bowl, oranges and apples) on a surface at <paramref name="at"/>.</summary>
        static void FruitBowl(Transform parent, Vector3 at)
        {
            Prim(parent, "Fruit bowl", PrimitiveType.Sphere, at + Vector3.up * 0.03f, new Vector3(0.36f, 0.14f, 0.36f), DreamProps.MeshLook("DecorBowl", new Color(0.98f, 0.95f, 0.9f), 0.006f));
            var orange = DreamProps.MeshLook("DecorOrange", new Color(0.98f, 0.65f, 0.3f), 0.004f);
            var apple = DreamProps.MeshLook("DecorApple", new Color(0.86f, 0.32f, 0.36f), 0.004f);
            var spots = new[] { new Vector3(-0.07f, 0.1f, -0.05f), new Vector3(0.07f, 0.1f, -0.03f), new Vector3(0f, 0.1f, 0.07f), new Vector3(0.01f, 0.17f, 0f) };
            for (int i = 0; i < spots.Length; i++)
                Prim(parent, "Fruit", PrimitiveType.Sphere, at + spots[i], Vector3.one * 0.1f, i % 2 == 0 ? orange : apple);
        }

        /// <summary>Rows of books on a bookcase's shelves (the bookcase's centre at <paramref name="at"/>, against an x wall
        /// when <paramref name="alongZ"/>), at the given shelf heights.</summary>
        static void Books(Transform parent, Vector3 at, bool alongZ, float[] shelves)
        {
            var colours = new[] { new Color(0.86f, 0.42f, 0.45f), new Color(0.45f, 0.62f, 0.86f), new Color(0.98f, 0.8f, 0.42f), new Color(0.55f, 0.75f, 0.55f), new Color(0.7f, 0.55f, 0.85f) };
            int k = 0;
            foreach (float y in shelves)
                for (float s = -0.3f; s <= 0.3f; s += 0.075f, k++)
                {
                    float h = 0.2f + 0.06f * ((k * 7) % 3);
                    var c = colours[(k * 3) % colours.Length];
                    var pos = alongZ ? new Vector3(at.x, at.y + y + h * 0.5f, at.z + s) : new Vector3(at.x + s, at.y + y + h * 0.5f, at.z);
                    var size = alongZ ? new Vector3(0.22f, h, 0.06f) : new Vector3(0.06f, h, 0.22f);
                    DreamEscapeBuilder.Box("Book", parent, pos, size, DreamEscapeBuilder.Pastel("DecorBook" + ColorUtility.ToHtmlStringRGB(c), c, 0.01f));
                }
        }

        /// <summary>Theo's toys in a corner: a stack of wooden blocks, a ball, a toy van.</summary>
        static void Toys(Transform parent, Vector3 corner)
        {
            var colours = new[] { new Color(0.86f, 0.42f, 0.45f), new Color(0.45f, 0.62f, 0.86f), new Color(0.98f, 0.8f, 0.42f) };
            for (int i = 0; i < 3; i++)
                Prim(parent, "Block", PrimitiveType.Cube, corner + new Vector3(0.05f * i, 0.09f + i * 0.18f, 0.02f * i), Vector3.one * 0.18f,
                    DreamProps.MeshLook("DecorBlock" + i, colours[i], 0.006f), Quaternion.Euler(0f, i * 17f, 0f));
            Prim(parent, "Ball", PrimitiveType.Sphere, corner + new Vector3(0.5f, 0.14f, 0.6f), Vector3.one * 0.28f, DreamProps.MeshLook("DecorBall", new Color(0.98f, 0.55f, 0.5f), 0.008f));
            DreamProps.Show("Toy van", DreamProps.RoundedBox("Toy van (small)", new Vector3(0.18f, 0.16f, 0.32f), 0.05f), DreamProps.MeshLook("DecorToyVan", new Color(0.45f, 0.72f, 0.86f), 0.006f),
                parent, Vector3.zero, Quaternion.Euler(0f, 30f, 0f)).transform.position = corner + new Vector3(0.9f, 0.1f, 0.1f);
        }
    }
}
