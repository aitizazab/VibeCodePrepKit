using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Argument
{
    /// <summary>One thing that can be focused and chosen: a card, a verb, a menu line.</summary>
    public sealed class NavItem
    {
        public string id = "";
        public VisualElement element;
        public Action activate;
        public bool enabled = true;
        /// <summary>Chosen by holding A / Enter (or the mouse button) for <see cref="ArgumentHost.PressHold"/> (Press).</summary>
        public bool hold;
    }

    /// <summary>
    /// The prototype's one focus model, everywhere (the battle's cards, verbs, evidence and reasons, the end card, its
    /// menu): lines of items; up and down move between lines (to the nearest item), left and right along a line; no
    /// wrap-around; disabled items are skipped; A / Enter chooses, B / Backspace goes back (the screen decides where).
    /// The focused item has a gold border and lifts slightly; nothing uses UI Toolkit focus, so focus is never lost
    /// to a click on empty space or a screen change. The mouse moves the focus by hovering and chooses by clicking.
    /// </summary>
    public sealed class ArgueNav
    {
        readonly List<List<NavItem>> _lines = new List<List<NavItem>>();
        int _line, _col;

        public int LineCount => _lines.Count;
        public NavItem Focused => _line < _lines.Count && _col < _lines[_line].Count ? _lines[_line][_col] : null;
        public string FocusedId => Focused != null ? Focused.id : "";

        public void Clear()
        {
            foreach (var l in _lines) foreach (var i in l) ArgueUi.Focus(i.element, false);
            _lines.Clear();
            _line = _col = 0;
        }

        public void AddLine(IList<NavItem> items)
        {
            var line = new List<NavItem>();
            foreach (var i in items) if (i != null) line.Add(i);
            if (line.Count > 0) _lines.Add(line);
        }

        public void AddLine(params NavItem[] items) => AddLine((IList<NavItem>)items);

        public NavItem Find(VisualElement e)
        {
            foreach (var l in _lines) foreach (var i in l) if (i.element == e) return i;
            return null;
        }

        public NavItem Find(string id)
        {
            foreach (var l in _lines) foreach (var i in l) if (i.id == id) return i;
            return null;
        }

        /// <summary>Focuses the item with this id (if it's there and enabled).</summary>
        public bool Focus(string id)
        {
            for (int a = 0; a < _lines.Count; a++)
                for (int b = 0; b < _lines[a].Count; b++)
                    if (_lines[a][b].id == id && _lines[a][b].enabled) { _line = a; _col = b; Highlight(); return true; }
            return false;
        }

        public bool Focus(NavItem item) => item != null && Focus(item.id);

        /// <summary>The first enabled item (top line first).</summary>
        public void FocusFirst()
        {
            for (int a = 0; a < _lines.Count; a++)
                for (int b = 0; b < _lines[a].Count; b++)
                    if (_lines[a][b].enabled) { _line = a; _col = b; Highlight(); return; }
            Highlight();
        }

        /// <summary>Moves the focus (dx along a line, dy between lines; no wrap). True if it moved.</summary>
        public bool Move(int dx, int dy)
        {
            if (_lines.Count == 0) return false;
            if (dy != 0)
            {
                // To the nearest enabled item on the next line that has one, by screen position.
                float x = Center(Focused);
                for (int l = _line + dy; l >= 0 && l < _lines.Count; l += dy)
                {
                    int best = -1;
                    float bestDist = float.MaxValue;
                    for (int c = 0; c < _lines[l].Count; c++)
                    {
                        if (!_lines[l][c].enabled) continue;
                        float d = Mathf.Abs(Center(_lines[l][c]) - x) + c * 0.01f;
                        if (d < bestDist) { bestDist = d; best = c; }
                    }
                    if (best < 0) continue;
                    _line = l;
                    _col = best;
                    Highlight();
                    return true;
                }
                return false;
            }
            if (dx != 0)
            {
                var line = _lines[_line];
                for (int c = _col + dx; c >= 0 && c < line.Count; c += dx)
                {
                    if (!line[c].enabled) continue;
                    _col = c;
                    Highlight();
                    return true;
                }
            }
            return false;
        }

        static float Center(NavItem i)
        {
            if (i == null || i.element == null) return 0f;
            // Before its first layout pass (or off a panel) an element's width is NaN while its x is 0: guard the
            // centre itself, or every distance is NaN and moving up / down finds nothing.
            float c = i.element.worldBound.center.x;
            return float.IsNaN(c) || float.IsInfinity(c) ? 0f : c;
        }

        /// <summary>Draws the focus: the focused item lit, the rest plain (disabled ones dimmed).</summary>
        public void Highlight()
        {
            var f = Focused;
            foreach (var l in _lines)
                foreach (var i in l)
                {
                    ArgueUi.Focus(i.element, i == f);
                    if (i.element != null) i.element.style.opacity = i.enabled ? 1f : 0.38f;
                }
        }
    }

    /// <summary>Reads Navigate as one step at a time, with a held stick or key repeating (0.35 s, then every 0.12 s).</summary>
    public sealed class NavInput
    {
        Vector2Int _last;
        float _timer;

        /// <summary>This frame's step: x right +1 / left -1, y DOWN +1 / up -1 (screen order). Zero most frames.</summary>
        public Vector2Int Step(InputService input, float dt)
        {
            Vector2 v = input.Navigate.ReadValue<Vector2>();
            var d = Vector2Int.zero;
            if (Mathf.Abs(v.x) >= 0.5f || Mathf.Abs(v.y) >= 0.5f)
                d = Mathf.Abs(v.x) >= Mathf.Abs(v.y) ? new Vector2Int(Math.Sign(v.x), 0) : new Vector2Int(0, -Math.Sign(v.y));
            if (d == Vector2Int.zero) { _last = d; return d; }
            if (d != _last)
            {
                _last = d;
                _timer = 0.35f;
                return d;
            }
            _timer -= dt;
            if (_timer > 0f) return Vector2Int.zero;
            _timer = 0.12f;
            return d;
        }

        public void Reset()
        {
            _last = Vector2Int.zero;
            _timer = 0f;
        }
    }

    /// <summary>The prototype's look (shared by the battle screen and its menu): colours, fonts, panels, cards, focus.</summary>
    public static class ArgueUi
    {
        public static readonly Color Black = new Color(0.025f, 0.02f, 0.035f, 1f);
        public static readonly Color Ink = new Color(0.95f, 0.93f, 0.89f);
        public static readonly Color Dim = new Color(0.66f, 0.63f, 0.72f);
        public static readonly Color Gold = new Color(1f, 0.79f, 0.36f);
        public static readonly Color Paper = new Color(0.96f, 0.92f, 0.84f);
        public static readonly Color PaperInk = new Color(0.17f, 0.13f, 0.12f);
        public static readonly Color Card = new Color(0.12f, 0.105f, 0.16f, 0.98f);
        public static readonly Color CardEdge = new Color(0.32f, 0.29f, 0.4f);
        public static readonly Color Panel = new Color(0.075f, 0.065f, 0.1f, 0.97f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.42f);
        public static readonly Color Good = new Color(0.55f, 0.88f, 0.6f);
        public static readonly Color Blue = new Color(0.62f, 0.74f, 1f);

        const string FocusedMark = "argue-focused";

        /// <summary>Lights or unlights an item: a gold border and a slight lift when focused.</summary>
        public static void Focus(VisualElement e, bool focused)
        {
            if (e == null) return;
            bool was = e.ClassListContains(FocusedMark);
            if (was == focused) return;
            e.EnableInClassList(FocusedMark, focused);
            var edge = e.userData is Color c ? c : CardEdge;
            Border(e, focused ? Gold : edge, focused ? 4f : 2f);
            e.style.scale = new Scale(focused ? new Vector3(1.025f, 1.025f, 1f) : Vector3.one);
        }

        public static bool IsFocused(VisualElement e) => e != null && e.ClassListContains(FocusedMark);

        /// <summary>A card: a dark rounded panel with a thin edge (gold when focused).</summary>
        public static VisualElement CardBox(VisualElement parent, string name, Color fill, Color edge)
        {
            var e = new VisualElement { name = name };
            e.userData = edge;
            e.style.backgroundColor = fill;
            Round(e, 12f);
            Border(e, edge, 2f);
            e.style.paddingLeft = e.style.paddingRight = 18;
            e.style.paddingTop = e.style.paddingBottom = 10;
            e.style.transitionProperty = new List<StylePropertyName> { "scale" };
            e.style.transitionDuration = new List<TimeValue> { new TimeValue(0.08f) };
            parent?.Add(e);
            return e;
        }

        public static Label Text(VisualElement parent, string text, Font font, float size, Color color, string name = null)
        {
            var l = new Label(text ?? "") { pickingMode = PickingMode.Ignore };
            if (name != null) l.name = name;
            ArgumentArt.Use(l, font);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingTop = l.style.paddingBottom = 0;
            parent?.Add(l);
            return l;
        }

        public static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        public static void Border(VisualElement e, Color c, float w)
        {
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
        }

        public static void Place(VisualElement e, float x, float y, float w, float h)
        {
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            e.style.width = w;
            if (h > 0f) e.style.height = h;
        }

        public static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = e.style.top = e.style.right = e.style.bottom = 0;
        }

        /// <summary>A pill-shaped tag (FACT, LINK, KEY, a status).</summary>
        public static Label Chip(VisualElement parent, string text, Color fill, Color ink)
        {
            var l = Text(parent, text, ArgumentArt.BodyBold, 15, ink);
            l.style.backgroundColor = fill;
            Round(l, 6f);
            l.style.paddingLeft = l.style.paddingRight = 8;
            l.style.paddingTop = l.style.paddingBottom = 1;
            l.style.marginRight = 6;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            return l;
        }
    }
}
