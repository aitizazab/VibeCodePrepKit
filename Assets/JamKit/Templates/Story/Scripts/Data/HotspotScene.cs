using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.Narrative
{
    /// <summary>
    /// A point-and-click scene: a background with clickable regions. ink opens it with
    /// #investigate:id; clicking a hotspot runs its knot; when that knot ends (-> DONE) the player is
    /// back in the scene, until a knot says #leave. Items from the inventory can be used on hotspots
    /// (Item Uses), with a fallback knot for "that does nothing".
    /// Coordinates are fractions of the picture: x 0 (left) to 1 (right), y 0 (top) to 1 (bottom).
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Story/Hotspot Scene", fileName = "HotspotScene")]
    public sealed class HotspotScene : ScriptableObject
    {
        [Serializable]
        public class ItemUse
        {
            [Tooltip("Item id (from the story config's items).")]
            public string item = "key";
            [Tooltip("Knot to run when this item is used here.")]
            public string knot = "";
        }

        [Serializable]
        public class Hotspot
        {
            [Tooltip("Unique in this scene (the walkthrough and tests use it).")]
            public string id = "thing";
            [Tooltip("What players see when they point at it.")]
            public string label = "Thing";
            public HotspotShapeKind shape = HotspotShapeKind.Rect;
            [Tooltip("Rectangle: x, y (top-left), width, height, as fractions of the picture.")]
            public Rect rect = new Rect(0.4f, 0.4f, 0.2f, 0.2f);
            [Tooltip("Polygon corners in order, as fractions of the picture (y down).")]
            public List<Vector2> points = new List<Vector2>();
            [Tooltip("Knot to run when clicked.")]
            public string knot = "";
            [Tooltip("Only there when this is true (e.g. \"!item:ticket\", \"seen:office_coat\", \"drawer_open\"). Empty = always.")]
            public string showIf = "";
            [Tooltip("Gone after its knot ran once.")]
            public bool hideAfterUse;
            public List<ItemUse> itemUses = new List<ItemUse>();
        }

        [Tooltip("The id ink uses: #investigate:id")]
        public string id = "room";
        [Tooltip("Background shown while the scene is open (empty: keep the current one).")]
        public string background = "";
        [Tooltip("The hint line at the top while the player looks around.")]
        [TextArea(1, 3)] public string prompt = "Look around: click things, or use the arrow keys and Enter.";
        [Tooltip("Small markers on hotspots, so nothing has to be pixel-hunted (dimmer once looked at).")]
        public bool showMarkers = true;
        public List<Hotspot> hotspots = new List<Hotspot>();
        [Tooltip("Knot for using an item where it does nothing (empty: the hotspot's own knot).")]
        public string fallbackItemKnot = "";
        [Tooltip("The smoke test's way through: hotspot ids, or item>hotspot to use an item. Must reach #leave.")]
        public List<string> walkthrough = new List<string>();

        public static HotspotShape ShapeOf(Hotspot h)
        {
            if (h == null) return HotspotShape.Rect(0f, 0f, 0f, 0f);
            if (h.shape == HotspotShapeKind.Polygon)
            {
                var xy = new float[(h.points != null ? h.points.Count : 0) * 2];
                for (int i = 0; h.points != null && i < h.points.Count; i++)
                {
                    xy[i * 2] = h.points[i].x;
                    xy[i * 2 + 1] = h.points[i].y;
                }
                return HotspotShape.Polygon(xy);
            }
            return HotspotShape.Rect(h.rect.x, h.rect.y, h.rect.width, h.rect.height);
        }

        public Hotspot Find(string hotspotId)
        {
            if (hotspots == null || string.IsNullOrEmpty(hotspotId)) return null;
            foreach (var h in hotspots)
                if (h != null && string.Equals(h.id, hotspotId, StringComparison.OrdinalIgnoreCase)) return h;
            return null;
        }

        public int IndexOf(string hotspotId)
        {
            for (int i = 0; hotspots != null && i < hotspots.Count; i++)
                if (hotspots[i] != null && string.Equals(hotspots[i].id, hotspotId, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>The knot for using <paramref name="item"/> on <paramref name="h"/>: its own reaction, else the fallback, else the hotspot's knot.</summary>
        public string KnotForItem(Hotspot h, string item)
        {
            if (h != null && h.itemUses != null)
                foreach (var u in h.itemUses)
                    if (u != null && string.Equals(u.item, item, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(u.knot)) return u.knot;
            if (!string.IsNullOrEmpty(fallbackItemKnot)) return fallbackItemKnot;
            return h != null ? h.knot : "";
        }

        /// <summary>Flattened for the core validator.</summary>
        public HotspotSceneInfo ToInfo()
        {
            var info = new HotspotSceneInfo { id = id, background = background, fallbackItemKnot = fallbackItemKnot };
            if (hotspots != null)
                foreach (var h in hotspots)
                {
                    if (h == null) continue;
                    var hi = new HotspotInfo { id = h.id, label = h.label, shape = ShapeOf(h), knot = h.knot, showIf = h.showIf };
                    if (h.itemUses != null)
                        foreach (var u in h.itemUses)
                            if (u != null) hi.itemKnots.Add(new KeyValuePair<string, string>(u.item, u.knot));
                    info.hotspots.Add(hi);
                }
            if (walkthrough != null)
                foreach (var w in walkthrough)
                    if (!string.IsNullOrEmpty(w)) info.walkthrough.Add(w);
            return info;
        }
    }
}
