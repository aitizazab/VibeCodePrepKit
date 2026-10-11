using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// A virtual mouse (and, when asked, keyboard) fed through the Input System as real devices: what the opening
    /// editor reads is exactly what a real mouse would give it. The PlayMode tests and the capture tour drive the editor
    /// with it. Screen points are pixels with the origin at the bottom left (as Mouse.position reads).
    /// </summary>
    public sealed class VhVirtualInput : System.IDisposable
    {
        public readonly Mouse Mouse;
        public readonly Keyboard Keyboard;
        readonly bool _ownKeyboard;
        MouseState _m;
        readonly List<Key> _held = new List<Key>();
        readonly List<InputDevice> _disabled = new List<InputDevice>();

        /// <summary>A new mouse, and a keyboard: <paramref name="keyboard"/> when given (a test's), else a new one.
        /// <paramref name="disableReal"/>: the machine's own mice, keyboards and pads are switched off meanwhile.</summary>
        public VhVirtualInput(string name, Keyboard keyboard = null, bool disableReal = false)
        {
            if (disableReal)
                foreach (var d in InputSystem.devices)
                    if (d.enabled && (d is Mouse || d is Keyboard || d is Gamepad || d is Touchscreen || d is Pen))
                    {
                        InputSystem.DisableDevice(d);
                        _disabled.Add(d);
                    }
            Mouse = InputSystem.AddDevice<Mouse>(name + " mouse");
            Mouse.MakeCurrent();
            if (keyboard != null) Keyboard = keyboard;
            else
            {
                Keyboard = InputSystem.AddDevice<Keyboard>(name + " keyboard");
                _ownKeyboard = true;
            }
            Keyboard.MakeCurrent();
            _m = new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) };
        }

        public Vector2 Position => _m.position;

        void Send()
        {
            InputSystem.QueueStateEvent(Mouse, _m);
            _m.delta = Vector2.zero;
            _m.scroll = Vector2.zero;
        }

        /// <summary>Puts the pointer at <paramref name="screen"/> (the delta is the jump).</summary>
        public void MoveTo(Vector2 screen)
        {
            _m.delta = screen - (Vector2)_m.position;
            _m.position = screen;
            Send();
        }

        /// <summary>Moves the mouse by <paramref name="d"/> pixels (a mouse-look move when the cursor is locked).</summary>
        public void MoveBy(Vector2 d)
        {
            _m.position = (Vector2)_m.position + d;
            _m.delta = d;
            Send();
        }

        public void SetButton(MouseButton b, bool down)
        {
            _m = _m.WithButton(b, down);
            Send();
        }

        /// <summary>Wheel notches (+ away from you: in).</summary>
        public void Scroll(float notches)
        {
            _m.scroll = new Vector2(0f, notches);
            Send();
        }

        /// <summary>Holds exactly these keys (none: all up).</summary>
        public void Keys(params Key[] keys)
        {
            _held.Clear();
            _held.AddRange(keys);
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys));
        }

        public void Type(string text)
        {
            foreach (char c in text) InputSystem.QueueTextEvent(Keyboard, c);
        }

        public static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Taps keys together (a chord): down for two frames, then up.</summary>
        public IEnumerator Tap(params Key[] keys)
        {
            Keys(keys);
            yield return null;
            yield return null;
            Keys();
            yield return null;
            yield return null;
        }

        /// <summary>Moves there, presses and lets go of <paramref name="b"/>.</summary>
        public IEnumerator Click(Vector2 at, MouseButton b = MouseButton.Left)
        {
            MoveTo(at);
            yield return null;
            yield return null;
            SetButton(b, true);
            yield return null;
            yield return null;
            SetButton(b, false);
            yield return null;
            yield return null;
        }

        /// <summary>A press at <paramref name="from"/>, a move to <paramref name="to"/> in <paramref name="steps"/> frames, a release.</summary>
        public IEnumerator DragTo(Vector2 from, Vector2 to, int steps = 10, MouseButton b = MouseButton.Left)
        {
            MoveTo(from);
            yield return null;
            yield return null;
            SetButton(b, true);
            yield return null;
            yield return null;
            for (int i = 1; i <= steps; i++)
            {
                MoveTo(Vector2.Lerp(from, to, i / (float)steps));
                yield return null;
            }
            yield return null;
            SetButton(b, false);
            yield return null;
            yield return null;
        }

        public void Dispose()
        {
            if (Mouse != null && Mouse.added) InputSystem.RemoveDevice(Mouse);
            if (_ownKeyboard && Keyboard != null && Keyboard.added) InputSystem.RemoveDevice(Keyboard);
            foreach (var d in _disabled)
                if (d.added) InputSystem.EnableDevice(d);
            _disabled.Clear();
        }
    }

    /// <summary>
    /// The opening editor's capture tour (docs/OPENING-EDITOR.md, tools/vh-editor-capture.ps1): with
    /// "-opening-editor-tour &lt;folder&gt;" the build opens the editor and works it with a virtual mouse and keyboard
    /// (the machine's own are switched off meanwhile): hover, select, drag a handle, the tools, fly, the wheel, frame,
    /// type a number, the beats, the list, a shot, the reset question, the shot camera, the scrub bar. After each step
    /// it saves a screenshot and a line in captions.txt (what was done, the status line, the checks), then undoes
    /// everything and quits. Nothing is saved anywhere (the edits file is pointed into the folder and never written).
    /// </summary>
    public sealed class VhOpeningEditorTour : MonoBehaviour
    {
        public const string Flag = "-opening-editor-tour";

        string _dir;
        int _n;
        readonly StringBuilder _log = new StringBuilder();
        int _fails;
        VhVirtualInput _in;

        public static string RequestedFolder()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, Flag);
            if (i < 0) return null;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-")) return args[i + 1];
            return Path.Combine(Application.dataPath, "..", "opening-editor-ux", System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        void Start()
        {
            _dir = RequestedFolder();
            if (_dir == null)
            {
                Destroy(this);
                return;
            }
            Directory.CreateDirectory(_dir);
            StartCoroutine(Run());
        }

        void Line(string s)
        {
            _log.AppendLine(s);
            Debug.Log("JAMKIT_VH_TOUR " + s);
            File.WriteAllText(Path.Combine(_dir, "captions.txt"), _log.ToString());
        }

        void Check(bool ok, string what)
        {
            if (!ok) _fails++;
            Line("    CHECK " + (ok ? "ok  " : "FAIL") + "  " + what);
        }

        IEnumerator Shot(string name, string caption)
        {
            yield return VhVirtualInput.Frames(4);
            _n++;
            string file = _n.ToString("00") + "-" + name + ".png";
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, file));
            yield return VhVirtualInput.Frames(3);
            var ed = VhOpeningEditor.Instance;
            Line(file + "  |  " + caption);
            if (ed != null) Line("    status: " + ed.StatusText);
        }

        IEnumerator Wait(System.Func<bool> until, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!until() && Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Run()
        {
            Application.runInBackground = true;
            VhOpeningEditor.SaveOnClose = false;
            VhSceneEdits.FileOverride = Path.Combine(_dir, "edits-never-saved.json");
            if (InputSystem.settings != null) InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Line("Opening editor capture tour, " + System.DateTime.Now.ToString("s") + ", screen " + Screen.width + " x " + Screen.height +
                 (Screen.fullScreen ? " (full screen)" : " (window)"));
            yield return Wait(() => VhOpeningEditor.Instance != null, 30f);
            var ed = VhOpeningEditor.Instance;
            if (ed == null)
            {
                Line("No editor (is Visiting Hours the active template?). Stopping.");
                Quit();
                yield break;
            }
            if (!ed.IsOpen) ed.ApplyLaunchArgs(new[] { VhOpeningEditor.LaunchFlag });
            yield return Wait(() => ed.IsOpen, 60f);
            if (!ed.IsOpen)
            {
                Line("The editor didn't open within a minute. Stopping.");
                Quit();
                yield break;
            }
            // A first-run card or another overlay on top would cover the editor: close it.
            if (GameRoot.Ui.Top != null && GameRoot.Ui.Top != GameRoot.Ui.Title) GameRoot.Ui.CloseOverlays();
            _in = new VhVirtualInput("Tour", null, true);
            yield return VhVirtualInput.Frames(30);
            yield return Steps(ed);
            Line("");
            Line(_fails == 0 ? "All checks passed." : _fails + " check(s) FAILED.");
            // Undo everything, close without saving, give the devices back.
            for (int i = 0; i < 30 && ed.IsOpen && ed.Undo(); i++) { }
            if (ed.IsOpen) ed.Close();
            _in.Dispose();
            _in = null;
            yield return VhVirtualInput.Frames(5);
            Quit();
        }

        IEnumerator Steps(VhOpeningEditor ed)
        {
            _in.MoveTo(ed.ViewScreenPoint());
            yield return VhVirtualInput.Frames(5);
            yield return Shot("opened", "Opened with -edit-opening (held at the opening's start). Nothing selected.");

            if (ed.TryScreenPointOf("beat:The title", out var titleChip)) yield return _in.Click(titleChip);
            Check(ed.Opening != null && Mathf.Abs(ed.Opening.Time - ed.BeatTime("The title")) < 0.2f, "the 'The title' beat button jumps to the title");
            yield return Shot("title-beat", "Clicked the beat button 'The title': the title table.");

            yield return _in.Tap(Key.H);
            Check(ed.HelpShown, "H shows the help");
            yield return Shot("help", "H: the help page.");
            yield return _in.Tap(Key.H);
            Check(!ed.HelpShown, "H again hides it");

            if (ed.TryThingScreenPoint("title-tablet", out var tablet))
            {
                _in.MoveTo(tablet);
                yield return VhVirtualInput.Frames(6);
                Check(ed.Hovered != null && ed.Hovered.id == "title-tablet", "hovering the tablet outlines it (white box and its name)");
                yield return Shot("hover-tablet", "The mouse over the tablet: a white box and its name show what a click picks.");
                yield return _in.Click(tablet);
            }
            else Check(false, "the tablet is visible to click");
            Check(ed.Selected != null && ed.Selected.id == "title-tablet", "a click in the view selects the tablet");
            yield return Shot("select-tablet", "Clicked the tablet: orange box, its name, the move arrows, its numbers on the right.");

            if (ed.Selected != null && ed.TryHandleScreenPoint(0, out var xHandle))
            {
                Vector3 before = ed.Selected.target.position;
                var cam = ed.Stage.cam;
                Vector2 dir = ((Vector2)cam.WorldToScreenPoint(before + Vector3.right * 0.1f) - (Vector2)cam.WorldToScreenPoint(before)).normalized;
                _in.MoveTo(xHandle);
                yield return VhVirtualInput.Frames(4);
                yield return Shot("hover-x", "The mouse on the red X arrow: it lights up yellow.");
                _in.SetButton(MouseButton.Left, true);
                yield return VhVirtualInput.Frames(2);
                for (int i = 1; i <= 10; i++)
                {
                    _in.MoveTo(xHandle + dir * (8f * i));
                    yield return null;
                }
                yield return Shot("dragging-x", "Dragging the X arrow 80 px: the status line counts the move.");
                _in.SetButton(MouseButton.Left, false);
                yield return VhVirtualInput.Frames(3);
                Vector3 after = ed.Selected.target.position;
                Check(after.x - before.x > 0.005f && Mathf.Abs(after.y - before.y) < 1e-4f && Mathf.Abs(after.z - before.z) < 1e-4f,
                    "dragging X moved only X (" + ((after - before) * 100f).ToString("F1") + " cm)");
                yield return Shot("moved-x", "Let go: moved; '* unsaved', and 'What you've changed (1)' lists it.");
            }
            else Check(false, "the X arrow is on screen");

            yield return _in.Tap(Key.E);
            Check(ed.CurrentTool == VhOpeningEditor.Tool.Rotate, "E: the turn tool");
            yield return Shot("turn-tool", "E: the turn rings.");
            yield return _in.Tap(Key.R);
            Check(ed.CurrentTool == VhOpeningEditor.Tool.Scale, "R: the size tool");
            yield return Shot("size-tool", "R: the size boxes.");
            yield return _in.Tap(Key.W);

            var c0 = ed.Stage.cam.transform.position;
            var f0 = ed.Stage.cam.transform.forward;
            _in.MoveTo(ed.ViewScreenPoint());
            yield return VhVirtualInput.Frames(2);
            _in.SetButton(MouseButton.Right, true);
            yield return VhVirtualInput.Frames(2);
            // A realistic hold: one second of S (backing away), the mouse turning right meanwhile.
            _in.Keys(Key.S);
            float flyStart = Time.unscaledTime;
            var flyFrom = ed.Stage.cam.transform.position;
            bool shot = false;
            while (Time.unscaledTime - flyStart < 1f)
            {
                _in.MoveBy(new Vector2(2f, 0f));
                yield return null;
                if (!shot && Time.unscaledTime - flyStart > 0.5f)
                {
                    shot = true;
                    yield return Shot("flying", "Right mouse held, S held for a second, the mouse moving right: backing away and turning. The bar says FREE CAMERA (flying), the tip says how fast.");
                }
            }
            float flew = Time.unscaledTime - flyStart;
            float dist = Vector3.Distance(ed.Stage.cam.transform.position, flyFrom);
            _in.Keys();
            yield return null;
            _in.SetButton(MouseButton.Right, false);
            yield return VhVirtualInput.Frames(3);
            var c1 = ed.Stage.cam.transform.position;
            Line("    fly: " + dist.ToString("F2") + " m in " + flew.ToString("F2") + " s held (" + (dist / Mathf.Max(0.01f, flew)).ToString("F2") + " m/s; the fly speed is " + ed.FlySpeed.ToString("F1") + " m/s, speeding up after a second held)");
            Check(Vector3.Dot(c1 - c0, f0) < -0.5f, "a second of right mouse + S backs the camera away (" + Vector3.Dot(c1 - c0, f0).ToString("F2") + " m)");
            Check(Vector3.SignedAngle(f0, ed.Stage.cam.transform.forward, Vector3.up) > 5f, "the mouse turned the view right");

            var c2 = ed.Stage.cam.transform.position;
            _in.Scroll(2f);
            yield return VhVirtualInput.Frames(3);
            Check(Vector3.Dot(ed.Stage.cam.transform.position - c2, ed.Stage.cam.transform.forward) > 0.01f, "the wheel moves the camera in");
            yield return _in.Tap(Key.F);
            yield return Shot("framed", "F: framed the tablet.");

            if (ed.TryScreenPointOf("field:pos.y", out var yField))
            {
                float y0 = ed.Selected.target.localPosition.y;
                yield return _in.Click(yField);
                Check(ed.Typing, "a click on a number box starts typing");
                _in.Type("82.5");
                yield return VhVirtualInput.Frames(2);
                yield return Shot("typing", "Clicked the Y position box and typed 82.5 (the box is lit; the shortcuts are off).");
                yield return _in.Tap(Key.Enter);
                Check(Mathf.Abs(ed.Selected.target.localPosition.y - 0.825f) < 1e-3f, "Enter applies 82.5 cm (was " + (y0 * 100f).ToString("F1") + ")");
                yield return Shot("typed", "Enter: applied.");
            }
            else Check(false, "the Y position box is on screen");

            if (ed.TryScreenPointOf("beat:The phone on the nightstand", out var nightChip))
            {
                yield return _in.Click(nightChip);
                Check(Mathf.Abs(ed.Opening.Time - ed.BeatTime("The phone on the nightstand")) < 0.2f, "the beat button jumps to 'The phone on the nightstand'");
                Check(ed.Stage.Pose != "table", "and the camera went to the bedroom");
            }
            Check(ed.Selected == null, "the tablet (at the title table) isn't left selected in the bedroom");
            yield return Shot("bedroom-beat", "Clicked 'The phone on the nightstand': the bedroom (the camera went there; the tablet's selection let go).");
            yield return _in.Tap(Key.L);
            Check(ed.WorkLight, "L: the work light");
            yield return Shot("work-light", "L: the work light on (an even light to see the set; only while editing).");

            yield return ClickListRow(ed, "phone");
            Check(ed.Selected != null && ed.Selected.id == "phone", "a click in the list selects the phone");
            yield return Shot("select-phone", "Clicked 'The phone' in the list: selected, with its note on what follows it.");

            // The phone in her hand (the creator: "I can't seem to move the phone when it's held in front of Nadia").
            if (ed.TryScreenPointOf("beat:Holding it: the voicemail", out var holdChip)) yield return _in.Click(holdChip);
            Check(ed.Stage.PhoneInHand, "'Holding it: the voicemail': she holds the phone up");
            if (ed.TryThingScreenPoint("phone-held", out var heldAt))
            {
                yield return _in.Click(heldAt);
                Check(ed.Selected != null && ed.Selected.id == "phone-held", "a click on the phone in her hand selects The phone (held in her hand)");
                yield return Shot("held-phone-selected", "At 'Holding it', the phone in her hand clicked: The phone (held in her hand), its arrows in front of her eyes.");
                var heldT = ed.Selected != null ? ed.Selected.target : null;
                if (heldT != null && ed.TryHandleScreenPoint(0, out var hx))
                {
                    Vector3 h0 = heldT.position, p0 = ed.Stage.phone.position;
                    var cam = ed.Stage.cam;
                    Vector2 dir = ((Vector2)cam.WorldToScreenPoint(h0 + Vector3.right * 0.03f) - (Vector2)cam.WorldToScreenPoint(h0)).normalized;
                    yield return _in.DragTo(hx, hx + dir * 80f, 10);
                    yield return VhVirtualInput.Frames(3);
                    float moved = Vector3.Distance(ed.Stage.phone.position, p0);
                    Check(heldT.position.x - h0.x > 0.004f, "the X arrow moved where she holds it (" + ((heldT.position - h0) * 100f).ToString("F1") + " cm)");
                    Check(moved > 0.002f || ed.StatusText.Contains("can't reach"), "the phone in her hand moved " + (moved * 100f).ToString("F1") + " cm (or the status line says her reach stops it)");
                    yield return Shot("held-phone-moved", "Dragged its X arrow: the phone in her hand moved, her hand with it.");
                    yield return _in.Tap(Key.LeftCtrl, Key.Z);
                }
                else Check(false, "the held phone's X arrow is on screen");
            }
            else Check(false, "the phone in her hand is visible to click");

            yield return ClickListRow(ed, "shot:nightstand");
            if (ed.TryScreenPointOf("button:look-through", out var look)) yield return _in.Click(look);
            yield return Shot("look-through", "A shot selected, then 'Look through it': the free camera at that shot.");
            if (ed.TryScreenPointOf("button:set-from-view", out var setView))
            {
                _in.MoveTo(setView);
                yield return VhVirtualInput.Frames(2);
                float until = Time.unscaledTime + 0.8f;
                while (Time.unscaledTime < until) yield return null;
                yield return Shot("tooltip", "The mouse resting on 'Set from my view': its tip shows next to it.");
            }

            if (ed.TryScreenPointOf("button:reset-all", out var resetAll)) yield return _in.Click(resetAll);
            Check(ed.ConfirmShown, "'Reset everything...' asks first");
            yield return Shot("reset-question", "'Reset everything...': it asks first.");
            yield return _in.Tap(Key.Escape);
            Check(!ed.ConfirmShown, "Esc cancels the question");

            yield return _in.Tap(Key.C);
            Check(!ed.FreeCamera, "C: the shot camera");
            yield return Shot("shot-camera", "C: the opening's own shot camera (the bar says SHOT CAMERA).");
            yield return _in.Tap(Key.C);

            if (ed.TryTrackScreenPoint(ed.Opening.Length * 0.15f, out var a) && ed.TryTrackScreenPoint(ed.Opening.Length * 0.4f, out var b))
            {
                _in.MoveTo(a);
                yield return VhVirtualInput.Frames(2);
                _in.SetButton(MouseButton.Left, true);
                yield return VhVirtualInput.Frames(2);
                for (int i = 1; i <= 8; i++)
                {
                    _in.MoveTo(Vector2.Lerp(a, b, i / 8f));
                    yield return null;
                }
                yield return Shot("scrubbing", "Dragging along the scrub bar.");
                _in.SetButton(MouseButton.Left, false);
                yield return VhVirtualInput.Frames(3);
                Check(Mathf.Abs(ed.Opening.Time - ed.Opening.Length * 0.4f) < ed.Opening.Length * 0.03f, "the scrub bar sets the time");
            }

            if (ed.TryScreenPointOf("hide:left", out var hideLeft)) yield return _in.Click(hideLeft);
            if (ed.TryScreenPointOf("hide:right", out var hideRight)) yield return _in.Click(hideRight);
            yield return Shot("side-panels-hidden", "Both side panels hidden (the tabs at the top corners bring them back).");
            if (ed.TryScreenPointOf("show:left", out var showLeft)) yield return _in.Click(showLeft);
            if (ed.TryScreenPointOf("show:right", out var showRight)) yield return _in.Click(showRight);
            yield return _in.Tap(Key.Tab);
            Check(ed.PanelsHidden, "Tab hides the panels");
            yield return Shot("tab-clean-view", "Tab: a clean view (only the bar at the bottom).");
            yield return _in.Tap(Key.Tab);
            Check(!ed.PanelsHidden, "Tab brings them back");
        }

        /// <summary>Clicks a row of the list, scrolling the list with the wheel until it shows.</summary>
        IEnumerator ClickListRow(VhOpeningEditor ed, string id)
        {
            for (int i = 0; i < 12; i++)
            {
                if (ed.TryScreenPointOf("list:" + id, out var p))
                {
                    yield return _in.Click(p);
                    yield break;
                }
                if (ed.TryPanelScreenPoint("left", out var left))
                {
                    _in.MoveTo(left);
                    yield return null;
                    _in.Scroll(-2f);
                    yield return VhVirtualInput.Frames(3);
                }
            }
        }

        void Quit()
        {
            Line("Done. " + _n + " pictures in " + _dir);
            Application.Quit();
        }

        void OnDestroy()
        {
            _in?.Dispose();
        }
    }
}
