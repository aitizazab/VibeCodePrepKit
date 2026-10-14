using System;
using System.Collections.Generic;
using System.Text;

namespace JamKit.Skyline
{
    /// <summary>The buttons a player can rebind (the pause menu's Controls page). Moves and look stay on WASD / the left stick
    /// and the mouse / the right stick in this pass.</summary>
    public enum BindAction { Jump, Crouch, Sprint, Fire, Aim, Reload, Swap, Inspect, Pause }

    /// <summary>Which half of the table a binding lives in: the keyboard and mouse, or a pad.</summary>
    public enum BindDevice { KeyboardMouse, Pad }

    /// <summary>What <see cref="BindingTable.Set"/> did.</summary>
    public enum BindResult
    {
        /// <summary>Refused: the path is empty of meaning or belongs to the other device.</summary>
        Rejected,
        /// <summary>The action now uses the control; nothing else changed.</summary>
        Set,
        /// <summary>Another action of the same device already used the control: the two traded.</summary>
        Swapped,
    }

    /// <summary>
    /// Skyline's table of actions to controls (the creator, 2026-10-14: "I want to crouch/slide by R3, not Circle or B"):
    /// one keyboard-and-mouse binding and one pad binding per action, each an Input System control path such as
    /// <c>&lt;Keyboard&gt;/space</c>, <c>&lt;Mouse&gt;/rightButton</c>, <c>&lt;Gamepad&gt;/buttonSouth</c> or
    /// <c>&lt;Gamepad&gt;/rightStickPress</c>. A binding may list alternatives joined with '|' (Crouch is Left Ctrl or C by
    /// default; either one counts). A rebind replaces the whole binding with the one control. The conflict rule: binding a
    /// control that another action of the same device already uses swaps the two (that action takes the old binding of the
    /// one being rebound, in the place the control had). Pure (no engine): the Unity side reads the controls
    /// (SkylineBindings) and the pause menu edits the table.
    /// </summary>
    public sealed class BindingTable
    {
        public const int ActionCount = 9;
        public const char Alt = '|';
        public const string Header = "skyline-bindings 1";

        public static readonly BindAction[] AllActions =
        {
            BindAction.Jump, BindAction.Crouch, BindAction.Sprint, BindAction.Fire, BindAction.Aim,
            BindAction.Reload, BindAction.Swap, BindAction.Inspect, BindAction.Pause,
        };

        readonly string[,] _paths = new string[2, ActionCount];

        public BindingTable() { ResetToDefaults(); }

        /// <summary>The shipped binding. Pad: A/Cross jump, R3 crouch and slide, L3 sprint toggle, right trigger fire, left
        /// trigger aim, X/Square reload, Y/Triangle swap, D-pad up inspect, Start/Options pause; B/Circle is left free.</summary>
        public static string DefaultPath(BindDevice d, BindAction a)
        {
            if (d == BindDevice.KeyboardMouse)
            {
                switch (a)
                {
                    case BindAction.Jump: return "<Keyboard>/space";
                    case BindAction.Crouch: return "<Keyboard>/leftCtrl" + Alt + "<Keyboard>/c";
                    case BindAction.Sprint: return "<Keyboard>/leftShift";
                    case BindAction.Fire: return "<Mouse>/leftButton";
                    case BindAction.Aim: return "<Mouse>/rightButton";
                    case BindAction.Reload: return "<Keyboard>/r";
                    case BindAction.Swap: return "<Keyboard>/q";
                    case BindAction.Inspect: return "<Keyboard>/f";
                    default: return "<Keyboard>/escape";
                }
            }
            switch (a)
            {
                case BindAction.Jump: return "<Gamepad>/buttonSouth";
                case BindAction.Crouch: return "<Gamepad>/rightStickPress";
                case BindAction.Sprint: return "<Gamepad>/leftStickPress";
                case BindAction.Fire: return "<Gamepad>/rightTrigger";
                case BindAction.Aim: return "<Gamepad>/leftTrigger";
                case BindAction.Reload: return "<Gamepad>/buttonWest";
                case BindAction.Swap: return "<Gamepad>/buttonNorth";
                case BindAction.Inspect: return "<Gamepad>/dpad/up";
                default: return "<Gamepad>/start";
            }
        }

