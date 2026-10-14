using System;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The player's bindings on the Unity side: the table itself (<see cref="BindingTable"/>, pure, with the defaults and the
    /// conflict rule) kept in PlayerPrefs as one string, loaded on first use, and read generically from the Input System by
    /// control path. A binding is held when its control reads past 0.5 (a key or button is 1, a trigger or any axis crosses
    /// half), pressed on the frame it first does. The mouse's bindings count only when the caller says the mouse may be read
    /// (the cursor is locked): with the cursor free a click must not fire. In the scripted runs (the capture, the demo, the
    /// perf probe) the saved bindings are ignored and the defaults used, so every pass sees the same controls.
    /// </summary>
    public static class SkylineBindings
    {
        public const string PrefsKey = "skyline.bindings";
        public const float PressPoint = 0.5f;

        static BindingTable _table;
        static readonly Dictionary<string, InputControl> _cache = new Dictionary<string, InputControl>();
        static readonly Dictionary<string, bool> _axisLast = new Dictionary<string, bool>();

        /// <summary>Raised when the table changed (a rebind, a reset).</summary>
        public static event Action Changed;

        public static BindingTable Table
        {
            get
            {
                if (_table == null) Load();
                return _table;
            }
        }

        /// <summary>Reads the saved table (or the defaults in a scripted run, or when nothing is saved).</summary>
        public static void Load()
        {
            _table = new BindingTable();
            if (SkylineRunArgs.Demo) return;
            try
            {
                string text = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(text)) _table = BindingTable.Deserialize(text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("SkylineBindings: could not read the saved bindings (" + e.Message + "); defaults");
            }
        }

        public static void Save()
        {
            if (SkylineRunArgs.Demo) return;
            try
            {
                PlayerPrefs.SetString(PrefsKey, Table.Serialize());
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("SkylineBindings: could not save the bindings (" + e.Message + ")");
            }
        }

        public static BindResult Rebind(BindDevice device, BindAction action, string path)
        {
            var r = Table.Set(device, action, path);
            if (r != BindResult.Rejected)
            {
                Save();
                Changed?.Invoke();
            }
            return r;
        }

        public static void ResetToDefaults()
        {
            Table.ResetToDefaults();
            Save();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- reading

        /// <summary>The control a path names on the current keyboard, mouse or pad (null when that device is absent or has no
        /// such control).</summary>
        public static InputControl Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int close = path.IndexOf('>');
            if (path.Length < 4 || path[0] != '<' || close < 0 || close + 2 > path.Length) return null;
            string layout = path.Substring(1, close - 1), rest = path.Substring(close + 2);
            InputDevice dev = null;
            if (layout == "Keyboard") dev = Keyboard.current;
            else if (layout == "Mouse") dev = Mouse.current;
            else if (layout == "Gamepad") dev = Gamepad.current;
            if (dev == null || !dev.added) return null;
            if (_cache.TryGetValue(path, out var c) && c != null && c.device == dev) return c;
            c = dev.TryGetChildControl(rest);
            _cache[path] = c;
            return c;
        }

        static bool IsMouse(string path) => path.StartsWith("<Mouse>", StringComparison.Ordinal);

        /// <summary>True while any control of the binding reads past half.</summary>
        public static bool Held(string binding, bool mouseOk)
        {
            foreach (var p in BindingTable.Split(binding))
            {
                if (IsMouse(p) && !mouseOk) continue;
                var f = Resolve(p) as InputControl<float>;
                if (f != null && f.ReadValue() > PressPoint) return true;
            }
            return false;
        }

        /// <summary>True on the frame any control of the binding first reads past half.</summary>
        public static bool Pressed(string binding, bool mouseOk)
        {
            foreach (var p in BindingTable.Split(binding))
            {
                if (IsMouse(p) && !mouseOk) continue;
                var c = Resolve(p);
                if (c == null) continue;
                var b = c as ButtonControl;
                if (b != null)
                {
                    if (b.wasPressedThisFrame) return true;
                    continue;
                }
                var f = c as InputControl<float>;
                if (f == null) continue;
                bool now = f.ReadValue() > PressPoint;
                _axisLast.TryGetValue(p, out bool was);
                _axisLast[p] = now;
                if (now && !was) return true;
            }
            return false;
        }

        public static bool Held(BindDevice device, BindAction action, bool mouseOk) => Held(Table.Get(device, action), mouseOk);

        public static bool Pressed(BindDevice device, BindAction action, bool mouseOk) => Pressed(Table.Get(device, action), mouseOk);

        // ---------------------------------------------------------------- capturing (the Controls page)

        /// <summary>The control path of a key, mouse button or pad button pressed this frame on <paramref name="device"/>, for
        /// "press a button...". Escape (keyboard) and Start (pad) are the caller's cancel keys and are never returned;
        /// sticks' directions and axes are not buttons here.</summary>
        public static bool TryCapture(BindDevice device, out string path)
        {
            path = null;
            if (device == BindDevice.KeyboardMouse)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    foreach (var key in kb.allKeys)
                    {
                        if (key == null || !key.wasPressedThisFrame || key == kb.escapeKey) continue;
                        path = "<Keyboard>/" + key.name;
                        return true;
                    }
                }
                var m = Mouse.current;
                if (m != null)
                {
                    if (m.leftButton.wasPressedThisFrame) { path = "<Mouse>/leftButton"; return true; }
                    if (m.rightButton.wasPressedThisFrame) { path = "<Mouse>/rightButton"; return true; }
                    if (m.middleButton.wasPressedThisFrame) { path = "<Mouse>/middleButton"; return true; }
                    if (m.forwardButton.wasPressedThisFrame) { path = "<Mouse>/forwardButton"; return true; }
                    if (m.backButton.wasPressedThisFrame) { path = "<Mouse>/backButton"; return true; }
                }
                return false;
            }
            var pad = Gamepad.current;
            if (pad == null) return false;
            foreach (var c in pad.allControls)
            {
                var b = c as ButtonControl;
                if (b == null || !b.wasPressedThisFrame || b == pad.startButton) continue;
                string rel = c.path.Substring(pad.path.Length + 1);
                if (rel.StartsWith("leftStick/", StringComparison.Ordinal) || rel.StartsWith("rightStick/", StringComparison.Ordinal)) continue;
                path = "<Gamepad>/" + rel;
                return true;
            }
            return false;
        }
    }
}
