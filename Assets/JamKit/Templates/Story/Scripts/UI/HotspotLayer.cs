using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    /// <summary>
    /// The clickable layer of a hotspot scene: finds the hotspot under the pointer (smallest wins),
    /// outlines it and names it, draws a small marker on each one (dimmer once looked at), and lets
    /// the keyboard or a gamepad move between them (arrows / stick by direction, Tab or shoulder
    /// buttons in reading order, Enter or (A) to use). Picks are in picture fractions (0..1), so the
    /// same data works at any screen size.
    /// </summary>
    public sealed class HotspotLayer
    {
        public readonly VisualElement Root;
        readonly VisualElement _canvas;
        readonly Label _hover, _prompt;
        readonly List<HotspotShape> _shapes = new List<HotspotShape>();
        readonly List<bool> _enabled = new List<bool>();
        readonly List<bool> _visited = new List<bool>();
        HotspotScene _scene;
        int _hoverIndex = -1, _selected = -1;
        bool _active, _showAll, _pointerInside;
        string _heldItemName = "";
        Color _accent = new Color(1f, 0.79f, 0.29f);
        float _pulse;

        /// <summary>A hotspot was clicked or chosen (its index in the scene's list).</summary>
        public Action<int> Activated;
        /// <summary>Right click: drop the held item.</summary>
        public Action RightClicked;

        public bool Active => _active;
        public HotspotScene Scene => _scene;
        public int Selected => _selected;
        public int Hovered => _hoverIndex;
        public bool ShowAll { get => _showAll; set { _showAll = value; _canvas.MarkDirtyRepaint(); } }
        public string HoverText => _hover.style.display == DisplayStyle.Flex ? _hover.text : "";
        public string PromptText => _prompt.text;

        public HotspotLayer(VisualElement parent)
        {
            Root = new VisualElement { name = "hotspots", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("st-hotspots");
            parent.Add(Root);
            _canvas = new VisualElement { pickingMode = PickingMode.Position };
            _canvas.AddToClassList("st-hotspot-canvas");
            Root.Add(_canvas);
            _canvas.generateVisualContent += Draw;
            _prompt = new Label();
            _prompt.AddToClassList("st-hotspot-prompt");
            _prompt.pickingMode = PickingMode.Ignore;
            Root.Add(_prompt);
            _hover = new Label();
            _hover.AddToClassList("st-hotspot-label");
            _hover.pickingMode = PickingMode.Ignore;
            Root.Add(_hover);
            _hover.style.display = DisplayStyle.None;
            _canvas.RegisterCallback<PointerMoveEvent>(OnMove);
            _canvas.RegisterCallback<PointerDownEvent>(OnDown);
            _canvas.RegisterCallback<PointerLeaveEvent>(_ => { _pointerInside = false; SetHover(-1); });
            Root.style.display = DisplayStyle.None;
        }

        public void SetAccent(Color c) => _accent = c;

        /// <summary>Opens the scene with each hotspot's enabled and looked-at state.</summary>
        public void Show(HotspotScene scene, IList<bool> enabled, IList<bool> visited)
        {
            _scene = scene;
            _shapes.Clear();
            if (scene != null && scene.hotspots != null)
                foreach (var h in scene.hotspots) _shapes.Add(HotspotScene.ShapeOf(h));
            SetStates(enabled, visited);
            _active = true;
            _prompt.text = scene != null ? scene.prompt : "";
            _prompt.style.display = string.IsNullOrEmpty(_prompt.text) ? DisplayStyle.None : DisplayStyle.Flex;
            Root.style.display = DisplayStyle.Flex;
            if (_selected >= 0 && (_selected >= _enabled.Count || !_enabled[_selected])) _selected = -1;
            SetHover(-1);
            _canvas.MarkDirtyRepaint();
        }

        public void SetStates(IList<bool> enabled, IList<bool> visited)
        {
            _enabled.Clear();
            _visited.Clear();
            for (int i = 0; i < _shapes.Count; i++)
            {
                _enabled.Add(enabled == null || i >= enabled.Count || enabled[i]);
                _visited.Add(visited != null && i < visited.Count && visited[i]);
            }
            _canvas.MarkDirtyRepaint();
        }

        public void Hide()
        {
            _active = false;
            Root.style.display = DisplayStyle.None;
            _hover.style.display = DisplayStyle.None;
            _hoverIndex = -1;
        }

        public bool IsEnabled(int index) => index >= 0 && index < _enabled.Count && _enabled[index];

        /// <summary>"Use the brass key on..." while an item is held ("" = nothing held).</summary>
        public void SetHeldItem(string displayName)
        {
            _heldItemName = displayName ?? "";
            Refresh();
        }

        // ---------- Pointer ----------

        Vector2 ToNormalized(Vector2 local)
        {
            var r = _canvas.contentRect;
            float w = r.width > 1f ? r.width : 1f, h = r.height > 1f ? r.height : 1f;
            return new Vector2(local.x / w, local.y / h);
        }

        void OnMove(PointerMoveEvent e)
        {
            if (!_active) return;
            _pointerInside = true;
            var n = ToNormalized(e.localPosition);
            int hit = HotspotPicker.Pick(_shapes, _enabled, n.x, n.y);
            if (hit >= 0) _selected = hit;
            SetHover(hit, e.localPosition);
        }

        void OnDown(PointerDownEvent e)
        {
            if (!_active) return;
            if (e.button == 1) { RightClicked?.Invoke(); return; }
            if (e.button != 0) return;
            var n = ToNormalized(e.localPosition);
            int hit = HotspotPicker.Pick(_shapes, _enabled, n.x, n.y);
            if (hit >= 0) Activated?.Invoke(hit);
        }

        void SetHover(int index, Vector2? at = null)
        {
            _hoverIndex = index;
            if (index >= 0 && at.HasValue) PlaceLabelAt(at.Value + new Vector2(0f, -46f));
            else if (index >= 0) PlaceLabelAtShape(index);
            Refresh();
            _canvas.MarkDirtyRepaint();
        }

        void Refresh()
        {
            int i = _hoverIndex >= 0 ? _hoverIndex : (!_pointerInside ? _selected : -1);
            if (!_active || _scene == null || i < 0 || i >= _scene.hotspots.Count)
            {
                _hover.style.display = DisplayStyle.None;
                return;
            }
            string label = _scene.hotspots[i].label;
            _hover.text = _heldItemName.Length > 0 ? "Use " + _heldItemName + " on " + label : label;
            _hover.style.display = DisplayStyle.Flex;
        }

        void PlaceLabelAt(Vector2 local)
        {
            _hover.style.left = local.x;
            _hover.style.top = Mathf.Max(4f, local.y);
        }

        void PlaceLabelAtShape(int index)
        {
            var r = _canvas.contentRect;
            HotspotGeometry.Bounds(_shapes[index], out float minX, out float minY, out float maxX, out _);
            PlaceLabelAt(new Vector2((minX + maxX) * 0.5f * r.width, minY * r.height - 46f));
        }

        // ---------- Keyboard and gamepad ----------

        public void MoveSelection(float dx, float dy)
        {
            if (!_active) return;
            _pointerInside = false;
            _selected = HotspotPicker.Step(_shapes, _enabled, _selected, dx, dy);
            SetHover(-1);
            if (_selected >= 0) PlaceLabelAtShape(_selected);
            Refresh();
        }

        public void Cycle(int step)
        {
            if (!_active) return;
            _pointerInside = false;
            _selected = HotspotPicker.Cycle(_shapes, _enabled, _selected, step);
            SetHover(-1);
            if (_selected >= 0) PlaceLabelAtShape(_selected);
            Refresh();
        }

        /// <summary>Selects hotspot <paramref name="index"/> as the keyboard would (its label shows):
        /// attract mode "points" at a thing before using it.</summary>
        public void Select(int index)
        {
            if (!_active || !IsEnabled(index)) return;
            _pointerInside = false;
            _selected = index;
            SetHover(-1);
            PlaceLabelAtShape(index);
            Refresh();
        }

        /// <summary>Enter / (A): use the selected hotspot (selects the first one if none is).</summary>
        public void ActivateSelected()
        {
            if (!_active) return;
            if (!IsEnabled(_selected)) { Cycle(1); return; }
            Activated?.Invoke(_selected);
        }

        public void Tick(float dt)
        {
            if (!_active) return;
            _pulse += dt;
            _canvas.MarkDirtyRepaint();
        }

        // ---------- Drawing ----------

        void Draw(MeshGenerationContext mgc)
        {
            if (!_active || _shapes.Count == 0) return;
            var r = _canvas.contentRect;
            if (r.width < 2f || r.height < 2f) return;
            var p = mgc.painter2D;
            int focus = _hoverIndex >= 0 ? _hoverIndex : _selected;
            for (int i = 0; i < _shapes.Count; i++)
            {
                if (!_enabled[i]) continue;
                bool hot = i == focus;
                if (hot || _showAll)
                {
                    Path(p, _shapes[i], r);
                    p.fillColor = new Color(1f, 1f, 1f, hot ? 0.12f : 0.05f);
                    p.Fill(FillRule.NonZero);
                    Path(p, _shapes[i], r);
                    p.strokeColor = hot ? _accent : new Color(1f, 1f, 1f, 0.5f);
                    p.lineWidth = hot ? 4f : 2f;
                    p.Stroke();
                }
                if (_scene != null && _scene.showMarkers && !hot)
                {
                    HotspotGeometry.Centroid(_shapes[i], out float cx, out float cy);
                    var c = new Vector2(cx * r.width, cy * r.height);
                    float pulse = _visited[i] ? 0f : 0.5f + 0.5f * Mathf.Sin(_pulse * 3f + i);
                    float radius = 7f + 3f * pulse;
                    p.BeginPath();
                    p.Arc(c, radius + 3f, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    p.fillColor = new Color(0f, 0f, 0f, _visited[i] ? 0.25f : 0.45f);
                    p.Fill(FillRule.NonZero);
                    p.BeginPath();
                    p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    p.fillColor = _visited[i] ? new Color(1f, 1f, 1f, 0.35f) : new Color(_accent.r, _accent.g, _accent.b, 0.9f);
                    p.Fill(FillRule.NonZero);
                }
            }
        }

        static void Path(Painter2D p, HotspotShape s, Rect r)
        {
            p.BeginPath();
            int n = s.PointCount;
            for (int k = 0; k < n; k++)
            {
                s.Point(k, out float x, out float y);
                var v = new Vector2(x * r.width, y * r.height);
                if (k == 0) p.MoveTo(v);
                else p.LineTo(v);
            }
            p.ClosePath();
        }
    }
}