        public void ResetToDefaults()
        {
            for (int d = 0; d < 2; d++)
                for (int a = 0; a < ActionCount; a++)
                    _paths[d, a] = DefaultPath((BindDevice)d, (BindAction)a);
        }

        /// <summary>The binding (one path, or alternatives joined with '|'; empty when unbound).</summary>
        public string Get(BindDevice d, BindAction a) => _paths[(int)d, (int)a] ?? "";

        /// <summary>The binding's alternatives, each a control path (an empty array when unbound).</summary>
        public static string[] Split(string binding)
        {
            if (string.IsNullOrEmpty(binding)) return new string[0];
            var parts = binding.Split(Alt);
            var list = new List<string>();
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0 && !list.Contains(t)) list.Add(t);
            }
            return list.ToArray();
        }

        /// <summary>True when <paramref name="binding"/> lists <paramref name="path"/> (ignoring case).</summary>
        public static bool Uses(string binding, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var p in Split(binding)) if (string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Which device a control path belongs to: <c>&lt;Keyboard&gt;/...</c> and <c>&lt;Mouse&gt;/...</c> to the
        /// keyboard and mouse, <c>&lt;Gamepad&gt;/...</c> to the pad. False for anything else.</summary>
        public static bool TryDeviceOf(string path, out BindDevice device)
        {
            device = BindDevice.KeyboardMouse;
            if (string.IsNullOrEmpty(path)) return false;
            if (HasLayout(path, "<Keyboard>/") || HasLayout(path, "<Mouse>/")) return true;
            if (HasLayout(path, "<Gamepad>/")) { device = BindDevice.Pad; return true; }
            return false;
        }

        static bool HasLayout(string path, string prefix) =>
            path.Length > prefix.Length && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>True for a single control path that belongs to <paramref name="d"/>.</summary>
        public static bool IsValidPath(BindDevice d, string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf(Alt) >= 0) return false;
            return TryDeviceOf(path, out var dev) && dev == d;
        }

        /// <summary>Binds <paramref name="a"/> to <paramref name="path"/> on device <paramref name="d"/>. If another action of
        /// that device used the control, that action takes the old binding of <paramref name="a"/> where the control was (so no
        /// control is ever used twice, and nothing is left unbound by a swap).</summary>
        public BindResult Set(BindDevice d, BindAction a, string path)
        {
            if (!IsValidPath(d, path)) return BindResult.Rejected;
            int di = (int)d;
            string old = _paths[di, (int)a] ?? "";
            BindResult result = BindResult.Set;
            for (int o = 0; o < ActionCount; o++)
            {
                if (o == (int)a) continue;
                string other = _paths[di, o];
                if (!Uses(other, path)) continue;
                _paths[di, o] = Replace(other, path, old);
                result = BindResult.Swapped;
            }
            _paths[di, (int)a] = path;
            return result;
        }

        /// <summary>Leaves <paramref name="a"/> unbound on <paramref name="d"/>.</summary>
        public void Clear(BindDevice d, BindAction a) => _paths[(int)d, (int)a] = "";

        /// <summary><paramref name="binding"/> with the alternative <paramref name="path"/> replaced by the alternatives of
        /// <paramref name="with"/> (dropped when that is empty; repeated alternatives collapse).</summary>
        static string Replace(string binding, string path, string with)
        {
            var result = new List<string>();
            foreach (var p in Split(binding))
            {
                if (string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var w in Split(with)) if (!result.Contains(w)) result.Add(w);
                }
                else if (!result.Contains(p)) result.Add(p);
            }
            return string.Join(Alt.ToString(), result.ToArray());
        }

        // ---------------------------------------------------------------- saving

        /// <summary>One string for PlayerPrefs: a header line, then <c>kb.Jump=&lt;Keyboard&gt;/space</c> and
        /// <c>pad.Jump=...</c> lines.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            for (int d = 0; d < 2; d++)
                foreach (var a in AllActions)
                    sb.Append(d == 0 ? "kb." : "pad.").Append(a.ToString()).Append('=').Append(_paths[d, (int)a] ?? "").Append('\n');
            return sb.ToString();
        }

        /// <summary>The table a <see cref="Serialize"/>d string describes. Anything missing, unknown or on the wrong device
        /// falls back to the default for that slot, so a damaged or old save never breaks the controls.</summary>
        public static BindingTable Deserialize(string text)
        {
            var t = new BindingTable();
            if (string.IsNullOrEmpty(text)) return t;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq), value = line.Substring(eq + 1).Trim();
                BindDevice dev;
                string name;
                if (key.StartsWith("kb.", StringComparison.Ordinal)) { dev = BindDevice.KeyboardMouse; name = key.Substring(3); }
                else if (key.StartsWith("pad.", StringComparison.Ordinal)) { dev = BindDevice.Pad; name = key.Substring(4); }
                else continue;
                if (!Enum.IsDefined(typeof(BindAction), name)) continue;
                var action = (BindAction)Enum.Parse(typeof(BindAction), name);
                if (value.Length == 0) { t._paths[(int)dev, (int)action] = ""; continue; }
                bool ok = true;
                foreach (var p in Split(value)) if (!IsValidPath(dev, p)) { ok = false; break; }
                if (ok) t._paths[(int)dev, (int)action] = string.Join(Alt.ToString(), Split(value));
            }
            return t;
        }
    }

    /// <summary>Readable names for control paths, for the Controls page ("Right Mouse", "R3", "A / Cross").</summary>
    public static class BindingNames
    {
        static readonly Dictionary<string, string> Pad = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "buttonSouth", "A / Cross" }, { "buttonEast", "B / Circle" }, { "buttonWest", "X / Square" }, { "buttonNorth", "Y / Triangle" },
            { "leftShoulder", "LB / L1" }, { "rightShoulder", "RB / R1" }, { "leftTrigger", "LT / L2" }, { "rightTrigger", "RT / R2" },
            { "leftStickPress", "L3" }, { "rightStickPress", "R3" }, { "start", "Start / Options" }, { "select", "Back / Share" },
            { "dpad/up", "D-pad Up" }, { "dpad/down", "D-pad Down" }, { "dpad/left", "D-pad Left" }, { "dpad/right", "D-pad Right" },
        };

        static readonly Dictionary<string, string> Mouse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "leftButton", "Left Mouse" }, { "rightButton", "Right Mouse" }, { "middleButton", "Middle Mouse" },
            { "forwardButton", "Mouse Forward" }, { "backButton", "Mouse Back" },
        };

        static readonly Dictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "escape", "Esc" }, { "leftCtrl", "Left Ctrl" }, { "rightCtrl", "Right Ctrl" }, { "leftShift", "Left Shift" },
            { "rightShift", "Right Shift" }, { "leftAlt", "Left Alt" }, { "rightAlt", "Right Alt" }, { "backquote", "`" },
            { "semicolon", ";" }, { "quote", "'" }, { "comma", "," }, { "period", "." }, { "slash", "/" }, { "backslash", "\\" },
            { "minus", "-" }, { "equals", "=" }, { "leftBracket", "[" }, { "rightBracket", "]" },
        };

        /// <summary>"&lt;Mouse&gt;/rightButton" gives "Right Mouse"; alternatives ("a|b") give "A / B"; empty gives "Unbound".</summary>
        public static string Display(string binding)
        {
            var parts = BindingTable.Split(binding);
            if (parts.Length == 0) return "Unbound";
            var names = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++) names[i] = One(parts[i]);
            return string.Join(" / ", names);
        }

        static string One(string path)
        {
            int close = path.IndexOf('>');
            if (close < 0 || close + 2 > path.Length) return Words(path);
            string layout = path.Substring(1, close - 1);
            string rest = path.Substring(close + 2);
            string found;
            if (layout.Equals("Gamepad", StringComparison.OrdinalIgnoreCase)) return Pad.TryGetValue(rest, out found) ? found : Words(rest);
            if (layout.Equals("Mouse", StringComparison.OrdinalIgnoreCase)) return Mouse.TryGetValue(rest, out found) ? found : Words(rest);
            if (Keys.TryGetValue(rest, out found)) return found;
            if (rest.StartsWith("digit", StringComparison.OrdinalIgnoreCase) && rest.Length == 6) return rest.Substring(5);
            return rest.Length == 1 ? rest.ToUpperInvariant() : Words(rest);
        }

        /// <summary>"rightStickPress" gives "Right Stick Press".</summary>
        static string Words(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i == 0) sb.Append(char.ToUpperInvariant(c));
                else if (c == '/') sb.Append(' ');
                else
                {
                    if (char.IsUpper(c) && !char.IsUpper(s[i - 1]) && s[i - 1] != '/') sb.Append(' ');
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
