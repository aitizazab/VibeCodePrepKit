using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    /// <summary>
    /// The items the player carries (ink's inventory list), as a row of icons with names. Click one
    /// (or press its number) to hold it, then click a hotspot to use it there; click it again or
    /// right click to put it away. Shown while a hotspot scene is open.
    /// </summary>
    public sealed class InventoryBar
    {
        public readonly VisualElement Root;
        readonly VisualElement _row;
        readonly Label _caption;
        readonly List<string> _items = new List<string>();
        readonly List<Button> _slots = new List<Button>();
        readonly StoryConfig _config;
        string _held = "";

        /// <summary>The held item changed (id, "" = none).</summary>
        public Action<string> HeldChanged;

        public string Held => _held;
        public IReadOnlyList<string> Items => _items;
        public bool PointerOver { get { return _pointerOver; } }
        bool _pointerOver;

        public InventoryBar(VisualElement parent, StoryConfig config)
        {
            _config = config;
            Root = new VisualElement { name = "inventory", pickingMode = PickingMode.Position };
            Root.AddToClassList("st-inventory");
            parent.Add(Root);
            _caption = new Label("Items");
            _caption.AddToClassList("st-inventory-caption");
            Root.Add(_caption);
            _row = new VisualElement();
            _row.AddToClassList("st-inventory-row");
            Root.Add(_row);
            Root.RegisterCallback<PointerEnterEvent>(_ => _pointerOver = true);
            Root.RegisterCallback<PointerLeaveEvent>(_ => _pointerOver = false);
            Root.style.display = DisplayStyle.None;
        }

        public void SetVisible(bool shown)
        {
            Root.style.display = shown && _items.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown) _pointerOver = false;
        }

        public bool Visible => Root.style.display == DisplayStyle.Flex;

        /// <summary>Shows these item ids (in ink's list order).</summary>
        public void SetItems(IList<string> ids)
        {
            _items.Clear();
            if (ids != null) _items.AddRange(ids);
            while (_slots.Count < _items.Count)
            {
                int index = _slots.Count;
                var b = new Button(() => Toggle(index)) { name = "item-" + index };
                b.AddToClassList("st-item");
                b.focusable = false;
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("st-item-icon");
                b.Add(icon);
                var name = new Label { pickingMode = PickingMode.Ignore };
                name.AddToClassList("st-item-name");
                b.Add(name);
                _row.Add(b);
                _slots.Add(b);
            }
            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < _items.Count;
                _slots[i].style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used) continue;
                var def = _config != null ? _config.FindItem(_items[i]) : null;
                var icon = _slots[i].ElementAt(0);
                if (def != null && def.icon != null) { icon.style.backgroundImage = new StyleBackground(def.icon); icon.style.backgroundColor = StyleKeyword.Null; }
                else { icon.style.backgroundImage = StyleKeyword.None; icon.style.backgroundColor = def != null ? def.color : Color.gray; }
                ((Label)_slots[i].ElementAt(1)).text = (i + 1) + "  " + (def != null ? def.displayName : _items[i]);
                _slots[i].tooltip = def != null ? def.description : "";
            }
            if (_held.Length > 0 && !_items.Contains(_held)) SetHeld("");
            else Mark();
            if (Root.style.display == DisplayStyle.Flex && _items.Count == 0) Root.style.display = DisplayStyle.None;
        }

        /// <summary>Number keys: hold item <paramref name="index"/>, or put it away if held.</summary>
        public void Toggle(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            SetHeld(_held == _items[index] ? "" : _items[index]);
            if (GameRoot.Exists) GameRoot.Audio.Play("click");
        }

        public void SetHeld(string id)
        {
            _held = id ?? "";
            Mark();
            var def = _config != null ? _config.FindItem(_held) : null;
            _caption.text = _held.Length == 0 ? "Items (click one to use it)" : "Holding: " + (def != null ? def.displayName : _held) + (def != null && def.description.Length > 0 ? "  -  " + def.description : "");
            HeldChanged?.Invoke(_held);
        }

        void Mark()
        {
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].EnableInClassList("st-item--held", i < _items.Count && _items[i] == _held && _held.Length > 0);
        }
    }
}
