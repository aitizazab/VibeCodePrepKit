using System.IO;
using System.Linq;
using JamKit.EditorTools;
using UnityEditor;
using UnityEngine;

namespace JamKit.Doomlike.EditorTools
{
    /// <summary>
    /// Placeholder art for the DOOM-like template, drawn in code (original, no external assets):
    /// tiling wall, floor and ceiling textures, 8-angle enemy sprite sheets whose faces, arms and
    /// backs visibly turn with the viewing angle, pickups, scenery, the effects sheet, the guns in
    /// your hands and the How to Play pictures. Replace any PNG in Templates/Doomlike/Art/Generated
    /// with real art of the same layout (see docs/TEMPLATE-DOOMLIKE.md), or point the data at new
    /// textures. Unchanged pixels aren't rewritten, so rebuilding doesn't touch files for nothing.
    /// </summary>
    public static class DoomlikeArt
    {
        public const string Dir = DoomlikeBuilder.Root + "/Art/Generated";
        const int Cell = 64;

        public sealed class Set
        {
            public Texture2D[] walls = new Texture2D[4];
            public Texture2D[] floors = new Texture2D[2];
            public Texture2D ceiling, slime, exitPad, door, lockedDoor;
            public Texture2D husk, gunner, spitter, brute, wisp, warden;
            public Texture2D patch, medkit, clip, ammoBox, shells, charges, key, exitSign;
            public Texture2D shotgunPickup, rivetPickup, launcherPickup;
            public Texture2D pillar, junk;
            public Texture2D fx;
            public Texture2D viewFist, viewPistol, viewShotgun, viewRivet, viewLauncher;
            public Sprite howMove, howShoot, howKeys, howExit;
        }

        public static Set MakeAll()
        {
            DoomlikeBuilder.EnsureFolder(Dir);
            var s = new Set();
            s.walls[0] = Tex("dl_wall_brick", Brick(), true);
            s.walls[1] = Tex("dl_wall_metal", Metal(), true);
            s.walls[2] = Tex("dl_wall_tech", Tech(), true);
            s.walls[3] = Tex("dl_wall_rock", Rock(), true);
            s.floors[0] = Tex("dl_floor_tiles", Tiles(), true);
            s.floors[1] = Tex("dl_floor_grate", Grate(), true);
            s.ceiling = Tex("dl_ceiling", CeilingPanels(), true);
            s.slime = Tex("dl_slime", Slime(), true);
            s.exitPad = Tex("dl_exit_pad", ExitPad(), true);
            s.door = Tex("dl_door", Door(false), true);
            s.lockedDoor = Tex("dl_door_locked", Door(true), true);

            s.husk = Tex("dl_enemy_husk", Sheet(Designs.Husk), false);
            s.gunner = Tex("dl_enemy_gunner", Sheet(Designs.Gunner), false);
            s.spitter = Tex("dl_enemy_spitter", Sheet(Designs.Spitter), false);
            s.brute = Tex("dl_enemy_brute", Sheet(Designs.Brute), false);
            s.wisp = Tex("dl_enemy_wisp", Sheet(Designs.Wisp), false);
            s.warden = Tex("dl_enemy_warden", Sheet(Designs.Warden), false);

            s.patch = Tex("dl_pickup_patch", Patch(), false);
            s.medkit = Tex("dl_pickup_medkit", Medkit(), false);
            s.clip = Tex("dl_pickup_clip", Clip(false), false);
            s.ammoBox = Tex("dl_pickup_ammo_box", Clip(true), false);
            s.shells = Tex("dl_pickup_shells", Shells(), false);
            s.charges = Tex("dl_pickup_charges", Charges(), false);
            s.key = Tex("dl_key", KeyCard(), false);
            s.exitSign = Tex("dl_exit_sign", ExitSign(), false);
            s.shotgunPickup = Tex("dl_pickup_scattergun", GunPickup(0), false);
            s.rivetPickup = Tex("dl_pickup_rivetgun", GunPickup(1), false);
            s.launcherPickup = Tex("dl_pickup_launcher", GunPickup(2), false);
            s.pillar = Tex("dl_deco_pillar", Pillar(), false);
            s.junk = Tex("dl_deco_junk", Junk(), false);
            s.fx = Tex("dl_fx", FxSheet(), false);

            s.viewFist = Tex("dl_view_knuckles", View(ViewKind.Fist), false);
            s.viewPistol = Tex("dl_view_pistol", View(ViewKind.Pistol), false);
            s.viewShotgun = Tex("dl_view_scattergun", View(ViewKind.Shotgun), false);
            s.viewRivet = Tex("dl_view_rivetgun", View(ViewKind.Rivet), false);
            s.viewLauncher = Tex("dl_view_launcher", View(ViewKind.Launcher), false);

            s.howMove = HowTo("dl_howto_move", HowMove());
            s.howShoot = HowTo("dl_howto_shoot", HowShoot());
            s.howKeys = HowTo("dl_howto_keys", HowKeys());
            s.howExit = HowTo("dl_howto_exit", HowExit());
            return s;
        }

        // ---------- Import ----------

