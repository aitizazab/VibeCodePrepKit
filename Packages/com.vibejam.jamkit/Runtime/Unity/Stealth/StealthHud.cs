using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Stealth
{
    /// <summary>
    /// The stealth kit's on-screen detection indicator (docs/STEALTH.md "Readable feedback"), built in code on a UI
    /// Toolkit layer (the shell's HUD root): a bar at the top centre that fills with the most alert seeker's detection
    /// meter, coloured by its state, with a word over it ("Unseen", "Hidden: closet", "Suspicious ?", "Searching",
    /// "Found !", "Counting 7"; being found comes first, then being hidden, then the seeker's state), and the
    /// collectibles line ("2 / 5 found"). Words refresh ten times a second.
    /// </summary>
    public sealed class StealthHud
    {
        readonly VisualElement _root, _bar, _fill, _dim;
        readonly Label _word, _items, _lucid;
        float _dimShown = -1f;
        float _textT;
        string _lastWord = "";

        public string Word => _word.text;
        public string ItemsText => _items.text;
        float _fillShown;
        public float FillShown => _fillShown;

        public StealthHud(VisualElement parent)
        {
            _root = new VisualElement { name = "stealth-hud", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;
            parent.Add(_root);

            // Being watched dims her lucidity (answer C2): the screen darkens at its edges as the powers fade.
            _dim = new VisualElement { name = "stealth-dim", pickingMode = PickingMode.Ignore };
            _dim.style.position = Position.Absolute;
            _dim.style.left = 0; _dim.style.right = 0; _dim.style.top = 0; _dim.style.bottom = 0;
            _dim.style.borderLeftWidth = 90; _dim.style.borderRightWidth = 90; _dim.style.borderTopWidth = 60; _dim.style.borderBottomWidth = 60;
            _dim.style.display = DisplayStyle.None;
            _root.Add(_dim);

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.position = Position.Absolute;
            top.style.top = 22; top.style.left = Length.Percent(50);
            top.style.width = 280; top.style.marginLeft = -140;
            top.style.alignItems = Align.Center;
            _root.Add(top);

            _word = new Label("") { pickingMode = PickingMode.Ignore };
            _word.style.fontSize = 20;
            _word.style.unityFontStyleAndWeight = FontStyle.Bold;
            _word.style.color = Color.white;
            _word.style.unityTextAlign = TextAnchor.MiddleCenter;
            top.Add(_word);

            _bar = new VisualElement { pickingMode = PickingMode.Ignore };
            _bar.style.width = 260; _bar.style.height = 12; _bar.style.marginTop = 4;
            _bar.style.backgroundColor = new Color(0.05f, 0.06f, 0.1f, 0.6f);
            Round(_bar, 6f);
            top.Add(_bar);
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.style.height = Length.Percent(100);
            _fill.style.width = Length.Percent(0);
            Round(_fill, 6f);
            _bar.Add(_fill);

            _items = new Label("") { pickingMode = PickingMode.Ignore };
            _items.style.position = Position.Absolute;
            _items.style.top = 24; _items.style.right = 32;
            _items.style.fontSize = 18;
            _items.style.color = new Color(1f, 0.92f, 0.62f);
            _items.style.unityFontStyleAndWeight = FontStyle.Bold;
            _root.Add(_items);

            _lucid = new Label("") { pickingMode = PickingMode.Ignore };
            _lucid.style.position = Position.Absolute;
            _lucid.style.top = 78; _lucid.style.left = Length.Percent(50);
            _lucid.style.width = 360; _lucid.style.marginLeft = -180;
            _lucid.style.unityTextAlign = TextAnchor.MiddleCenter;
            _lucid.style.fontSize = 15;
            _lucid.style.color = new Color(0.82f, 0.78f, 1f);
            _root.Add(_lucid);
        }

        public string LucidText => _lucid.text;

        static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        public void Remove() => _root.RemoveFromHierarchy();
        public void Show(bool on) => _root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Call every frame.</summary>
        public void Tick()
        {
            // The most alert seeker.
            SeekerAgent top = null;
            float best = -1f;
            var all = SeekerAgent.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.Brain == null) continue;
                float score = (int)s.State * 2f + s.Meter + (s.Counting ? 0.5f : 0f);
                if (score > best) { best = score; top = s; }
            }
            float fill = 0f;
            var color = SeekerOverlay.Unaware;
            if (top != null)
            {
                fill = top.Counting ? 0f : top.Meter;
                color = SeekerOverlay.ColorOf(top.State);
            }
            if (Mathf.Abs(fill - _fillShown) > 0.005f)
            {
                _fillShown = fill;
                _fill.style.width = Length.Percent(fill * 100f);
            }
            _fill.style.backgroundColor = color;

            // The lucidity fade (answer C2) and the daytime section (C1).
            var pr = StealthPlayerRules.Current;
            float dim = pr != null ? 1f - pr.Lucidity : 0f;
            if (Mathf.Abs(dim - _dimShown) > 0.01f)
            {
                _dimShown = dim;
                _dim.style.display = dim > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
                var c = new Color(0.12f, 0.08f, 0.22f, 0.55f * dim);
                _dim.style.borderLeftColor = c; _dim.style.borderRightColor = c; _dim.style.borderTopColor = c; _dim.style.borderBottomColor = c;
            }

            _textT -= Time.unscaledDeltaTime;
            if (_textT > 0f) return;
            _textT = 0.1f;
            string lucid = pr == null ? "" : pr.Daytime ? "Daytime: no dream powers (just a nurse on a ward)"
                : StealthRules.Shared.lucidityFade ? (pr.Faded ? "Watched: the dream's powers fade" : pr.Lucidity < 0.99f ? "Lucid again..." : "Lucid") : "";
            if (_lucid.text != lucid) _lucid.text = lucid;
            string word = WordFor(top);
            if (word != _lastWord) { _lastWord = word; _word.text = word; }
            _word.style.color = top != null && top.State != AlertState.Unaware ? color : Color.white;
            var counter = CollectibleCounterIfAny();
            string items = counter != null && counter.Total > 0 ? counter.Summary : "";
            if (_items.text != items) _items.text = items;
        }

        static CollectibleCounter CollectibleCounterIfAny() => CollectibleCounter.Existing;

        static string WordFor(SeekerAgent top)
        {
            var player = StealthPlayer.Current;
            // Found first; then being hidden (what you most need to know while it searches); then its state.
            if (top != null && top.State == AlertState.Found) return "Found !";
            if (player != null && player.Hidden) return "Hidden: " + HidingSpotView.Spots[player.HidingSpot].kind.ToString().ToLowerInvariant();
            if (top != null && top.Counting) return "Counting " + Mathf.CeilToInt(top.Brain.CountLeft).ToString(CultureInfo.InvariantCulture);
            if (top != null && top.State == AlertState.Searching) return "Searching";
            if (top != null && top.State == AlertState.Suspicious) return "Suspicious ?";
            return "Unseen";
        }
    }
}
