using System.Collections.Generic;
using Ink.Runtime;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamKit.Narrative
{
    public sealed partial class StoryGame
    {
        HotspotScene _investigation;
        readonly HashSet<string> _usedHotspots = new HashSet<string>();
        readonly List<bool> _enabledBuffer = new List<bool>();
        readonly List<bool> _visitedBuffer = new List<bool>();
        readonly List<string> _itemBuffer = new List<string>();
        readonly List<string> _lastItems = new List<string>();
        readonly List<KeyValuePair<InkListItem, int>> _pairBuffer = new List<KeyValuePair<InkListItem, int>>();

        /// <summary>The open hotspot scene (#investigate), or null.</summary>
        public HotspotScene Investigation => _investigation;

        void BeginInvestigation(string id)
        {
            var scene = config.FindHotspotScene(id);
            if (scene == null)
            {
                Debug.LogError("StoryGame: no hotspot scene \"" + id + "\" in the StoryConfig (#investigate:" + id + ").");
                return;
            }
            _investigation = scene;
            SmokeNoteRoom(id);
            if (!string.IsNullOrEmpty(scene.background) && _state.background != scene.background)
            {
                var cmd = StoryCommand.Parse("bg:" + scene.background);
                _state.Apply(cmd);
                _stage.Play(cmd, _state, false);
            }
        }

        void EndInvestigation()
        {
            _investigation = null;
            _hotspots.Hide();
            _inventory.SetVisible(false);
            _inventory.SetHeld("");
        }

        void OnParked()
        {
            if (_investigation == null) return;
            ShowHotspots();
            Capture();
            Autosave();
        }

        /// <summary>Opens (or refreshes) the hotspot scene: which hotspots show, which were looked at, the items.</summary>
        void ShowHotspots()
        {
            if (_investigation == null) return;
            RefreshHotspotStates();
            _hotspots.Show(_investigation, _enabledBuffer, _visitedBuffer);
            RefreshInventory(false);
            _inventory.SetVisible(true);
        }

        void RefreshHotspotStates()
        {
            _enabledBuffer.Clear();
            _visitedBuffer.Clear();
            var story = GameRoot.Dialogue.Story;
            foreach (var h in _investigation.hotspots)
            {
                bool used = h != null && _usedHotspots.Contains(h.id);
                bool on = h != null && !(h.hideAfterUse && used) && StoryCondition.Evaluate(h.showIf, this);
                _enabledBuffer.Add(on);
                bool seen = used;
                if (!seen && h != null && story != null && !string.IsNullOrEmpty(h.knot))
                {
                    try { seen = story.state.VisitCountAtPathString(h.knot) > 0; }
                    catch (System.Exception) { seen = false; }
                }
                _visitedBuffer.Add(seen);
            }
        }

        void OnHotspotActivated(int index)
        {
            if (_investigation == null || index < 0 || index >= _investigation.hotspots.Count) return;
            var d = GameRoot.Dialogue;
            if (!d.IsParked || !GameRoot.Input.GameplayAllowed) return;
            var h = _investigation.hotspots[index];
            string held = _inventory.Held;
            string knot = held.Length > 0 ? _investigation.KnotForItem(h, held) : h.knot;
            if (string.IsNullOrEmpty(knot)) return;
            _usedHotspots.Add(h.id);
            _inventory.SetHeld("");
            _hotspots.Hide();
            _inventory.SetVisible(false);
            GameRoot.Audio.Play("click");
            SetUsedItemVariable(held);
            d.Unpark(knot);
        }

        /// <summary>Uses a hotspot by id, as a click would (the walkthrough, tests). With <paramref name="item"/>, uses that item on it.</summary>
        public bool UseHotspot(string hotspotId, string item = null)
        {
            if (_investigation == null || !GameRoot.Dialogue.IsParked) return false;
            int index = _investigation.IndexOf(hotspotId);
            if (index < 0) return false;
            RefreshHotspotStates();
            if (!_enabledBuffer[index]) return false;
            _inventory.SetHeld(string.IsNullOrEmpty(item) ? "" : item);
            SmokeNoteHotspot(hotspotId, item);
            OnHotspotActivated(index);
            return true;
        }

        /// <summary>ink can read which item was used last: VAR used_item = "" (optional).</summary>
        void SetUsedItemVariable(string item)
        {
            var story = GameRoot.Dialogue.Story;
            if (story == null || !story.variablesState.GlobalVariableExistsWithName("used_item")) return;
            story.variablesState["used_item"] = item ?? "";
        }

        void HandleHotspotInput(Keyboard kb, Gamepad pad)
        {
            var input = GameRoot.Input;
            if (!input.GameplayAllowed) return;
            if (input.Navigate.WasPressedThisFrame())
            {
                Vector2 v = input.Navigate.ReadValue<Vector2>();
                if (v.sqrMagnitude > 0.2f) _hotspots.MoveSelection(v.x, -v.y);
            }
            if ((kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame))
                _hotspots.Cycle(kb != null && kb.shiftKey.isPressed ? -1 : 1);
            if (pad != null && pad.leftShoulder.wasPressedThisFrame) _hotspots.Cycle(-1);
            if (input.Submit.WasPressedThisFrame() || input.Interact.WasPressedThisFrame()) _hotspots.ActivateSelected();
            if (kb != null)
                for (int i = 0; i < 9; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame) _inventory.Toggle(i);
            if (pad != null && pad.buttonNorth.wasPressedThisFrame) CycleHeldItem();
            if (input.Cancel.WasPressedThisFrame() || (kb != null && kb.backspaceKey.wasPressedThisFrame)) _inventory.SetHeld("");
        }

        void CycleHeldItem()
        {
            var items = _inventory.Items;
            if (items.Count == 0) return;
            int at = -1;
            for (int i = 0; i < items.Count; i++) if (items[i] == _inventory.Held) at = i;
            if (at + 1 >= items.Count) _inventory.SetHeld("");
            else _inventory.Toggle(at + 1);
        }

        // ---------- Inventory (ink's list) ----------

        /// <summary>The item ids the player carries now, in the list's order.</summary>
        public List<string> InventoryItems()
        {
            _itemBuffer.Clear();
            var story = GameRoot.Dialogue.Story;
            string var = string.IsNullOrEmpty(config.inventoryVariable) ? "inventory" : config.inventoryVariable;
            if (story == null || !story.variablesState.GlobalVariableExistsWithName(var)) return new List<string>(_itemBuffer);
            if (story.variablesState[var] is InkList list)
            {
                // An ink list is a set of (item, value): show it in the LIST's order.
                _pairBuffer.Clear();
                foreach (var kv in list) _pairBuffer.Add(kv);
                _pairBuffer.Sort((x, y) => x.Value != y.Value ? x.Value.CompareTo(y.Value) : string.CompareOrdinal(x.Key.itemName, y.Key.itemName));
                foreach (var kv in _pairBuffer) _itemBuffer.Add(kv.Key.itemName);
            }
            return new List<string>(_itemBuffer);
        }

        public bool HasItem(string id)
        {
            foreach (var i in InventoryItems()) if (string.Equals(i, id, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Refreshes the bar; with <paramref name="announce"/>, new items get a "Got: ..." message.</summary>
        void RefreshInventory(bool announce)
        {
            var now = InventoryItems();
            if (announce)
                foreach (var id in now)
                    if (!_lastItems.Contains(id))
                    {
                        var def = config.FindItem(id);
                        GameRoot.Hud.Message("Got: " + (def != null ? def.displayName : id), 2.5f);
                        GameRoot.Audio.Play("item");
                    }
            _lastItems.Clear();
            _lastItems.AddRange(now);
            _inventory.SetItems(now);
        }

        void ObserveInventory(Story story)
        {
            string var = string.IsNullOrEmpty(config.inventoryVariable) ? "inventory" : config.inventoryVariable;
            if (!story.variablesState.GlobalVariableExistsWithName(var)) return;
            story.ObserveVariable(var, (name, value) => RefreshInventory(true));
        }

        /// <summary>Debug: every item into the inventory.</summary>
        public void GiveAllItems()
        {
            var story = GameRoot.Dialogue.Story;
            string var = string.IsNullOrEmpty(config.inventoryVariable) ? "inventory" : config.inventoryVariable;
            if (story == null || !story.variablesState.GlobalVariableExistsWithName(var)) return;
            var current = story.variablesState[var] as InkList ?? new InkList();
            foreach (var item in config.items)
            {
                if (item == null) continue;
                try { current = current.Union(InkList.FromString(item.id, story)); }
                catch (System.Exception) { /* not in any ink LIST */ }
            }
            story.variablesState[var] = current;
            RefreshInventory(false);
            if (_hotspots.Active) ShowHotspots();
        }
    }
}