        /// <summary>Writes the PNG if its pixels changed and imports it point-filtered: tiling with
        /// mipmaps for surfaces, clamped with alpha for sprites.</summary>
        public static Texture2D Tex(string name, Px img, bool surface)
        {
            string path = Dir + "/" + name + ".png";
            var tex = new Texture2D(img.w, img.h, TextureFormat.RGBA32, false);
            tex.SetPixels32(img.ToUnity());
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            var wrap = surface ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            bool dirty = imp.textureType != TextureImporterType.Default || imp.filterMode != FilterMode.Point
                || imp.wrapMode != wrap || imp.mipmapEnabled != surface || imp.alphaIsTransparency == surface
                || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.npotScale != TextureImporterNPOTScale.None;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Default;
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = wrap;
                imp.mipmapEnabled = surface;
                imp.alphaIsTransparency = !surface;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.isReadable = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Sprite HowTo(string name, Px img) => SceneBuilder.MakeSprite(name, img.w, img.h, ToColors(img), 100f, Dir);

        static Color[] ToColors(Px img)
        {
            var c32 = img.ToUnity();
            var c = new Color[c32.Length];
            for (int i = 0; i < c.Length; i++) c[i] = c32[i];
            return c;
        }

        // ---------- Surfaces (64 x 64, tiling) ----------

        static Px Brick()
        {
            var p = new Px(64, 64);
            var mortar = Px.Hex(0x3a3632);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int row = y / 8;
                    int bx = (x + (row % 2) * 8) % 64;
                    int brick = bx / 16 + row * 7;
                    bool joint = y % 8 == 0 || bx % 16 == 0;
                    float n = Px.Hash(x, y, 3) * 0.12f + Px.Hash(brick, row, 9) * 0.22f;
                    var c = joint ? mortar : Px.C(0.55f + n, 0.5f + n * 0.9f, 0.45f + n * 0.8f);
                    if (!joint && (y % 8 == 1 || bx % 16 == 1)) c = Px.Mul(c, 1.12f);
                    if (!joint && (y % 8 == 7)) c = Px.Mul(c, 0.82f);
                    p.Set(x, y, c);
                }
            return p;
        }

