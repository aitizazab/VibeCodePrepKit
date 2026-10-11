using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The opening editor's view (docs/OPENING-EDITOR.md): what a click picks (the nearest surface the mouse ray meets,
    /// walls and floors included, so a click never selects something hidden behind a wall), the outlines (white: what a
    /// click would pick; orange: the selection, with its name), the marks for shots and lights, and the handles, drawn
    /// and hit-tested in screen space at a constant size on screen.
    /// </summary>
    public sealed partial class VhOpeningEditor
    {
        sealed class Pickable
        {
            public Renderer r;
            public VhEditable owner;
            public Mesh mesh;
            public Vector3[] verts;
            public int[] tris;
            public bool triedMesh, skinned;
            public Mesh baked;
            public float bakedAt = -10f;
            public Matrix4x4 bakedToWorld;
        }

        readonly List<Pickable> _pickables = new List<Pickable>();
        readonly Dictionary<VhEditable, Renderer[]> _renderers = new Dictionary<VhEditable, Renderer[]>();
        readonly Vector3[] _corners = new Vector3[8];
        readonly Vector2[] _cornersPanel = new Vector2[8];

        /// <summary>The last pick's ray met something that can't be moved (a wall, the floor).</summary>
        bool PickedSomething;

        // ---------- What can be picked ----------

        void BuildPickables()
        {
            foreach (var p in _pickables)
                if (p.baked != null) Destroy(p.baked);
            _pickables.Clear();
            _renderers.Clear();
            if (_stage == null) return;
            var owners = new Dictionary<Transform, VhEditable>();
            foreach (var e in _stage.edits.All)
                if (!e.isShot && e.target != null) owners[e.target] = e;
            var phone = _stage.edits.Find("phone");
            // (The phone's own parts belong to "phone"; a pick turns that into the held phone while she holds it.)
            foreach (var r in _stage.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                VhEditable owner = null;
                for (var t = r.transform; t != null && t != _stage.transform; t = t.parent)
                {
                    if (phone != null && t == _stage.phone)
                    {
                        owner = phone;
                        break;
                    }
                    if (owners.TryGetValue(t, out owner)) break;
                }
                _pickables.Add(new Pickable { r = r, owner = owner, skinned = r is SkinnedMeshRenderer });
            }
        }

        /// <summary>What a thing is drawn with (the phone: the phone itself, not the mark of where it rests).</summary>
        Renderer[] Renderers(VhEditable e)
        {
            if (!_renderers.TryGetValue(e, out var r) || r == null)
            {
                Transform root = IsPhone(e) && _stage != null && _stage.phone != null ? _stage.phone : e.target;
                r = root != null && !e.isShot ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                _renderers[e] = r;
            }
            return r;
        }

        static bool Drawn(Renderer r) => r != null && r.enabled && r.gameObject.activeInHierarchy;

        /// <summary>The resting phone or the held one: both are drawn by the phone itself.</summary>
        static bool IsPhone(VhEditable e) => e != null && (e.id == "phone" || e.id == "phone-held");

        /// <summary>Shots and lights (nothing drawn): shown as marks you can click.</summary>
        bool IsMark(VhEditable e)
        {
            if (e.isShot) return true;
            foreach (var r in Renderers(e))
                if (r != null) return false;
            return true;
        }

        Bounds BoundsOf(VhEditable e)
        {
            Bounds b = default;
            bool any = false;
            foreach (var r in Renderers(e))
            {
                if (!Drawn(r)) continue;
                if (!any) b = r.bounds;
                else b.Encapsulate(r.bounds);
                any = true;
            }
            if (!any) b = new Bounds(e.target != null ? e.target.position : Vector3.zero, Vector3.one * 0.2f);
            return b;
        }

        /// <summary>The thing under the panel point <paramref name="m"/>: a shot's or light's mark near it, else the owner
        /// of the nearest surface the mouse ray meets (null when that's a wall or nothing).</summary>
        VhEditable PickThing(Vector2 m)
        {
            PickedSomething = false;
            if (_stage == null || _stage.cam == null) return null;
            foreach (var e in _stage.edits.All)
            {
                if (e.target == null || !e.target.gameObject.activeInHierarchy || !IsMark(e)) continue;
                if (ToPanel(e.target.position, out var c) && (c - m).sqrMagnitude < 14f * 14f) return e;
            }
            var ray = _stage.cam.ScreenPointToRay(PanelToScreen(m));
            float best = float.MaxValue;
            VhEditable owner = null;
            bool any = false;
            foreach (var p in _pickables)
            {
                if (!Drawn(p.r)) continue;
                var bounds = p.r.bounds;
                float d0;
                if (bounds.Contains(ray.origin)) d0 = 0f;
                else if (!bounds.IntersectRay(ray, out d0)) continue;
                if (d0 > best) continue;
                if (!Precise(p, ray, out float t)) continue;
                if (t < best)
                {
                    best = t;
                    owner = p.owner;
                    any = true;
                }
            }
            PickedSomething = any && owner == null;
            // The phone in her hand is the held phone (its resting place is elsewhere at this moment).
            if (owner != null && owner.id == "phone" && _stage.PhoneInHand)
            {
                var held = _stage.edits.Find("phone-held");
                if (held != null) owner = held;
            }
            return owner;
        }

        /// <summary>Where the ray meets the renderer's triangles (its bounds when its mesh can't be read).</summary>
        bool Precise(Pickable p, Ray ray, out float t)
        {
            t = float.MaxValue;
            Matrix4x4 toWorld;
            Vector3[] v;
            int[] tri;
            if (p.skinned)
            {
                var smr = (SkinnedMeshRenderer)p.r;
                if (smr.sharedMesh == null) return BoundsHit(p, ray, out t);
                if (p.baked == null || Time.unscaledTime - p.bakedAt > 1f)
                {
                    if (p.baked == null) p.baked = new Mesh { name = "pick bake" };
                    smr.BakeMesh(p.baked, true);
                    p.bakedAt = Time.unscaledTime;
                    p.verts = p.baked.vertices;
                    p.tris = p.baked.triangles;
                    // Baked with the scale: relative to the renderer's place and turn. Check against its bounds.
                    var tr = smr.transform;
                    p.bakedToWorld = Matrix4x4.TRS(tr.position, tr.rotation, Vector3.one);
                    var bb = p.baked.bounds;
                    if ((p.bakedToWorld.MultiplyPoint3x4(bb.center) - smr.bounds.center).magnitude > smr.bounds.extents.magnitude + 0.05f)
                        p.bakedToWorld = tr.localToWorldMatrix;
                }
                toWorld = p.bakedToWorld;
                v = p.verts;
                tri = p.tris;
            }
            else
            {
                if (!p.triedMesh)
                {
                    p.triedMesh = true;
                    var mf = p.r.GetComponent<MeshFilter>();
                    p.mesh = mf != null ? mf.sharedMesh : null;
                    if (p.mesh != null && p.mesh.isReadable)
                    {
                        p.verts = p.mesh.vertices;
                        p.tris = p.mesh.triangles;
                    }
                }
                if (p.verts == null || p.tris == null) return BoundsHit(p, ray, out t);
                toWorld = p.r.transform.localToWorldMatrix;
                v = p.verts;
                tri = p.tris;
            }
            if (v == null || tri == null || v.Length == 0) return BoundsHit(p, ray, out t);
            var toLocal = toWorld.inverse;
            Vector3 o = toLocal.MultiplyPoint3x4(ray.origin), d = toLocal.MultiplyVector(ray.direction);
            bool hit = false;
            for (int i = 0; i + 2 < tri.Length; i += 3)
            {
                if (RayTriangle(o, d, v[tri[i]], v[tri[i + 1]], v[tri[i + 2]], out float s) && s < t)
                {
                    t = s;
                    hit = true;
                }
            }
            return hit;
        }

        static bool BoundsHit(Pickable p, Ray ray, out float t)
        {
            var b = p.r.bounds;
            if (b.Contains(ray.origin))
            {
                t = 0.02f;
                return true;
            }
            bool hit = b.IntersectRay(ray, out t);
            t += 0.02f;
            return hit;
        }

        /// <summary>Möller-Trumbore, both sides; <paramref name="t"/> along the (unnormalised) direction.</summary>
        static bool RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0f;
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 pv = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, pv);
            if (Mathf.Abs(det) < 1e-12f) return false;
            float inv = 1f / det;
            Vector3 tv = o - a;
            float u = Vector3.Dot(tv, pv) * inv;
            if (u < 0f || u > 1f) return false;
            Vector3 qv = Vector3.Cross(tv, e1);
            float w = Vector3.Dot(d, qv) * inv;
            if (w < 0f || u + w > 1f) return false;
            t = Vector3.Dot(e2, qv) * inv;
            return t > 0f;
        }

        // ---------- Screen space ----------

        bool ToPanel(Vector3 world, out Vector2 p)
        {
            p = Vector2.zero;
            if (_stage == null || _stage.cam == null || _root == null || _root.panel == null) return false;
            Vector3 sp = _stage.cam.WorldToScreenPoint(world);
            if (sp.z <= 0.01f) return false;
            p = ScreenToPanel(new Vector2(sp.x, sp.y));
            return true;
        }

        float PanelHeight()
        {
            float h = _root != null ? _root.worldBound.height : 0f;
            return float.IsNaN(h) || h < 10f ? 1080f : h;
        }

        Vector3 Pivot(VhEditable e) => e.target.position;

        /// <summary>The handles' length in the world: <see cref="HandlePixels"/> on screen at the thing's distance.</summary>
        float HandleLength(VhEditable e)
        {
            var cam = _stage.cam;
            float depth = Vector3.Dot(Pivot(e) - cam.transform.position, cam.transform.forward);
            depth = Mathf.Max(0.05f, depth);
            return HandlePixels * 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / PanelHeight();
        }

        Vector3 Axis(VhEditable e, int i)
        {
            Vector3 a = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            return _tool == Tool.Scale ? e.target.rotation * a : a;
        }

        static Vector3 RingPoint(Vector3 o, Vector3 axis, float r, float angle)
        {
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            return o + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * r;
        }

        static float SegmentDistance(Vector2 m, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(m - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(m, a + ab * t);
        }

        bool HandlesShown => _sel != null && _sel.target != null && _sel.target.gameObject.activeInHierarchy && !_playing;

        bool ToolApplies(VhEditable e) => _tool != Tool.Scale || (e.canScale && !e.isShot);

        /// <summary>The handle under the panel point (0..2 an axis, 3 the middle square or box), or -1.</summary>
        int HitHandle(Vector2 m, out Vector3 ringPoint)
        {
            ringPoint = Vector3.zero;
            if (!HandlesShown || !ToolApplies(_sel)) return -1;
            Vector3 o = Pivot(_sel);
            if (!ToPanel(o, out var po)) return -1;
            float L = HandleLength(_sel);
            if (_tool != Tool.Rotate && Mathf.Abs(m.x - po.x) < 11f && Mathf.Abs(m.y - po.y) < 11f) return 3;
            int best = -1;
            float bestD = 12f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 a = Axis(_sel, i);
                if (_tool == Tool.Rotate)
                {
                    for (int k = 0; k < 64; k++)
                    {
                        Vector3 w0 = RingPoint(o, a, L * 0.9f, k / 64f * Mathf.PI * 2f), w1 = RingPoint(o, a, L * 0.9f, (k + 1) / 64f * Mathf.PI * 2f);
                        if (!ToPanel(w0, out var q0) || !ToPanel(w1, out var q1)) continue;
                        float d = SegmentDistance(m, q0, q1);
                        if (d < bestD)
                        {
                            bestD = d;
                            best = i;
                            ringPoint = (w0 + w1) * 0.5f;
                        }
                    }
                    continue;
                }
                if (!ToPanel(o + a * L, out var pe)) continue;
                if ((pe - po).sqrMagnitude < 15f * 15f) continue; // pointing at the camera: use the others
                float dd = SegmentDistance(m, po + (pe - po) * 0.15f, pe);
                if (dd < bestD)
                {
                    bestD = dd;
                    best = i;
                }
            }
            return best;
        }

        // ---------- Drawing ----------

        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color HoverYellow = new Color(1f, 0.93f, 0.35f);

        static void Line(Painter2D p, Vector2 a, Vector2 b, Color c, float w)
        {
            p.strokeColor = Shadow;
            p.lineWidth = w + 2.5f;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
            p.strokeColor = c;
            p.lineWidth = w;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        static void Circle(Painter2D p, Vector2 c, float r, Color col, float w)
        {
            p.strokeColor = Shadow;
            p.lineWidth = w + 2.5f;
            p.BeginPath();
            p.Arc(c, r, 0f, 360f);
            p.Stroke();
            p.strokeColor = col;
            p.lineWidth = w;
            p.BeginPath();
            p.Arc(c, r, 0f, 360f);
            p.Stroke();
        }

        static void Square(Painter2D p, Vector2 c, float half, Color col, bool fill)
        {
            p.BeginPath();
            p.MoveTo(c + new Vector2(-half, -half));
            p.LineTo(c + new Vector2(half, -half));
            p.LineTo(c + new Vector2(half, half));
            p.LineTo(c + new Vector2(-half, half));
            p.ClosePath();
            if (fill)
            {
                p.fillColor = col;
                p.Fill();
            }
            p.strokeColor = Shadow;
            p.lineWidth = 1.5f;
            p.Stroke();
        }

        void Draw(MeshGenerationContext ctx)
        {
            if (!_open || _stage == null || _stage.cam == null) return;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            DrawMarks(p);
            if (_hover != null && _hover != _sel && _hover.target != null) Outline(p, _hover, new Color(1f, 1f, 1f, 0.85f), 1.5f, out _);
            if (_sel == null || _sel.target == null || !_sel.target.gameObject.activeInHierarchy) return;
            Outline(p, _sel, SelColor, 2.5f, out _);
            if (_sel.isShot) Frustum(p, _sel);
            if (HandlesShown && ToolApplies(_sel)) DrawHandles(p);
        }

        /// <summary>The name tags over the hovered and the selected thing (or, when the selection is out of view, a tag at
        /// the edge saying so). Each frame, from Update (styles aren't changed while drawing).</summary>
        void PlaceTags()
        {
            if (_tagLayer == null || _stage == null || _stage.cam == null) return;
            HideTag(_selTag);
            HideTag(_hoverTag);
            HideTag(_offTag);
            if (_hover != null && _hover != _sel && _hover.target != null)
            {
                Outline(null, _hover, Color.white, 0f, out var top);
                if (top.HasValue) PlaceTag(_hoverTag, _hover.label, top.Value);
            }
            if (_sel == null || _sel.target == null || !_sel.target.gameObject.activeInHierarchy) return;
            Outline(null, _sel, SelColor, 0f, out var selTop);
            var camT = _stage.cam.transform;
            // A shot the camera sits on (looked through, or the shot camera): it's this view, not out of it.
            if (_sel.isShot && Vector3.Distance(camT.position, _sel.target.position) < 0.05f) return;
            bool onScreen = ToPanel(BoundsOf(_sel).center, out var centre) && OnScreen(centre);
            if (selTop.HasValue && OnScreen(selTop.Value)) PlaceTag(_selTag, _sel.label, selTop.Value);
            else if (onScreen) PlaceTag(_selTag, _sel.label, centre);
            else
            {
                // Out of view (or behind the camera): say so at the edge, nearest where it is.
                var dir = _sel.target.position - _stage.cam.transform.position;
                Vector3 sp = _stage.cam.WorldToScreenPoint(_sel.target.position);
                if (Vector3.Dot(dir, _stage.cam.transform.forward) < 0f) sp = new Vector3(Screen.width - sp.x, Screen.height - sp.y, 0f);
                var pp = ScreenToPanel(new Vector2(sp.x, sp.y));
                PlaceTag(_offTag, Short(_sel) + " is out of view: F frames it", pp + new Vector2(0f, 28f));
            }
        }

        /// <summary>In the part of the view the panels leave free.</summary>
        bool InView(Vector2 pp)
        {
            var r = ViewArea();
            return pp.x > r.xMin && pp.x < r.xMax && pp.y > r.yMin && pp.y < r.yMax;
        }

        /// <summary>Anywhere on the screen (under a panel too).</summary>
        bool OnScreen(Vector2 pp)
        {
            var r = _root.worldBound;
            return pp.x > r.xMin && pp.x < r.xMax && pp.y > r.yMin && pp.y < r.yMax;
        }

        static void HideTag(Label l)
        {
            if (l != null && l.style.display != DisplayStyle.None) l.style.display = DisplayStyle.None;
        }

        /// <summary>A name tag centred over <paramref name="at"/>, kept inside the part of the view the panels leave free.</summary>
        void PlaceTag(Label l, string text, Vector2 at)
        {
            if (l == null) return;
            SetText(l, text);
            l.style.display = DisplayStyle.Flex;
            var area = ViewArea();
            float w = l.worldBound.width;
            if (float.IsNaN(w) || w < 1f) w = text.Length * 8f;
            float x = Mathf.Clamp(at.x - w * 0.5f, area.xMin + 4f, Mathf.Max(area.xMin + 4f, area.xMax - w - 4f));
            float y = Mathf.Clamp(at.y - 28f, area.yMin + 4f, area.yMax - 30f);
            l.style.left = x;
            l.style.top = y;
        }

        /// <summary>A box round the thing, turned with it (white for hover, orange for the selection); <paramref name="top"/>
        /// is where its name goes (above its top edge on screen). With no painter it only works out <paramref name="top"/>.</summary>
        void Outline(Painter2D p, VhEditable e, Color col, float width, out Vector2? top)
        {
            top = null;
            if (IsMark(e))
            {
                if (ToPanel(e.target.position, out var c))
                {
                    if (p != null) Circle(p, c, e.isShot ? 13f : 11f, col, width);
                    top = c - new Vector2(0f, 12f);
                }
                return;
            }
            Transform frame = IsPhone(e) && _stage.phone != null ? _stage.phone : e.target;
            var toFrame = frame.worldToLocalMatrix;
            Bounds local = default;
            bool any = false;
            foreach (var r in Renderers(e))
            {
                if (!Drawn(r)) continue;
                Bounds rb;
                Matrix4x4 m;
                var mf = r is MeshRenderer ? r.GetComponent<MeshFilter>() : null;
                if (mf != null && mf.sharedMesh != null)
                {
                    rb = mf.sharedMesh.bounds;
                    m = toFrame * r.transform.localToWorldMatrix;
                }
                else
                {
                    rb = r.bounds;
                    m = toFrame;
                }
                Vector3 mn = rb.min, mx = rb.max;
                for (int k = 0; k < 8; k++)
                {
                    var c = m.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? mn.x : mx.x, (k & 2) == 0 ? mn.y : mx.y, (k & 4) == 0 ? mn.z : mx.z));
                    if (!any)
                    {
                        local = new Bounds(c, Vector3.zero);
                        any = true;
                    }
                    else local.Encapsulate(c);
                }
            }
            if (!any) return;
            var toWorld = frame.localToWorldMatrix;
            Vector3 lo = local.min, hi = local.max;
            float minY = float.MaxValue, cx = 0f;
            int seen = 0;
            for (int k = 0; k < 8; k++)
            {
                _corners[k] = toWorld.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? lo.x : hi.x, (k & 2) == 0 ? lo.y : hi.y, (k & 4) == 0 ? lo.z : hi.z));
                if (!ToPanel(_corners[k], out _cornersPanel[k])) return; // the camera is inside it, or it's behind
                if (_cornersPanel[k].y < minY) minY = _cornersPanel[k].y;
                cx += _cornersPanel[k].x;
                seen++;
            }
            for (int k = 0; k < 8 && p != null; k++)
                for (int bit = 1; bit < 8; bit <<= 1)
                    if ((k & bit) == 0) Line(p, _cornersPanel[k], _cornersPanel[k | bit], col, width);
            top = new Vector2(cx / Mathf.Max(1, seen), minY);
        }

        void DrawMarks(Painter2D p)
        {
            foreach (var e in _stage.edits.All)
            {
                if (e.target == null || !e.target.gameObject.activeInHierarchy || !IsMark(e) || e == _throughShot) continue;
                if (!ToPanel(e.target.position, out var c)) continue;
                Color col = e == _sel ? SelColor : e == _hover ? HoverYellow : new Color(1f, 1f, 1f, 0.85f);
                if (e.isShot)
                {
                    // A camera: a box with its lens pointing where it films.
                    Square(p, c, 7f, new Color(col.r, col.g, col.b, 0.55f), true);
                    if (ToPanel(e.target.position + e.target.forward * 0.2f, out var f))
                    {
                        var d = f - c;
                        if (d.sqrMagnitude > 1f) Line(p, c, c + d.normalized * Mathf.Min(34f, Mathf.Max(18f, d.magnitude)), col, 2.5f);
                    }
                }
                else
                {
                    Circle(p, c, 6f, col, 2f);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                        var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Line(p, c + u * 9f, c + u * 14f, col, 2f);
                    }
                }
            }
        }

        void Frustum(Painter2D p, VhEditable shot)
        {
            var t = shot.target;
            float d = 0.35f, h = Mathf.Tan(shot.fov * 0.5f * Mathf.Deg2Rad) * d, w = h * (Screen.width / Mathf.Max(1f, (float)Screen.height));
            if (!ToPanel(t.position, out var o)) return;
            var q = new Vector2[4];
            for (int k = 0; k < 4; k++)
            {
                var c = t.position + t.rotation * new Vector3((k == 0 || k == 3) ? -w : w, k < 2 ? h : -h, d);
                if (!ToPanel(c, out q[k])) return;
            }
            var col = new Color(SelColor.r, SelColor.g, SelColor.b, 0.9f);
            for (int k = 0; k < 4; k++)
            {
                Line(p, o, q[k], col, 1.5f);
                Line(p, q[k], q[(k + 1) % 4], col, 1.5f);
            }
        }

        void DrawHandles(Painter2D p)
        {
            Vector3 o = Pivot(_sel);
            if (!ToPanel(o, out var po)) return;
            float L = HandleLength(_sel);
            int active = _drag == Drag.Handle ? _dragHandle : _hoverHandle;
            for (int i = 0; i < 3; i++)
            {
                Color col = active == i ? HoverYellow : AxisColors[i];
                Vector3 a = Axis(_sel, i);
                if (_tool == Tool.Rotate)
                {
                    Vector2 prev = Vector2.zero;
                    bool started = false;
                    for (int k = 0; k <= 64; k++)
                    {
                        if (!ToPanel(RingPoint(o, a, L * 0.9f, k / 64f * Mathf.PI * 2f), out var q))
                        {
                            started = false;
                            continue;
                        }
                        if (started) Line(p, prev, q, col, active == i ? 4.5f : 3f);
                        prev = q;
                        started = true;
                    }
                    continue;
                }
                if (!ToPanel(o + a * L, out var pe)) continue;
                if ((pe - po).sqrMagnitude < 15f * 15f) continue;
                float shown = (pe - po).magnitude / HandlePixels;
                if (shown < 0.45f && active != i) col.a = 0.55f;
                Line(p, po, pe, col, active == i ? 5f : shown < 0.45f ? 2.5f : 3.5f);
                Vector2 dir = (pe - po).normalized, side = new Vector2(-dir.y, dir.x);
                if (_tool == Tool.Move)
                {
                    p.fillColor = col;
                    p.BeginPath();
                    p.MoveTo(pe + dir * 14f);
                    p.LineTo(pe + side * 7f);
                    p.LineTo(pe - side * 7f);
                    p.ClosePath();
                    p.Fill();
                    p.strokeColor = Shadow;
                    p.lineWidth = 1.5f;
                    p.Stroke();
                }
                else Square(p, pe, 7f, col, true);
            }
            if (_tool != Tool.Rotate)
            {
                Color mid = active == 3 ? HoverYellow : new Color(1f, 1f, 1f, _tool == Tool.Move ? 0.55f : 0.95f);
                Square(p, po, _tool == Tool.Move ? 8f : 7f, mid, true);
            }
        }

        // ---------- For tests and the capture tour ----------

        /// <summary>A screen point (pixels, bottom-left origin) on handle <paramref name="handle"/> of the selection (0..2
        /// the X, Y, Z arrow, ring or box; 3 the middle), where a press grabs exactly that handle.</summary>
        public bool TryHandleScreenPoint(int handle, out Vector2 screen)
        {
            screen = Vector2.zero;
            if (!HandlesShown || !ToolApplies(_sel)) return false;
            Vector3 o = Pivot(_sel);
            if (!ToPanel(o, out var po)) return false;
            if (handle == 3)
            {
                if (HitHandle(po, out _) != 3) return false;
                screen = PanelToScreen(po);
                return true;
            }
            float L = HandleLength(_sel);
            Vector3 a = Axis(_sel, handle);
            for (int k = 0; k < 64; k++)
            {
                Vector3 w = _tool == Tool.Rotate ? RingPoint(o, a, L * 0.9f, k / 64f * Mathf.PI * 2f) : o + a * (L * (0.85f - k * 0.01f));
                if (!ToPanel(w, out var q) || HitHandle(q, out _) != handle || OverUi(q)) continue;
                screen = PanelToScreen(q);
                return true;
            }
            return false;
        }

        /// <summary>A screen point where a click selects the thing called <paramref name="id"/> (a visible part of it, not
        /// under a panel). False when none of it shows.</summary>
        public bool TryThingScreenPoint(string id, out Vector2 screen)
        {
            screen = Vector2.zero;
            var e = _stage != null ? _stage.edits.Find(id) : null;
            if (e == null || e.target == null || !e.target.gameObject.activeInHierarchy) return false;
            if (IsMark(e))
            {
                if (!ToPanel(e.target.position, out var c) || OverUi(c)) return false;
                screen = PanelToScreen(c);
                return true;
            }
            var b = BoundsOf(e);
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            for (int k = 0; k < 8; k++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                if (!ToPanel(c, out var q)) continue;
                mn = Vector2.Min(mn, q);
                mx = Vector2.Max(mx, q);
            }
            if (mn.x > mx.x) return false;
            // From the middle outwards, the first spot where a click picks it.
            for (int ring = 0; ring <= 6; ring++)
                for (int i = -ring; i <= ring; i++)
                    for (int j = -ring; j <= ring; j++)
                    {
                        if (Mathf.Max(Mathf.Abs(i), Mathf.Abs(j)) != ring) continue;
                        var q = (mn + mx) * 0.5f + new Vector2((mx.x - mn.x) * i / 14f, (mx.y - mn.y) * j / 14f);
                        if (!InView(q) || OverUi(q)) continue;
                        if (PickThing(q) != e) continue;
                        screen = PanelToScreen(q);
                        return true;
                    }
            return false;
        }

        /// <summary>A spot in the 3D view clear of the panels (the middle of the screen, nudged below the timeline).</summary>
        public Vector2 ViewScreenPoint()
        {
            var r = _root != null ? _root.worldBound : new Rect(0, 0, 1920, 1080);
            var q = new Vector2(r.center.x, r.center.y + 40f);
            return PanelToScreen(q);
        }
    }
}
