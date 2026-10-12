using System.Collections.Generic;
using JamKit.Doomlike;
using JamKit.VisitingHours.Characters;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>Something to look at with E, placed in the world.</summary>
    public sealed class VhSpot
    {
        public ExamineDef def;
        public Vector3 position;
        /// <summary>Extra reach (big things like a house front can be examined from further away).</summary>
        public float radius;
        public Billboard billboard;
    }

    /// <summary>A place built for play: its root, where Nadia stands, what she can examine and the
    /// billboards that turn to face her.</summary>
    public sealed class VhArea
    {
        public SliceArea area;
        public Transform root;
        public Vector3 spawn;
        public float spawnYaw;
        public readonly List<VhSpot> spots = new List<VhSpot>();
        public readonly List<Billboard> faced = new List<Billboard>();
        /// <summary>Walt (on the street), the dog and where the heart monitor beeps (hospital).</summary>
        public Billboard walt, dog;
        public Vector3 monitor;
        public Vector3 dogHome;

        // ---------- The ward's bedside (docs/redesign/day.md) ----------
        /// <summary>Room 14's live monitor (its beeps are its own).</summary>
        public VhPatientMonitor patientMonitor;
        /// <summary>Elias's near hand on the blanket, the stand-in's (it answers her: the fingers move). Null when the 3D
        /// model lies there (<see cref="eliasFingers"/>).</summary>
        public VhHandRig eliasHand;
        /// <summary>The 3D Elias (People's EliasBed) and his near hand's fingers, when the model is in the build.</summary>
        public VhCharacter elias;
        public VhBedHand eliasFingers;
        /// <summary>The place's 3D people (People's prefabs): heads turn to her, mouths move when they speak.</summary>
        public readonly List<VhCharacter> cast = new List<VhCharacter>();
        /// <summary>The rooms (per-room culling: their props and lights go when she can't see into them).</summary>
        public VhWardRooms rooms;

        /// <summary>His hand is there to hold (the model's or the stand-in's).</summary>
        public bool HasEliasHand => eliasHand != null || (eliasFingers != null && eliasFingers.Ready);

        /// <summary>His fingers move against hers (<paramref name="amount"/> 0..1 over <paramref name="seconds"/>).</summary>
        public void TwitchElias(float amount, float seconds)
        {
            if (eliasHand != null) eliasHand.Twitch(amount, seconds);
            if (eliasFingers != null) eliasFingers.Twitch(amount, seconds);
        }

        /// <summary>His hand at rest again (she let go).</summary>
        public void RestEliasHand()
        {
            if (eliasHand != null) eliasHand.curl = 0.22f;
            if (eliasFingers != null) eliasFingers.curl = 0.1f;
        }

        /// <summary>The 3D person for a speaker's model name ("Mags", "Rafael", "Representative"), or null.</summary>
        public VhCharacter Person(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            for (int i = 0; i < cast.Count; i++)
                if (cast[i] != null && cast[i].characterName == model) return cast[i];
            return null;
        }
        /// <summary>Where her eyes are when she sits in the chair by his bed, and which way she looks (yaw, pitch down).</summary>
        public Vector3 bedsideEye;
        public float bedsideYaw, bedsidePitch;
        /// <summary>Where she stands to sit down, and stands back up to.</summary>
        public Vector3 bedsideStand;
        /// <summary>Where her hand rests on his (world, the back of his hand) and which way his fingers point.</summary>
        public Vector3 bedsideHold;
        public Vector3 bedsideHandForward = Vector3.forward;
        /// <summary>The bedside's shallow focus (weight 0 until she sits).</summary>
        public UnityEngine.Rendering.Volume bedsideFocus;
        /// <summary>The booth: where her eyes are in the dream chair (she wakes there when the house threw her out) and
        /// which way she looks.</summary>
        public Vector3 chairEye;
        public float chairYaw;

        public VhSpot Spot(string id)
        {
            foreach (var s in spots) if (s.def != null && s.def.id == id) return s;
            return null;
        }
    }

    /// <summary>
    /// Building blocks for the slice's places, made in code each time a place starts (like the
    /// DOOM-like's levels): tiled floors and walls with colliders (world-space UVs, so textures
    /// keep their scale), painted flats, people as the DOOM-like's billboards, and a sky drum.
    /// </summary>
    public static class VhWorldKit
    {
        static readonly int TexId = Shader.PropertyToID("_MainTex");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static MaterialPropertyBlock _block;

        static void Paint(MeshRenderer mr, Texture tex, Color tint)
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.Clear();
            if (tex != null) _block.SetTexture(TexId, tex);
            _block.SetColor(ColorId, tint);
            mr.SetPropertyBlock(_block);
        }

        static MeshRenderer Renderer(string name, Transform parent, Mesh mesh, Material mat, Texture tex, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Paint(mr, tex, tint);
            return mr;
        }

        /// <summary>A flat floor (or ceiling, facing down) over x0..x1, z0..z1 at height y; one texture repeat per <paramref name="tile"/> metres.</summary>
        public static MeshRenderer Floor(string name, Transform parent, Material mat, Texture tex, Color tint,
            float x0, float z0, float x1, float z1, float y, float tile, bool collider = true, bool ceiling = false)
        {
            var batch = new MeshBatch();
            var a = new Vector3(x0, y, z0);
            var b = new Vector3(x0, y, z1);
            var c = new Vector3(x1, y, z1);
            var d = new Vector3(x1, y, z0);
            Color32 w = Color.white;
            var ua = new Vector2(x0 / tile, z0 / tile);
            var ub = new Vector2(x0 / tile, z1 / tile);
            var uc = new Vector2(x1 / tile, z1 / tile);
            var ud = new Vector2(x1 / tile, z0 / tile);
            if (ceiling) batch.Quad(d, c, b, a, ud, uc, ub, ua, w, w);
            else batch.Quad(a, b, c, d, ua, ub, uc, ud, w, w);
            var mr = Renderer(name, parent, batch.ToMesh(name), mat, tex, tint);
            if (collider)
            {
                var box = mr.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3((x0 + x1) * 0.5f, y - 0.25f, (z0 + z1) * 0.5f);
                box.size = new Vector3(Mathf.Abs(x1 - x0), 0.5f, Mathf.Abs(z1 - z0));
            }
            return mr;
        }

        /// <summary>A wall from <paramref name="from"/> to <paramref name="to"/> (on the floor), <paramref name="height"/> tall,
        /// with a collider. Seen from both sides.</summary>
        public static MeshRenderer Wall(string name, Transform parent, Material mat, Texture tex, Color tint,
            Vector3 from, Vector3 to, float height, float tile, bool collider = true)
        {
            var batch = new MeshBatch();
            float len = Vector3.Distance(from, to);
            var up = Vector3.up * height;
            Color32 w = Color.white;
            batch.Quad(from, from + up, to + up, to,
                new Vector2(0f, 0f), new Vector2(0f, height / tile), new Vector2(len / tile, height / tile), new Vector2(len / tile, 0f), w, w);
            var mr = Renderer(name, parent, batch.ToMesh(name), mat, tex, tint);
            if (collider) AddWallCollider(mr.gameObject, from, to, height);
            return mr;
        }

        /// <summary>An invisible wall (the edge of a place).</summary>
        public static void Barrier(string name, Transform parent, Vector3 from, Vector3 to, float height = 4f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AddWallCollider(go, from, to, height);
        }

        static void AddWallCollider(GameObject go, Vector3 from, Vector3 to, float height)
        {
            var holder = new GameObject("Collider");
            holder.transform.SetParent(go.transform, false);
            Vector3 mid = (from + to) * 0.5f + Vector3.up * height * 0.5f;
            Vector3 dir = to - from;
            holder.transform.position = mid;
            if (dir.sqrMagnitude > 1e-6f) holder.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var box = holder.AddComponent<BoxCollider>();
            box.size = new Vector3(0.2f, height, dir.magnitude);
        }

        /// <summary>A solid box (a desk, a counter), every face textured.</summary>
        public static MeshRenderer Block(string name, Transform parent, Material mat, Texture tex, Color tint, Vector3 center, Vector3 size, float tile = 1f)
        {
            var batch = new MeshBatch();
            Vector3 h = size * 0.5f;
            Vector3 c = center;
            Color32 w = Color.white;
            // Four sides and the top (the bottom is never seen).
            Side(batch, c + new Vector3(-h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, -h.z), size.y, tile, w);
            Side(batch, c + new Vector3(h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, h.z), size.y, tile, w);
            Side(batch, c + new Vector3(h.x, -h.y, h.z), c + new Vector3(-h.x, -h.y, h.z), size.y, tile, w);
            Side(batch, c + new Vector3(-h.x, -h.y, h.z), c + new Vector3(-h.x, -h.y, -h.z), size.y, tile, w);
            float y = c.y + h.y;
            batch.Quad(new Vector3(c.x - h.x, y, c.z - h.z), new Vector3(c.x - h.x, y, c.z + h.z), new Vector3(c.x + h.x, y, c.z + h.z), new Vector3(c.x + h.x, y, c.z - h.z),
                new Vector2(0f, 0f), new Vector2(0f, size.z / tile), new Vector2(size.x / tile, size.z / tile), new Vector2(size.x / tile, 0f), w, w);
            var mr = Renderer(name, parent, batch.ToMesh(name), mat, tex, tint);
            var box = mr.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            return mr;
        }

        static void Side(MeshBatch batch, Vector3 a, Vector3 d, float height, float tile, Color32 w)
        {
            float len = Vector3.Distance(a, d);
            var up = Vector3.up * height;
            batch.Quad(a, a + up, d + up, d, new Vector2(0f, 0f), new Vector2(0f, height / tile), new Vector2(len / tile, height / tile), new Vector2(len / tile, 0f), w, w);
        }

        /// <summary>Height for a picture <paramref name="width"/> metres wide, keeping its proportions.</summary>
        public static float HeightFor(Texture tex, float width) => tex != null && tex.width > 0 ? width * tex.height / tex.width : width;

        /// <summary>A painted flat standing on the ground at <paramref name="feet"/>, facing <paramref name="yaw"/>
        /// (0 = it faces -Z): stage-set houses. It doesn't turn. Optional thin collider.</summary>
        public static Billboard Flat(string name, Transform parent, Material mat, Texture tex, Color tint, Vector3 feet, float width, float yaw, bool collider)
        {
            float height = HeightFor(tex, width);
            var b = Billboard.Create(name, parent, feet, width, height, mat, tex, tint);
            b.Face(yaw);
            if (collider)
            {
                var holder = new GameObject("Collider");
                holder.transform.SetParent(b.transform, false);
                holder.transform.localPosition = new Vector3(0f, height * 0.5f, 0.1f);
                var box = holder.AddComponent<BoxCollider>();
                box.size = new Vector3(width * 0.92f, height, 0.4f);
            }
            return b;
        }

        /// <summary>A person or prop that turns to face Nadia (added to <paramref name="area"/>'s faced list),
        /// <paramref name="height"/> metres tall.</summary>
        public static Billboard Person(string name, VhArea area, Material mat, Texture tex, Color tint, Vector3 feet, float height)
        {
            float width = tex != null && tex.height > 0 ? height * tex.width / tex.height : height * 0.45f;
            var b = Billboard.Create(name, area.root, feet, width, height, mat, tex, tint);
            area.faced.Add(b);
            return b;
        }

        /// <summary>A picture lying flat (a newspaper on a porch), <paramref name="width"/> metres across.</summary>
        public static Billboard Lying(string name, Transform parent, Material mat, Texture tex, Color tint, Vector3 at, float width, float yaw)
        {
            float depth = HeightFor(tex, width);
            var b = Billboard.Create(name, parent, at, width, depth, mat, tex, tint);
            b.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            b.transform.position = at + b.transform.rotation * new Vector3(0f, -depth * 0.5f, 0f);
            return b;
        }

        /// <summary>A drum of sky around a place (seen from inside), <paramref name="repeats"/> copies of the panorama around.</summary>
        public static MeshRenderer SkyDrum(string name, Transform parent, Material mat, Texture tex, Color tint, Vector3 centre, float radius, float height, float bottom, int repeats = 2)
        {
            const int segments = 48;
            var batch = new MeshBatch();
            Color32 w = Color.white;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                var p0 = centre + new Vector3(Mathf.Cos(a0) * radius, bottom, Mathf.Sin(a0) * radius);
                var p1 = centre + new Vector3(Mathf.Cos(a1) * radius, bottom, Mathf.Sin(a1) * radius);
                float u0 = (float)i / segments * repeats, u1 = (float)(i + 1) / segments * repeats;
                batch.Quad(p0, p0 + Vector3.up * height, p1 + Vector3.up * height, p1,
                    new Vector2(u0, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f), w, w);
            }
            return Renderer(name, parent, batch.ToMesh(name), mat, tex, tint);
        }
    }
}