        static Px Metal()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int lx = x % 32, ly = y % 32;
                    float n = Px.Hash(x, y, 5) * 0.06f + Px.Smooth(x / 8f, y / 2f, 8, 11) * 0.08f;
                    var c = Px.C(0.5f + n, 0.53f + n, 0.58f + n);
                    if (lx == 0 || ly == 0) c = Px.Hex(0x2a2d33);
                    else if (lx == 1 || ly == 1) c = Px.Mul(c, 1.25f);
                    else if (lx == 31 || ly == 31) c = Px.Mul(c, 0.7f);
                    if ((lx == 4 || lx == 27) && (ly == 4 || ly == 27)) c = Px.Hex(0xd0d4dc);
                    if ((lx == 5 || lx == 28) && (ly == 5 || ly == 28)) c = Px.Hex(0x30333a);
                    p.Set(x, y, c);
                }
            return p;
        }

        static Px Tech()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = Px.Hash(x, y, 7) * 0.05f;
                    var c = Px.C(0.2f + n, 0.22f + n, 0.26f + n);
                    if (y >= 26 && y <= 37)
                    {
                        c = Px.C(0.1f, 0.12f, 0.14f);
                        if (y >= 29 && y <= 34 && (x / 4) % 2 == 0) c = Px.C(0.3f, 1f, 0.85f);
                    }
                    if (y == 25 || y == 38) c = Px.C(0.55f, 0.6f, 0.65f);
                    if (x % 16 == 0 && (y < 25 || y > 38)) c = Px.C(0.12f, 0.13f, 0.15f);
                    if ((x % 16 == 8) && (y == 10 || y == 52)) c = Px.C(1f, 0.35f, 0.25f);
                    p.Set(x, y, c);
                }
            return p;
        }

        static Px Rock()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = Px.Smooth(x / 8f, y / 8f, 8, 21) * 0.6f + Px.Smooth(x / 4f, y / 4f, 16, 22) * 0.3f + Px.Hash(x, y, 23) * 0.1f;
                    float k = 0.3f + n * 0.5f;
                    var c = Px.C(k * 1.05f, k * 0.95f, k * 0.85f);
                    if (n > 0.78f) c = Px.Mul(c, 1.2f);
                    if (n < 0.32f) c = Px.Mul(c, 0.6f);
                    p.Set(x, y, c);
                }
            return p;
        }

        static Px Tiles()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int t = (x / 32) + (y / 32) * 2;
                    float n = Px.Hash(x, y, 31) * 0.08f + Px.Hash(t, 0, 32) * 0.08f;
                    var c = Px.C(0.42f + n, 0.42f + n, 0.4f + n);
                    if (x % 32 == 0 || y % 32 == 0) c = Px.Hex(0x23221f);
                    else if (x % 32 == 1 || y % 32 == 1) c = Px.Mul(c, 1.15f);
                    p.Set(x, y, c);
                }
            return p;
        }

        static Px Grate()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    bool bar = x % 8 < 2 || y % 8 < 2;
                    float n = Px.Hash(x, y, 41) * 0.06f;
                    var c = bar ? Px.C(0.5f + n, 0.48f + n, 0.42f + n) : Px.C(0.08f, 0.08f, 0.08f);
                    if (bar && (x % 8 == 0 || y % 8 == 0)) c = Px.Mul(c, 1.2f);
                    p.Set(x, y, c);
                }
            return p;
        }

        static Px CeilingPanels()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = Px.Hash(x, y, 51) * 0.05f;
                    var c = Px.C(0.4f + n, 0.4f + n, 0.42f + n);
                    if (x % 32 == 0 || y % 32 == 0) c = Px.Hex(0x1f1f22);
                    float dx = (x % 32) - 16f, dy = (y % 32) - 16f;
                    if (Mathf.Abs(dx) < 6 && Mathf.Abs(dy) < 3) c = Px.C(0.95f, 0.93f, 0.8f);
                    p.Set(x, y, c);
                }
            return p;
        }

        static Px Slime()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = Px.Smooth(x / 8f, y / 8f, 8, 61) * 0.7f + Px.Smooth(x / 4f, y / 4f, 16, 62) * 0.3f;
                    var c = Px.C(0.15f + n * 0.25f, 0.55f + n * 0.45f, 0.1f + n * 0.15f);
                    if (n > 0.8f) c = Px.C(0.7f, 1f, 0.5f);
                    p.Set(x, y, c);
                }
            return p;
        }

        static Px ExitPad()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    bool border = x < 4 || y < 4 || x > 59 || y > 59;
                    bool chevron = ((x + Mathf.Abs(y - 32)) / 8) % 2 == 0;
                    var c = border ? Px.C(1f, 0.85f, 0.2f) : chevron ? Px.C(0.3f, 1f, 0.45f) : Px.C(0.08f, 0.25f, 0.12f);
                    p.Set(x, y, c);
                }
            return p;
        }

        static Px Door(bool locked)
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = Px.Hash(x, y, 71) * 0.06f;
                    Color32 c = locked ? Px.C(0.55f + n, 0.55f + n, 0.55f + n) : Px.C(0.45f + n, 0.42f + n, 0.38f + n);
                    if (x == 31 || x == 32) c = Px.Hex(0x1a1a1a);
                    if (x % 16 == 4) c = Px.Mul(c, 0.75f);
                    if (locked && ((y >= 10 && y <= 17) || (y >= 46 && y <= 53))) c = Px.C(1f, 1f, 1f);
                    if (!locked && y >= 54 && ((x + y) / 6) % 2 == 0) c = Px.C(0.9f, 0.75f, 0.15f);
                    if (!locked && y >= 54 && ((x + y) / 6) % 2 == 1) c = Px.C(0.12f, 0.12f, 0.12f);
                    if (x < 2 || x > 61) c = Px.Mul(c, 0.6f);
                    p.Set(x, y, c);
                }
            return p;
        }

        // ---------- Enemies (8 columns x 5 rows of 64 x 64) ----------

        enum Pose { WalkA, WalkB, Attack, Pain }
        enum Arms { None, Gun, Claws, Fists, Cannons, Mouth }

        sealed class Design
        {
            public Color32 body, skin, eye, accent;
            public float bodyW = 11f, bodyH = 20f, headR = 6.5f, legH = 16f;
            public bool legs = true, floating, helmet, horns;
            public Arms arms = Arms.None;
            public int eyes = 2;
        }

        static class Designs
        {
            public static readonly Design Husk = new Design { body = Px.Hex(0x6f7d5a), skin = Px.Hex(0x9aa37a), eye = Px.Hex(0xff3322), accent = Px.Hex(0x3a4030), bodyW = 10f, bodyH = 18f, headR = 6f, arms = Arms.Claws };
            public static readonly Design Gunner = new Design { body = Px.Hex(0x5d5a3c), skin = Px.Hex(0xb08a6a), eye = Px.Hex(0xffe14a), accent = Px.Hex(0x2f3a2a), helmet = true, arms = Arms.Gun };
            public static readonly Design Spitter = new Design { body = Px.Hex(0x6a3f86), skin = Px.Hex(0x9d6ec0), eye = Px.Hex(0x7dff6a), accent = Px.Hex(0x3a2050), bodyW = 9f, bodyH = 22f, headR = 7f, legH = 18f, horns = true, arms = Arms.Mouth };
            public static readonly Design Brute = new Design { body = Px.Hex(0x8a4a32), skin = Px.Hex(0xb06a4a), eye = Px.Hex(0xffd23a), accent = Px.Hex(0x4a2416), bodyW = 16f, bodyH = 22f, headR = 5.5f, legH = 14f, arms = Arms.Fists };
            public static readonly Design Wisp = new Design { body = Px.Hex(0x2fa0c0), skin = Px.Hex(0x8ff0ff), eye = Px.Hex(0xffffff), accent = Px.Hex(0x16506a), bodyW = 14f, bodyH = 14f, headR = 0f, legs = false, floating = true, eyes = 1 };
            public static readonly Design Warden = new Design { body = Px.Hex(0x4a4f5c), skin = Px.Hex(0xc9a24a), eye = Px.Hex(0xff3a2a), accent = Px.Hex(0x22252c), bodyW = 17f, bodyH = 24f, headR = 6.5f, legH = 15f, helmet = true, horns = true, arms = Arms.Cannons, eyes = 3 };
        }

        static Px Sheet(Design d)
        {
            var sheet = new Px(Cell * 8, Cell * 5);
            var outline = Px.C(0.05f, 0.04f, 0.06f);
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 8; col++)
                {
                    var cell = new Px(Cell, Cell);
                    Figure(cell, d, col * 45f * Mathf.Deg2Rad, (Pose)row);
                    cell.Outline(outline);
                    sheet.Blit(cell, col * Cell, row * Cell);
                }
            for (int f = 0; f < 7; f++)
            {
                var cell = new Px(Cell, Cell);
                if (f < 4) Death(cell, d, f);
                else Gib(cell, d, f - 4);
                cell.Outline(outline);
                sheet.Blit(cell, f * Cell, 4 * Cell);
            }
            return sheet;
        }

        /// <summary>A paper-doll figure seen from <paramref name="rel"/> (radians, the camera's angle
        /// counter-clockwise from the figure's facing): the body narrows side-on, the face and arms
        /// slide toward the way it faces, and from behind you see its back.</summary>
        static void Figure(Px p, Design d, float rel, Pose pose)
        {
            float cosv = Mathf.Cos(rel), side = -Mathf.Sin(rel);
            if (Mathf.Abs(cosv) < 1e-3f) cosv = 0f;
            if (Mathf.Abs(side) < 1e-3f) side = 0f;
            float ac = Mathf.Abs(cosv);
            const float cx = 32f;
            bool front = cosv > -0.2f;
            Color32 body = d.body, skin = d.skin;
            float lean = 0f, crouch = 0f;
            if (pose == Pose.Pain)
            {
                body = Px.Lerp(body, Px.C(1f, 1f, 1f), 0.35f);
                skin = Px.Lerp(skin, Px.C(1f, 1f, 1f), 0.35f);
                lean = -side * 3f;
                crouch = 2f;
            }
            if (pose == Pose.Attack) lean = side * 2f;
            int stride = pose == Pose.WalkA ? 1 : pose == Pose.WalkB ? -1 : 0;

            if (d.floating)
            {
                float bob = pose == Pose.WalkB ? 1.5f : 0f;
                float cy = 30f + bob;
                // Tail (behind), body, a ring that tilts with the view, one big eye.
                p.Poly(d.accent, cx - 6f, cy + 6f, cx + 6f, cy + 6f, cx + side * 8f, cy + 26f);
                p.Ellipse(cx + lean, cy, d.bodyW, d.bodyH, body);
                p.Ellipse(cx + lean - 3f, cy - 4f, d.bodyW * 0.45f, d.bodyH * 0.4f, Px.Lerp(body, skin, 0.6f));
                float ringW = d.bodyW + 6f, ringH = 3f + 3f * ac;
                for (int i = 0; i < 40; i++)
                {
                    float a = i / 40f * Mathf.PI * 2f;
                    float x = cx + lean + Mathf.Cos(a) * ringW, y = cy + 4f + Mathf.Sin(a) * ringH;
                    if (Mathf.Sin(a) < 0f && front) continue; // the back half hides behind the body
                    p.Circle(x, y, 1.2f, d.accent);
                }
                if (front)
                {
                    float ex = cx + lean + side * d.bodyW * 0.55f;
                    float er = 4.5f * Mathf.Max(0.45f, ac);
                    p.Ellipse(ex, cy - 1f, er + 1f, 5.5f, Px.C(0.05f, 0.05f, 0.1f));
                    p.Ellipse(ex, cy - 1f, er, 4.5f, pose == Pose.Attack ? Px.C(1f, 1f, 0.6f) : d.eye);
                    p.Circle(ex + side * 1.5f, cy - 1f, 1.5f, Px.C(0.05f, 0.05f, 0.1f));
                }
                else p.Ellipse(cx + lean, cy, d.bodyW * 0.5f, d.bodyH * 0.5f, d.accent);
                if (pose == Pose.Attack)
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        p.Circle(cx + Mathf.Cos(a) * (d.bodyW + 3f), cy + Mathf.Sin(a) * (d.bodyH + 3f), 2f, Px.C(1f, 0.9f, 0.4f));
                    }
                return;
            }

            float ground = 62f;
            float hip = ground - d.legH + crouch;
            float torsoTop = hip - d.bodyH;
            float bw = Mathf.Lerp(d.bodyW * 0.62f, d.bodyW, ac);
            float headCx = cx + lean + side * 2.5f, headCy = torsoTop - d.headR * 0.75f;
            float armY = torsoTop + d.bodyH * 0.35f;

            // Legs: apart sideways from the front, apart along the walk from the side.
            if (d.legs)
            {
                float spread = bw * 0.42f * ac + 1.5f;
                float stepX = stride * side * 5f;
                float l = cx - spread + stepX, r = cx + spread - stepX;
                float liftL = stride > 0 ? 3f : 0f, liftR = stride < 0 ? 3f : 0f;
                var legC = Px.Mul(d.accent, 1.2f);
                p.Line(cx - spread * 0.6f, hip, l, ground - liftL, 5.5f, legC);
                p.Line(cx + spread * 0.6f, hip, r, ground - liftR, 5.5f, legC);
                p.Ellipse(l + side * 2f, ground - liftL, 4f, 2f, d.accent);
                p.Ellipse(r + side * 2f, ground - liftR, 4f, 2f, d.accent);
            }

            if (!front) DrawArms(p, d, cx + lean, armY, bw, side, ac, pose, false);

            // Torso, a lighter belly or a dark back plate, the head.
            p.Ellipse(cx + lean, (hip + torsoTop) * 0.5f, bw, d.bodyH * 0.55f, body);
            if (cosv > 0.2f) p.Ellipse(cx + lean + side * 2f, (hip + torsoTop) * 0.5f + 2f, bw * 0.55f * ac, d.bodyH * 0.35f, Px.Lerp(body, skin, 0.45f));
            if (cosv < -0.2f)
            {
                p.Rect((int)(cx + lean - bw * 0.5f * ac), (int)(torsoTop + 3f), (int)(cx + lean + bw * 0.5f * ac), (int)(hip - 4f), Px.Mul(d.accent, 1.1f));
                for (int i = 0; i < 3; i++) p.Circle(cx + lean, torsoTop + 5f + i * 5f, 1.6f, Px.Mul(d.accent, 0.6f));
            }
            if (d.headR > 0f)
            {
                p.Circle(headCx, headCy, d.headR, skin);
                if (d.helmet)
                {
                    p.Ellipse(headCx, headCy - d.headR * 0.4f, d.headR + 1f, d.headR * 0.7f, d.accent);
                    if (front) p.Rect((int)(headCx - d.headR), (int)(headCy - 1f), (int)(headCx + d.headR), (int)(headCy), d.accent);
                }
                if (d.horns)
                {
                    p.Line(headCx - d.headR * 0.7f, headCy - d.headR * 0.5f, headCx - d.headR * 1.3f - side, headCy - d.headR * 1.7f, 2.5f, Px.Hex(0xe8e0c8));
                    p.Line(headCx + d.headR * 0.7f, headCy - d.headR * 0.5f, headCx + d.headR * 1.3f - side, headCy - d.headR * 1.7f, 2.5f, Px.Hex(0xe8e0c8));
                }
                if (cosv > -0.35f)
                {
                    // Eyes slide toward the facing side and merge into one when seen side-on.
                    float ex = headCx + side * d.headR * 0.5f;
                    float gap = d.headR * 0.42f * ac;
                    var eye = d.eye;
                    if (d.eyes == 3)
                    {
                        p.Circle(ex - gap * 1.3f, headCy, 1.5f, eye);
                        p.Circle(ex, headCy - 1.5f, 1.5f, eye);
                        p.Circle(ex + gap * 1.3f, headCy, 1.5f, eye);
                    }
                    else if (gap < 1.2f) p.Circle(ex + side * 1.5f, headCy, 1.6f, eye);
                    else
                    {
                        p.Circle(ex - gap, headCy, 1.6f, eye);
                        p.Circle(ex + gap, headCy, 1.6f, eye);
                    }
                    if (d.arms == Arms.Mouth)
                        p.Ellipse(ex + side, headCy + d.headR * 0.5f, 2f + ac * 1.5f, pose == Pose.Attack ? 2.5f : 1f,
                            pose == Pose.Attack ? Px.C(1f, 0.8f, 0.2f) : Px.Hex(0x200a20));
                }
            }

            if (front) DrawArms(p, d, cx + lean, armY, bw, side, ac, pose, true);
        }

        static void DrawArms(Px p, Design d, float cx, float armY, float bw, float side, float ac, Pose pose, bool inFront)
        {
            var metal = Px.Hex(0x2b2d33);
            var flash = Px.C(1f, 0.92f, 0.4f);
            bool attack = pose == Pose.Attack;
            switch (d.arms)
            {
                case Arms.Gun:
                {
                    float sx = cx + bw * 0.55f;
                    if (Mathf.Abs(side) > 0.3f)
                    {
                        float x0 = cx + side * bw * 0.2f, x1 = cx + side * (bw + 11f);
                        float y = attack ? armY - 2f : armY + 2f;
                        p.Line(x0, y, x1, y, 4f, metal);
                        p.Line(x0, y + 2f, x0 + side * 4f, y + 6f, 3f, d.skin);
                        if (attack) p.Circle(x1 + side * 3f, y, 4f, flash);
                    }
                    else
                    {
                        p.Circle(sx, armY + 2f, 3.5f, metal);
                        p.Circle(sx, armY + 2f, 1.5f, Px.C(0.05f, 0.05f, 0.05f));
                        if (attack && inFront) p.Circle(sx, armY + 2f, 5f, flash);
                    }
                    break;
                }
                case Arms.Claws:
                {
                    float reach = attack ? 12f : 6f;
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float shoulder = cx + k * bw * 0.8f * Mathf.Max(0.35f, ac);
                        float hx = shoulder + side * reach + k * (1f - Mathf.Abs(side)) * 3f;
                        float hy = attack ? armY - 4f : armY + 10f;
                        p.Line(shoulder, armY, hx, hy, 3.5f, d.skin);
                        for (int c = -1; c <= 1; c++) p.Line(hx, hy, hx + side * 3f + c * 1.5f, hy + 3f, 1.2f, Px.Hex(0xf0e8d0));
                    }
                    break;
                }
                case Arms.Fists:
                {
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float shoulder = cx + k * bw * 0.9f * Mathf.Max(0.4f, ac);
                        bool punching = attack && k == 1;
                        float hx = shoulder + side * (punching ? 16f : 5f) + k * (1f - Mathf.Abs(side)) * 2f;
                        float hy = punching ? armY : armY + 14f;
                        p.Line(shoulder, armY - 2f, hx, hy, 6f, d.body);
                        p.Circle(hx, hy, 5f, d.skin);
                    }
                    break;
                }
                case Arms.Cannons:
                {
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float sx = cx + k * bw * 0.75f * Mathf.Max(0.4f, ac);
                        if (Mathf.Abs(side) > 0.3f)
                        {
                            float y = armY - 6f + k * 2f;
                            p.Line(sx, y, sx + side * 16f, y, 6f, metal);
                            if (attack) p.Circle(sx + side * 19f, y, 4.5f, flash);
                        }
                        else
                        {
                            p.Circle(sx, armY - 6f, 5f, metal);
                            p.Circle(sx, armY - 6f, 2.5f, Px.C(0.05f, 0.05f, 0.05f));
                            if (attack && inFront) p.Circle(sx, armY - 6f, 6.5f, flash);
                        }
                    }
                    break;
                }
            }
        }

        static void Death(Px p, Design d, int f)
        {
            var body = d.body;
            var blood = Px.Hex(0x8a0f0f);
            if (d.floating)
            {
                float cy = 30f + f * 9f;
                if (f < 2)
                {
                    p.Ellipse(32f, cy, d.bodyW - f * 3f, d.bodyH - f * 4f, Px.Lerp(body, Px.C(1f, 1f, 1f), 0.4f - f * 0.2f));
                    p.Circle(32f, cy, 4f - f, d.eye);
                }
                else
                {
                    for (int i = 0; i < 5; i++) p.Circle(20f + i * 6f, 58f - (i % 2) * 2f, 3f, i % 2 == 0 ? body : d.accent);
                    if (f == 3) p.Ellipse(32f, 60f, 14f, 2f, d.accent);
                }
                return;
            }
            switch (f)
            {
                case 0:
                    Figure(p, d, 0f, Pose.Pain);
                    break;
                case 1:
                    p.Ellipse(32f, 50f, d.bodyW, d.bodyH * 0.4f, body);
                    p.Circle(32f + 4f, 41f, d.headR, d.skin);
                    p.Ellipse(32f, 61f, d.bodyW + 4f, 2f, blood);
                    break;
                case 2:
                    p.Ellipse(32f, 58f, d.bodyW * 1.5f, 4.5f, body);
                    p.Circle(32f + d.bodyW * 1.4f, 57f, d.headR * 0.85f, d.skin);
                    p.Ellipse(30f, 61f, d.bodyW * 1.6f, 2f, blood);
                    break;
                default:
                    p.Ellipse(32f, 60f, d.bodyW * 1.9f, 3f, blood);
                    p.Ellipse(32f, 59f, d.bodyW * 1.5f, 3.5f, Px.Mul(body, 0.8f));
                    p.Circle(32f + d.bodyW * 1.4f, 58f, d.headR * 0.8f, Px.Mul(d.skin, 0.8f));
                    break;
            }
        }

        static void Gib(Px p, Design d, int f)
        {
            var blood = Px.Hex(0xa01010);
            var dark = Px.Hex(0x5a0808);
            if (f == 0)
            {
                p.Circle(32f, 40f, 16f, blood);
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 0.7f;
                    p.Circle(32f + Mathf.Cos(a) * 18f, 40f + Mathf.Sin(a) * 14f, 4f, i % 3 == 0 ? d.body : i % 3 == 1 ? d.skin : dark);
                }
            }
            else if (f == 1)
            {
                for (int i = 0; i < 10; i++)
                    p.Circle(14f + i * 4f, 54f + Px.Hash(i, 1, 7) * 6f, 2.5f + Px.Hash(i, 2, 7) * 2f, i % 2 == 0 ? blood : d.body);
            }
            else
            {
                p.Ellipse(32f, 60f, 22f, 3f, blood);
                for (int i = 0; i < 6; i++) p.Circle(16f + i * 6.5f, 58f, 2.5f, i % 2 == 0 ? dark : Px.Mul(d.body, 0.7f));
            }
        }

        // ---------- Pickups and scenery ----------

        static Px Patch()
        {
            var p = new Px(32, 32);
            p.Rect(10, 8, 21, 29, Px.Hex(0xe0e6e8));
            p.Rect(13, 4, 18, 8, Px.Hex(0x9aa6aa));
            p.Rect(12, 14, 19, 25, Px.Hex(0x3ad06a));
            p.Rect(15, 16, 16, 23, Px.Hex(0xf2fff2));
            p.Rect(13, 19, 18, 20, Px.Hex(0xf2fff2));
            p.Outline(Px.C(0.05f, 0.05f, 0.06f));
            return p;
        }

        static Px Medkit()
        {
            var p = new Px(48, 40);
            p.Rect(4, 10, 43, 37, Px.Hex(0xe8eaec));
            p.Rect(4, 34, 43, 37, Px.Hex(0xa8acb0));
            p.Rect(18, 4, 29, 10, Px.Hex(0x606468));
            p.Rect(20, 14, 27, 33, Px.Hex(0xe02a2a));
            p.Rect(13, 20, 34, 27, Px.Hex(0xe02a2a));
            p.Outline(Px.C(0.05f, 0.05f, 0.06f));
            return p;
        }

        static Px Clip(bool box)
        {
            var p = new Px(box ? 48 : 32, 32);
            if (box)
            {
                p.Rect(4, 12, 43, 31, Px.Hex(0x5a6a3a));
                p.Rect(4, 12, 43, 15, Px.Hex(0x7a8a4a));
                for (int i = 0; i < 6; i++) p.Rect(7 + i * 6, 4, 10 + i * 6, 12, Px.Hex(0xe0b040));
                p.Rect(16, 20, 31, 25, Px.Hex(0xe8e0b0));
            }
            else
            {
                p.Rect(10, 8, 21, 31, Px.Hex(0x3a3d44));
                for (int i = 0; i < 3; i++) p.Rect(12 + i * 3, 3, 13 + i * 3, 8, Px.Hex(0xe0b040));
            }
            p.Outline(Px.C(0.05f, 0.05f, 0.06f));
            return p;
        }

        static Px Shells()
        {
            var p = new Px(40, 32);
            for (int i = 0; i < 4; i++)
            {
                int x = 5 + i * 8;
                p.Rect(x, 8, x + 5, 25, Px.Hex(0xc02a20));
                p.Rect(x, 25, x + 5, 30, Px.Hex(0xd8b048));
                p.Rect(x + 1, 9, x + 1, 24, Px.Hex(0xe85a48));
            }
            p.Outline(Px.C(0.05f, 0.05f, 0.06f));
            return p;
        }

        static Px Charges()
        {
            var p = new Px(32, 40);
            p.Rect(8, 6, 23, 37, Px.Hex(0x30343c));
            p.Rect(11, 10, 20, 33, Px.Hex(0x40c8ff));
            p.Rect(13, 12, 14, 31, Px.Hex(0xc8f4ff));
            p.Rect(12, 2, 19, 6, Px.Hex(0x80868e));
            p.Outline(Px.C(0.05f, 0.05f, 0.06f));
            return p;
        }

        /// <summary>White (the key colour tints it).</summary>
        static Px KeyCard()
        {
            var p = new Px(32, 32);
            p.Rect(6, 6, 25, 27, Px.C(1f, 1f, 1f));
            p.Rect(8, 8, 23, 12, Px.C(0.55f, 0.55f, 0.55f));
            p.Rect(9, 16, 22, 17, Px.C(0.7f, 0.7f, 0.7f));
            p.Rect(9, 20, 18, 21, Px.C(0.7f, 0.7f, 0.7f));
            p.Rect(20, 22, 23, 25, Px.C(0.35f, 0.35f, 0.35f));
            p.Outline(Px.C(0.05f, 0.05f, 0.06f));
            return p;
        }

        static readonly string[] Glyphs =
        {
            // E, X, I, T in a 5 x 7 font
            "#####|#....|#....|####.|#....|#....|#####",
            "#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#",
            "#####|..#..|..#..|..#..|..#..|..#..|#####",
            "#####|..#..|..#..|..#..|..#..|..#..|..#..",
        };

        static Px ExitSign()
        {
            var p = new Px(64, 24);
            p.Rect(0, 0, 63, 23, Px.Hex(0x0c1a10));
            p.Rect(0, 0, 63, 1, Px.Hex(0x40ff70));
            p.Rect(0, 22, 63, 23, Px.Hex(0x40ff70));
            p.Rect(0, 0, 1, 23, Px.Hex(0x40ff70));
            p.Rect(62, 0, 63, 23, Px.Hex(0x40ff70));
            for (int g = 0; g < 4; g++)
            {
                var rows = Glyphs[g].Split('|');
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 5; x++)
                        if (rows[y][x] == '#')
                        {
                            int ox = 9 + g * 12 + x * 2, oy = 5 + y * 2;
                            p.Rect(ox, oy, ox + 1, oy + 1, Px.Hex(0x7dff9a));
                        }
            }
            return p;
        }

        static Px GunPickup(int kind)
        {
            var p = new Px(64, 24);
            var metal = Px.Hex(0x3a3d44);
            var light = Px.Hex(0x6a707a);
            var wood = Px.Hex(0x7a4a2a);
            switch (kind)
            {
                case 0: // scattergun: long double barrel and a wooden stock
                    p.Rect(4, 8, 40, 11, metal);
                    p.Rect(4, 12, 40, 14, light);
                    p.Poly(wood, 38, 8, 60, 10, 60, 18, 40, 16);
                    p.Rect(30, 14, 34, 19, metal);
                    break;
                case 1: // rivet gun: chunky body with three barrels
                    p.Rect(4, 6, 26, 8, metal);
                    p.Rect(4, 10, 26, 12, metal);
                    p.Rect(4, 14, 26, 16, metal);
                    p.Rect(24, 4, 50, 18, light);
                    p.Rect(40, 18, 46, 22, metal);
                    p.Rect(28, 7, 40, 9, Px.Hex(0xe0b040));
                    break;
                default: // launcher: a fat tube with a glowing cell
                    p.Rect(4, 4, 56, 16, Px.Hex(0x2f4a5a));
                    p.Rect(4, 6, 56, 7, Px.Hex(0x5a8aa0));
                    p.Rect(22, 8, 34, 14, Px.Hex(0x40c8ff));
                    p.Rect(40, 16, 46, 22, metal);
                    break;
            }
            p.Outline(Px.C(0.05f, 0.05f, 0.06f));
            return p;
        }

        static Px Pillar()
        {
            var p = new Px(32, 96);
            for (int y = 18; y < 92; y++)
                for (int x = 9; x < 23; x++)
                {
                    float n = Px.Hash(x, y, 81) * 0.08f;
                    var c = Px.C(0.42f + n, 0.4f + n, 0.38f + n);
                    if (x == 9 || x == 22) c = Px.Mul(c, 0.7f);
                    if (x == 10) c = Px.Mul(c, 1.2f);
                    p.Set(x, y, c);
                }
            p.Rect(6, 88, 25, 95, Px.Hex(0x34322e));
            p.Rect(6, 14, 25, 19, Px.Hex(0x34322e));
            p.Ellipse(16f, 9f, 8f, 7f, Px.Hex(0xffd060));
            p.Ellipse(16f, 9f, 4f, 4f, Px.Hex(0xfff6d0));
            p.Outline(Px.C(0.05f, 0.04f, 0.04f));
            return p;
        }

        static Px Junk()
        {
            var p = new Px(48, 24);
            var bone = Px.Hex(0xe0d6bc);
            p.Ellipse(24f, 21f, 20f, 3f, Px.Hex(0x4a1010));
            p.Line(8f, 18f, 22f, 14f, 3f, bone);
            p.Line(26f, 20f, 40f, 16f, 3f, bone);
            p.Circle(30f, 12f, 5f, bone);
            p.Circle(28f, 11f, 1.2f, Px.Hex(0x201010));
            p.Circle(32f, 11f, 1.2f, Px.Hex(0x201010));
            p.Outline(Px.C(0.05f, 0.04f, 0.04f));
            return p;
        }

        // ---------- Effects sheet: 8 x 4 cells of 32 ----------

        static Px FxSheet()
        {
            var sheet = new Px(256, 128);
            for (int f = 0; f < 4; f++)
            {
                var puff = new Px(32, 32);
                float r = 4f + f * 3f;
                var c = Px.Lerp(Px.C(1f, 0.95f, 0.75f), Px.C(0.55f, 0.55f, 0.55f), f / 3f);
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float d = new Vector2(x - 15.5f, y - 15.5f).magnitude;
                        if (d < r && Px.Hash(x, y, 90 + f) > f * 0.18f) puff.Set(x, y, c);
                    }
                sheet.Blit(puff, f * 32, 0);

                var blood = new Px(32, 32);
                for (int i = 0; i < 6 + f * 2; i++)
                {
                    float a = i * 1.7f + f;
                    float dist = (1f + f) * 2.5f * Px.Hash(i, f, 91);
                    blood.Circle(16f + Mathf.Cos(a) * dist, 16f + Mathf.Sin(a) * dist + f, 3.5f - f * 0.6f, i % 2 == 0 ? Px.Hex(0xc01818) : Px.Hex(0x7a0a0a));
                }
                sheet.Blit(blood, f * 32, 32);
            }
            for (int f = 0; f < 6; f++)
            {
                var blast = new Px(32, 32);
                float r = 6f + f * 1.8f;
                float t = f / 5f;
                var outer = Px.Lerp(Px.C(1f, 0.55f, 0.1f), Px.C(0.35f, 0.32f, 0.3f), t);
                var inner = Px.Lerp(Px.C(1f, 1f, 0.7f), Px.C(0.5f, 0.45f, 0.4f), t);
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float d = new Vector2(x - 15.5f, y - 15.5f).magnitude + Px.Hash(x, y, 95 + f) * 3f;
                        if (d < r * 0.55f) blast.Set(x, y, inner);
                        else if (d < r && Px.Hash(x, y, 99) > t * 0.5f) blast.Set(x, y, outer);
                    }
                sheet.Blit(blast, f * 32, 64);
            }
            for (int f = 0; f < 2; f++)
            {
                var orb = new Px(32, 32);
                orb.Circle(15.5f, 15.5f, 11f - f * 1.5f, Px.C(0.85f, 0.85f, 0.85f));
                orb.Circle(15.5f, 15.5f, 6f - f, Px.C(1f, 1f, 1f));
                sheet.Blit(orb, f * 32, 96);
            }
            return sheet;
        }

        // ---------- The gun in your hands: 3 frames of 160 x 120 ----------

        enum ViewKind { Fist, Pistol, Shotgun, Rivet, Launcher }

        static Px View(ViewKind kind)
        {
            const int W = 160, H = 120;
            var sheet = new Px(W * 3, H);
            var skin = Px.Hex(0xc8906a);
            var skinDark = Px.Hex(0x8a5a40);
            var metal = Px.Hex(0x3a3d44);
            var metalLight = Px.Hex(0x6a707a);
            var flash = Px.C(1f, 0.9f, 0.35f);
            var flashCore = Px.C(1f, 1f, 0.85f);
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(W, H);
                float rise = f == 1 ? -8f : f == 2 ? -3f : 0f;
                switch (kind)
                {
                    case ViewKind.Fist:
                    {
                        float fx = f == 1 ? 80f : 118f, fy = f == 1 ? 58f : 92f;
                        float size = f == 1 ? 24f : 20f;
                        p.Poly(skinDark, fx - 12f, fy + 10f, fx + 12f, fy + 10f, fx + 26f, H, fx - 4f, H);
                        p.Ellipse(fx, fy, size, size * 0.8f, skin);
                        for (int k = 0; k < 4; k++) p.Rect((int)(fx - size * 0.7f + k * size * 0.38f), (int)(fy - size * 0.55f), (int)(fx - size * 0.7f + k * size * 0.38f) + 1, (int)(fy - size * 0.1f), skinDark);
                        if (f == 0) p.Ellipse(36f, 104f, 16f, 14f, skin);
                        break;
                    }
                    case ViewKind.Pistol:
                    {
                        float y = 56f + rise;
                        p.Rect(72, (int)y, 88, (int)y + 34, metal);
                        p.Rect(74, (int)y + 2, 76, (int)y + 30, metalLight);
                        p.Poly(skin, 62f, y + 30f, 98f, y + 30f, 110f, H, 54f, H);
                        p.Rect(70, (int)y + 34, 90, (int)y + 44, skinDark);
                        if (f == 1) { p.Circle(80f, y - 8f, 12f, flash); p.Circle(80f, y - 8f, 6f, flashCore); }
                        break;
                    }
                    case ViewKind.Shotgun:
                    {
                        float y = 48f + rise;
                        p.Poly(metal, 66f, y, 94f, y, 100f, H, 60f, H);
                        p.Poly(metalLight, 70f, y + 2f, 78f, y + 2f, 76f, H, 66f, H);
                        p.Poly(metalLight, 82f, y + 2f, 90f, y + 2f, 94f, H, 84f, H);
                        p.Rect(60, (int)y + 50, 100, (int)y + 62, Px.Hex(0x7a4a2a));
                        p.Poly(skin, 50f, y + 60f, 70f, y + 56f, 74f, H, 44f, H);
                        if (f == 1) { p.Circle(80f, y - 10f, 20f, flash); p.Circle(80f, y - 10f, 10f, flashCore); }
                        if (f == 2) { p.Circle(74f, y - 6f, 6f, Px.C(0.6f, 0.6f, 0.6f)); p.Circle(86f, y - 12f, 5f, Px.C(0.5f, 0.5f, 0.5f)); }
                        break;
                    }
                    case ViewKind.Rivet:
                    {
                        float y = 46f + rise * 0.5f;
                        for (int k = 0; k < 3; k++) p.Rect(66 + k * 10, (int)y, 72 + k * 10, (int)y + 44, k == 1 ? metalLight : metal);
                        p.Rect(60, (int)y + 40, 100, (int)y + 74, metal);
                        p.Rect(64, (int)y + 46, 96, (int)y + 50, Px.Hex(0xe0b040));
                        p.Poly(skin, 96f, y + 60f, 120f, y + 58f, 130f, H, 100f, H);
                        if (f > 0) { p.Circle(f == 1 ? 69f : 89f, y - 6f, 11f, flash); p.Circle(f == 1 ? 69f : 89f, y - 6f, 5f, flashCore); }
                        break;
                    }
                    default:
                    {
                        float y = 44f + rise;
                        p.Poly(Px.Hex(0x2f4a5a), 62f, y, 98f, y, 108f, H, 52f, H);
                        p.Rect(64, (int)y + 2, 96, (int)y + 6, Px.Hex(0x5a8aa0));
                        p.Rect(72, (int)y + 30, 88, (int)y + 50, f == 1 ? Px.C(0.8f, 1f, 1f) : Px.Hex(0x40c8ff));
                        p.Poly(skin, 100f, y + 56f, 118f, y + 52f, 128f, H, 104f, H);
                        if (f == 1) { p.Circle(80f, y - 8f, 16f, Px.C(0.5f, 0.9f, 1f)); p.Circle(80f, y - 8f, 8f, Px.C(0.9f, 1f, 1f)); }
                        break;
                    }
                }
                p.Outline(Px.C(0.04f, 0.04f, 0.05f));
                sheet.Blit(p, f * W, 0);
            }
            return sheet;
        }

        // ---------- How to Play pictures (128 x 128) ----------

        static Px HowMove()
        {
            var p = new Px(128, 128);
            var key = Px.Hex(0x3a3a5a);
            var top = Px.Hex(0xffc94a);
            void Key(int x, int y, bool lit) { p.Rect(x, y, x + 30, y + 30, lit ? top : key); p.Rect(x + 3, y + 3, x + 27, y + 27, lit ? Px.Mul(top, 1.1f) : Px.Mul(key, 1.3f)); }
            Key(49, 22, true);
            Key(15, 58, false);
            Key(49, 58, false);
            Key(83, 58, false);
            p.Ellipse(64f, 108f, 26f, 12f, Px.Hex(0x22223a));
            return p;
        }

        static Px HowShoot()
        {
            var p = new Px(128, 128);
            p.Fill(Px.Hex(0x15152a));
            var enemy = new Px(64, 64);
            Figure(enemy, Designs.Gunner, 0f, Pose.Attack);
            enemy.Outline(Px.C(0.05f, 0.04f, 0.06f));
            p.Blit(enemy, 32, 20);
            p.Rect(62, 52, 65, 75, Px.C(1f, 1f, 1f));
            p.Rect(52, 62, 75, 65, Px.C(1f, 1f, 1f));
            return p;
        }

        static Px HowKeys()
        {
            var p = new Px(128, 128);
            p.Fill(Px.Hex(0x15152a));
            p.Rect(70, 14, 118, 118, Px.Hex(0x707070));
            p.Rect(70, 40, 118, 50, Px.Hex(0x4a7aff));
            p.Rect(70, 86, 118, 96, Px.Hex(0x4a7aff));
            var card = KeyCard();
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    var c = card.Get(x, y);
                    if (c.a == 0) continue;
                    var t = c.r > 200 ? Px.Hex(0x4a7aff) : c;
                    p.Rect(10 + x * 2 - 1, 40 + y * 2 - 1, 10 + x * 2, 40 + y * 2, t);
                }
            return p;
        }

        static Px HowExit()
        {
            var p = new Px(128, 128);
            p.Fill(Px.Hex(0x15152a));
            var sign = ExitSign();
            for (int y = 0; y < sign.h; y++)
                for (int x = 0; x < sign.w; x++)
                    p.Rect(x * 2 - 1, 20 + y * 2 - 1, x * 2, 20 + y * 2, sign.Get(x, y));
            var pad = ExitPad();
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 128; x++)
                    p.Set(x, 90 + y, pad.Get(x % 64, (y * 2) % 64));
            return p;
        }
    }
}
