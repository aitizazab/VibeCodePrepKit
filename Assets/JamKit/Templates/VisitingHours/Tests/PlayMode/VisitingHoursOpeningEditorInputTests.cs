using System.Collections;
using System.IO;
using JamKit.Flow;
using JamKit.Shell;
using JamKit.Tests.Play;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>
    /// The opening editor worked the way the creator works it (docs/OPENING-EDITOR.md): through a test keyboard and a
    /// test mouse fed to the Input System, never by calling the editor's own methods. Opening with F9, flying the free
    /// camera (right mouse + W, the mouse turning the view) while the opening holds still, panning, the wheel, F, clicking
    /// in the view and on the panels, dragging the X arrow, snapping, undo and redo, typing a number, the beat buttons
    /// and the scrub bar, a shot set from the view, saving and building again with the save, and closing (the opening
    /// plays on, the title takes input again). Edits go to a file in the temp folder, never the repository.
    /// </summary>
    public class VisitingHoursOpeningEditorInputTests
    {
        TestInput _keys;
        /// <summary>What the status line said as the editor opened (before a test's own setup keys).</summary>
        string _openStatus;
        VhVirtualInput _in;
        string _file;

        static VhOpeningEditor Ed => VhOpeningEditor.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            VhOpeningEditor.SaveOnClose = false;
            _file = Path.Combine(Path.GetTempPath(), "vh-opening-edits-test-" + System.Guid.NewGuid().ToString("N") + ".json");
            VhSceneEdits.FileOverride = _file;
            var c = Object.Instantiate(GameRoot.Config);
            c.firstGameplayScene = VhPresenter.Scene;
            c.chooseDifficultyOnPlay = false;
            c.showHowToPlayOnFirstRun = false;
            GameRoot.UseConfig(c);
            VhBoot.Refresh();
            yield return WaitFor(() => VhBoot.Stage != null && VhBoot.Stage.Interactive, "the title table", 10f);
            _keys = new TestInput("EditorTestKeyboard");
            _in = new VhVirtualInput("EditorTest", _keys.Keyboard);
            GameRoot.Input.SetPadMode(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Ed != null && Ed.IsOpen) Ed.Close();
            VhOpeningEditor.SaveOnClose = true;
            if (VhBoot.Opening != null) VhBoot.Opening.Finish(true);
            _in?.Dispose();
            _in = null;
            _keys?.Dispose();
            _keys = null;
            VhSceneEdits.FileOverride = null;
            if (_file != null && File.Exists(_file)) File.Delete(_file);
            // The next test builds the sets from the real (committed) edits again.
            var runner = GameRoot.Instance != null ? GameRoot.Instance.GetComponent<VhTitleRunner>() : null;
            if (runner != null) runner.DestroyStage();
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            yield return WaitFor(() => !GameRoot.IsLoadingScene, "any load to finish");
            UseSandboxConfig();
            VhBoot.Refresh();
            yield return null;
        }

        IEnumerator OpenWithF9()
        {
            Assert.That(Ed, Is.Not.Null, "the editor lives on the GameRoot");
            Assert.That(Ed.IsOpen, Is.False);
            yield return _in.Tap(Key.F9);
            yield return WaitFor(() => Ed.IsOpen, "F9 to open the editor", 5f);
            _openStatus = Ed.StatusText;
            // Its panels laid out (the list's rows have a size).
            yield return WaitFor(() => Ed.TryScreenPointOf("button:close", out _), "the editor's panels to lay out", 5f);
            _in.MoveTo(Ed.ViewScreenPoint());
            yield return Frames(2);
            // The editor lives on the GameRoot and keeps its tool and snapping from one opening to the next (as the
            // Scene view does): start each test from the move tool with snapping on, by the keys.
            if (Ed.CurrentTool != VhOpeningEditor.Tool.Move) yield return _in.Tap(Key.W);
            if (!Ed.Snap) yield return _in.Tap(Key.X);
            Assert.That(Ed.CurrentTool, Is.EqualTo(VhOpeningEditor.Tool.Move), "W: the move tool");
            Assert.That(Ed.Snap, Is.True, "X: snapping on");
        }

        Vector2 Point(string id)
        {
            Assert.That(Ed.TryScreenPointOf(id, out var p), Is.True, "\"" + id + "\" shows on screen");
            return p;
        }

        IEnumerator ClickThing(string id)
        {
            Assert.That(Ed.TryThingScreenPoint(id, out var p), Is.True, id + " shows in the view");
            Assert.That(Ed.IsOverPanels(p), Is.False, "and not under a panel");
            yield return _in.Click(p);
        }

        /// <summary>Scrolls the list with the wheel (the mouse over its panel) until the row can be clicked, then clicks it.</summary>
        IEnumerator ClickListRow(string id)
        {
            for (int i = 0; i < 20 && !Ed.TryScreenPointOf("list:" + id, out _); i++)
            {
                Assert.That(Ed.TryPanelScreenPoint("left", out var left), Is.True, "the list panel");
                _in.MoveTo(left);
                yield return null;
                _in.Scroll(-2f);
                yield return Frames(3);
            }
            yield return _in.Click(Point("list:" + id));
        }

        IEnumerator HoldKeyFor(Key key, float seconds)
        {
            _in.Keys(key);
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
            _in.Keys();
            yield return null;
        }

        static Transform Cam => Ed.Stage.cam.transform;

        // ---------- Opening, closing, nothing leaking ----------

        [UnityTest]
        public IEnumerator F9OpensItHeldStillAndF9ClosesItBackToTheTitle()
        {
            yield return OpenWithF9();
            Assert.That(Ed.Opening, Is.Not.Null);
            Assert.That(Ed.Opening.Paused, Is.True, "the opening holds still while editing");
            Assert.That(Ed.Stage.table.Live, Is.False, "the title table doesn't take input under the editor");
            float t0 = Ed.Opening.Time;
            yield return Frames(20);
            Assert.That(Ed.Opening.Time, Is.EqualTo(t0).Within(1e-4f), "held");
            Assert.That(_openStatus, Does.Contain("Editing the opening"), "the status line says what's open");

            // Enter (the title's Submit) and a click on the tablet don't press Play under the editor.
            yield return _in.Tap(Key.Enter);
            yield return ClickThing("title-tablet");
            yield return Frames(3);
            Assert.That(GameRoot.Flow.Current, Is.EqualTo(GameState.Title), "still on the title: nothing leaked into the game");
            Assert.That(Ed.Selected != null && Ed.Selected.id == "title-tablet", Is.True, "the click selected the tablet instead");

            // L: the work light (the stage's ambient boost); Tab: the panels away and back.
            yield return _in.Tap(Key.L);
            Assert.That(Ed.WorkLight, Is.True, "L turns the work light on");
            Assert.That(Ed.Stage.workLight, Is.EqualTo(1f), "the stage lights the set evenly");
            yield return _in.Tap(Key.Tab);
            Assert.That(Ed.PanelsHidden, Is.True, "Tab hides the panels");
            Assert.That(Ed.TryScreenPointOf("button:play", out _), Is.False, "the timeline is gone");
            yield return _in.Tap(Key.Tab);
            Assert.That(Ed.PanelsHidden, Is.False);
            yield return Frames(2);
            Assert.That(Ed.TryScreenPointOf("button:play", out _), Is.True, "and back");

            // A pad touched by accident doesn't hide the cursor here.
            GameRoot.Input.SetPadMode(true);
            yield return Frames(2);
            Assert.That(GameRoot.Input.PadMode, Is.False, "the editor keeps the mouse cursor");

            yield return _in.Tap(Key.F9);
            Assert.That(Ed.IsOpen, Is.False, "F9 closes it");
            yield return Frames(3);
            Assert.That(Ed.Stage == null || !Ed.Stage.editorCamera, Is.True, "the shots have the camera again");
            Assert.That(VhBoot.Stage.table.Live, Is.True, "the title table takes input again");
            Assert.That(VhBoot.Stage.workLight, Is.EqualTo(0f), "the work light goes with the editor");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ClosingMidOpeningPlaysOnFromThere()
        {
            Assert.That(VhBoot.PlayOpeningNow(1f), Is.Not.Null, "the opening plays");
            float end = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < end) yield return null;
            yield return OpenWithF9();
            float t0 = Ed.Opening.Time;
            Assert.That(t0, Is.GreaterThan(0.5f), "opened part way in");
            end = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.That(Ed.Opening.Time, Is.EqualTo(t0).Within(1e-4f), "held while editing");
            // Space plays it from here; Space again pauses.
            yield return _in.Tap(Key.Space);
            Assert.That(Ed.Playing, Is.True, "Space plays");
            Assert.That(Ed.FreeCamera, Is.False, "through the shot cameras");
            end = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.That(Ed.Opening.Time, Is.GreaterThan(t0 + 0.2f), "it plays on");
            yield return _in.Tap(Key.Space);
            Assert.That(Ed.Playing, Is.False, "Space pauses");
            var opening = Ed.Opening;
            float t1 = opening.Time;
            yield return _in.Tap(Key.F9);
            Assert.That(Ed.IsOpen, Is.False);
            end = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.That(opening.Running, Is.True, "the opening goes on after closing");
            Assert.That(opening.Time, Is.GreaterThan(t1 + 0.3f), "from where it was");
        }

        // ---------- The free camera ----------

        [UnityTest]
        public IEnumerator RightMouseAndWFlyForwardAndTheMouseTurnsTheView()
        {
            yield return OpenWithF9();
            Assert.That(Ed.IsOverPanels(_in.Position), Is.False, "the mouse is in the view");
            float held = Ed.Opening.Time;
            var p0 = Cam.position;
            var f0 = Cam.forward;

            // Without the right button, W is the move tool, not a flight.
            yield return _in.Tap(Key.E);
            yield return _in.Tap(Key.W);
            Assert.That(Ed.CurrentTool, Is.EqualTo(VhOpeningEditor.Tool.Move), "W: the move tool");
            Assert.That(Vector3.Distance(Cam.position, p0), Is.LessThan(1e-4f), "W alone doesn't fly");

            _in.SetButton(MouseButton.Right, true);
            yield return Frames(2);
            float t0 = Time.unscaledTime;
            var start = Cam.position;
            yield return HoldKeyFor(Key.W, 0.5f);
            float t1 = Time.unscaledTime;
            var moved = Cam.position - start;
            float along = Vector3.Dot(moved, f0);
            float expected = Ed.FlySpeed * (t1 - t0);
            Assert.That(along, Is.GreaterThan(0.1f), "right mouse + W flies forward");
            Assert.That(along, Is.InRange(expected * 0.5f, expected * 1.5f), "at the fly speed (" + Ed.FlySpeed + " m/s for " + (t1 - t0).ToString("F2") + " s)");
            Assert.That((moved - f0 * along).magnitude, Is.LessThan(0.03f), "straight ahead");

            float yaw0 = Cam.eulerAngles.y, pitch0 = Cam.eulerAngles.x;
            _in.MoveBy(new Vector2(100f, 0f));
            yield return Frames(3);
            Assert.That(Mathf.DeltaAngle(yaw0, Cam.eulerAngles.y), Is.EqualTo(100f * VhOpeningEditor.LookDegreesPerPixel).Within(0.5f), "the mouse turns the view right");
            Assert.That(Mathf.DeltaAngle(pitch0, Cam.eulerAngles.x), Is.EqualTo(0f).Within(0.5f), "and not up or down");
            _in.MoveBy(new Vector2(0f, 40f));
            yield return Frames(3);
            Assert.That(Mathf.DeltaAngle(pitch0, Cam.eulerAngles.x), Is.EqualTo(-40f * VhOpeningEditor.LookDegreesPerPixel).Within(0.5f), "moving it up looks up");
            _in.SetButton(MouseButton.Right, false);
            yield return Frames(2);
            Assert.That(Ed.Opening.Time, Is.EqualTo(held).Within(1e-4f), "the opening held still all along");
            Assert.That(Ed.FreeCamera, Is.True);
        }

        [UnityTest]
        public IEnumerator MiddleMousePansTheWheelMovesInAndFFrames()
        {
            yield return OpenWithF9();
            var p0 = Cam.position;
            var r0 = Cam.rotation;
            _in.SetButton(MouseButton.Middle, true);
            yield return Frames(2);
            for (int i = 0; i < 5; i++)
            {
                _in.MoveBy(new Vector2(20f, 0f));
                yield return null;
            }
            _in.SetButton(MouseButton.Middle, false);
            yield return Frames(2);
            var d = Cam.position - p0;
            Assert.That(Vector3.Dot(d, r0 * Vector3.right), Is.LessThan(-0.005f), "dragging right moves the view so the scene follows the mouse");
            Assert.That(Quaternion.Angle(r0, Cam.rotation), Is.LessThan(0.01f), "panning doesn't turn");

            var p1 = Cam.position;
            _in.Scroll(1f);
            yield return Frames(3);
            Assert.That(Vector3.Dot(Cam.position - p1, Cam.forward), Is.GreaterThan(0.005f), "the wheel moves in");
            Assert.That(Quaternion.Angle(r0, Cam.rotation), Is.LessThan(0.01f), "without turning");

            yield return ClickThing("title-tablet");
            Assert.That(Ed.Selected != null && Ed.Selected.id == "title-tablet", Is.True);
            // Look away, then F.
            _in.MoveTo(Ed.ViewScreenPoint());
            yield return null;
            _in.SetButton(MouseButton.Right, true);
            yield return Frames(2);
            _in.MoveBy(new Vector2(400f, 0f));
            yield return Frames(2);
            _in.SetButton(MouseButton.Right, false);
            yield return Frames(2);
            yield return _in.Tap(Key.F);
            // Framed: the whole thing in the part of the view the panels leave free, filling a good share of it, in its
            // middle (whatever the lens: the title shot's is narrow).
            Bounds b = default;
            bool any = false;
            foreach (var r in Ed.Selected.target.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (!any) b = r.bounds;
                else b.Encapsulate(r.bounds);
                any = true;
            }
            Assert.That(any, Is.True, "the tablet is drawn");
            var view = Ed.ViewScreenRect();
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                Vector3 sp = Ed.Stage.cam.WorldToScreenPoint(c);
                Assert.That(sp.z, Is.GreaterThan(0f), "F: all of it in front of the camera");
                x0 = Mathf.Min(x0, sp.x);
                y0 = Mathf.Min(y0, sp.y);
                x1 = Mathf.Max(x1, sp.x);
                y1 = Mathf.Max(y1, sp.y);
            }
            string seen = "box " + Rect.MinMaxRect(x0, y0, x1, y1) + " in the free view " + view;
            Assert.That(x0 >= view.xMin - 2f && x1 <= view.xMax + 2f && y0 >= view.yMin - 2f && y1 <= view.yMax + 2f, Is.True, "F: all of it in the free view, under no panel (" + seen + ")");
            Assert.That(Mathf.Max((x1 - x0) / view.width, (y1 - y0) / view.height), Is.GreaterThan(0.35f), "F: it fills a good share of the view (" + seen + ")");
            Assert.That(Mathf.Abs((x0 + x1) * 0.5f - view.center.x) / view.width, Is.LessThan(0.12f), "F: centred across the free view (" + seen + ")");
            Assert.That(Mathf.Abs((y0 + y1) * 0.5f - view.center.y) / view.height, Is.LessThan(0.12f), "F: centred up and down (" + seen + ")");
        }

        // ---------- Selecting, handles, snapping, undo ----------

        [UnityTest]
        public IEnumerator AClickInTheViewSelectsAndAClickOnAPanelDoesnt()
        {
            yield return OpenWithF9();
            Assert.That(Ed.Selected, Is.Null);
            Assert.That(Ed.TryThingScreenPoint("title-tablet", out var tablet), Is.True);
            _in.MoveTo(tablet);
            yield return Frames(3);
            Assert.That(Ed.Hovered != null && Ed.Hovered.id == "title-tablet", Is.True, "hovering shows what a click would pick");
            yield return _in.Click(tablet);
            Assert.That(Ed.Selected != null && Ed.Selected.id == "title-tablet", Is.True, "the click selects it");
            Assert.That(Ed.StatusText, Does.Contain("Selected"), "and says so");

            foreach (var panel in new[] { "left", "right", "top", "bottom" })
            {
                Assert.That(Ed.TryPanelScreenPoint(panel, out var p), Is.True, "the " + panel + " panel");
                yield return _in.Click(p);
                Assert.That(Ed.Selected != null && Ed.Selected.id == "title-tablet", Is.True, "a click on the " + panel + " panel doesn't reach the view (" + Ed.LastPress + ")");
                Assert.That(Ed.CurrentTool, Is.EqualTo(VhOpeningEditor.Tool.Move), "a click on the " + panel + " panel's background presses nothing (" + Ed.LastPress + ")");
                Assert.That(Ed.ConfirmShown || Ed.HelpShown, Is.False, "nothing opened (" + Ed.LastPress + ")");
            }
            yield return _in.Tap(Key.Escape);
            Assert.That(Ed.Selected, Is.Null, "Esc selects nothing");

            yield return ClickListRow("title-mug");
            Assert.That(Ed.Selected != null && Ed.Selected.id == "title-mug", Is.True, "a click in the list selects (" + Ed.LastPress + ")");
        }

        [UnityTest]
        public IEnumerator DraggingTheXArrowMovesOnlyXSnappedAndUndoRedoAndEscWork()
        {
            yield return OpenWithF9();
            yield return ClickThing("title-tablet");
            var t = Ed.Selected.target;
            Vector3 start = t.position;
            Assert.That(Ed.Snap, Is.True, "snapping starts on");
            Assert.That(Ed.TryHandleScreenPoint(0, out var x), Is.True, "the X arrow shows");
            var cam = Ed.Stage.cam;
            Vector2 dir = ((Vector2)cam.WorldToScreenPoint(start + Vector3.right * 0.1f) - (Vector2)cam.WorldToScreenPoint(start)).normalized;
            yield return _in.DragTo(x, x + dir * 80f, 10);
            Vector3 d = t.position - start;
            Assert.That(d.x, Is.GreaterThan(0.005f), "dragging the X arrow moves along X (" + Ed.LastPress + ")");
            Assert.That(Mathf.Abs(d.y) + Mathf.Abs(d.z), Is.LessThan(1e-4f), "and only along X");
            Assert.That(Mathf.Abs(d.x * 100f - Mathf.Round(d.x * 100f)), Is.LessThan(1e-3f), "in whole centimetres (snapping)");
            Assert.That(Ed.StatusText, Does.Contain("along X"), "the status line says what moved");
            Assert.That(Ed.Dirty, Is.True, "unsaved");
            Vector3 moved = t.position;

            yield return _in.Tap(Key.LeftCtrl, Key.Z);
            Assert.That(Vector3.Distance(t.position, start), Is.LessThan(1e-4f), "Ctrl+Z undoes");
            yield return _in.Tap(Key.LeftCtrl, Key.Y);
            Assert.That(Vector3.Distance(t.position, moved), Is.LessThan(1e-4f), "Ctrl+Y redoes");

            // Esc while dragging cancels.
            Assert.That(Ed.TryHandleScreenPoint(0, out x), Is.True);
            _in.MoveTo(x);
            yield return Frames(2);
            _in.SetButton(MouseButton.Left, true);
            yield return Frames(2);
            for (int i = 1; i <= 6; i++)
            {
                _in.MoveTo(x + dir * (10f * i));
                yield return null;
            }
            Assert.That(Vector3.Distance(t.position, moved), Is.GreaterThan(0.004f), "it follows the drag");
            yield return _in.Tap(Key.Escape);
            _in.SetButton(MouseButton.Left, false);
            yield return Frames(2);
            Assert.That(Vector3.Distance(t.position, moved), Is.LessThan(1e-4f), "Esc put it back");
            Assert.That(Ed.Selected, Is.Not.Null, "and kept the selection");

            // Snapping off (X): free moves.
            yield return _in.Tap(Key.X);
            Assert.That(Ed.Snap, Is.False, "X turns snapping off");
            Assert.That(Ed.StatusText, Does.Contain("off"));
            bool fractional = false;
            foreach (float px in new[] { 23f, 37f })
            {
                Vector3 before = t.position;
                Assert.That(Ed.TryHandleScreenPoint(0, out x), Is.True);
                yield return _in.DragTo(x, x + dir * px, 6);
                float dx = (t.position.x - before.x) * 100f;
                Assert.That(Mathf.Abs(dx), Is.GreaterThan(0.1f), "it moved");
                if (Mathf.Abs(dx - Mathf.Round(dx)) > 1e-3f) fractional = true;
            }
            Assert.That(fractional, Is.True, "without snapping it isn't held to whole centimetres");
        }

        [UnityTest]
        public IEnumerator TypingANumberAppliesItAndTheShortcutsWait()
        {
            yield return OpenWithF9();
            yield return ClickThing("title-tablet");
            yield return _in.Click(Point("field:pos.y"));
            Assert.That(Ed.Typing, Is.True, "a click on a number box starts typing");
            yield return _in.Tap(Key.E);
            Assert.That(Ed.CurrentTool, Is.EqualTo(VhOpeningEditor.Tool.Move), "E while typing isn't the turn tool");
            _in.Type("82.5");
            yield return Frames(2);
            yield return _in.Tap(Key.Enter);
            Assert.That(Ed.Typing, Is.False, "Enter applies");
            Assert.That(Ed.Selected.target.localPosition.y, Is.EqualTo(0.825f).Within(1e-4f), "82.5 cm");
            Assert.That(Ed.StatusText, Does.Contain("82.5"));

            // Esc cancels a typed number.
            yield return _in.Click(Point("field:pos.x"));
            float x0 = Ed.Selected.target.localPosition.x;
            _in.Type("40");
            yield return Frames(2);
            yield return _in.Tap(Key.Escape);
            Assert.That(Ed.Selected.target.localPosition.x, Is.EqualTo(x0).Within(1e-5f), "Esc leaves it as it was");
            Assert.That(Ed.Selected, Is.Not.Null, "and doesn't deselect");
        }

        // ---------- The timeline ----------

        [UnityTest]
        public IEnumerator TheBeatButtonsTheArrowsAndTheScrubBarChangeTheMoment()
        {
            yield return OpenWithF9();
            Assert.That(Ed.Stage.Pose, Is.EqualTo("table"));
            yield return ClickThing("title-tablet");
            Assert.That(Ed.Selected, Is.Not.Null);
            yield return _in.Click(Point("beat:The grab"));
            Assert.That(Ed.Selected, Is.Null, "the tablet (at the title table) isn't left selected in the bedroom");
            Assert.That(Ed.StatusText, Does.Contain("nothing is selected"), "and the status line says so");
            Assert.That(Ed.Opening.Time, Is.EqualTo(Ed.BeatTime("The grab")).Within(0.05f), "the beat button jumps there");
            Assert.That(Ed.Stage.Pose, Is.Not.EqualTo("table"), "the bedroom");
            Assert.That(Vector3.Distance(Cam.position, Ed.Stage.bedroom.position), Is.LessThan(6f), "the free camera went to the bedroom too");
            Assert.That(Ed.Opening.Paused, Is.True, "still held");

            yield return _in.Tap(Key.RightArrow);
            Assert.That(Ed.Opening.Time, Is.EqualTo(Ed.BeatTime("Holding it: the voicemail")).Within(0.05f), "the right arrow: the next beat");
            yield return _in.Tap(Key.LeftArrow);
            Assert.That(Ed.Opening.Time, Is.EqualTo(Ed.BeatTime("The grab")).Within(0.05f), "the left arrow: back");

            // The free camera, untouched since the jump put it on the shot, follows the shots as you go.
            Assert.That(Ed.CameraFollowsShots, Is.True, "the free camera is on the shot");
            int pose = Ed.Stage.PoseIndex(Ed.Stage.Pose);
            Assert.That(Vector3.Distance(Cam.position, Ed.Stage.posePositions[pose]), Is.LessThan(1e-3f), "at this beat's shot");
            _in.MoveTo(Ed.ViewScreenPoint());
            yield return null;
            _in.Scroll(1f);
            yield return Frames(3);
            Assert.That(Ed.CameraFollowsShots, Is.False, "moving it (the wheel) stops it following");
            var parked = Cam.position;
            yield return _in.Tap(Key.RightArrow);
            Assert.That(Vector3.Distance(Cam.position, parked), Is.LessThan(1e-4f), "a moved free camera stays where you put it");

            float len = Ed.Opening.Length;
            Assert.That(Ed.TryTrackScreenPoint(len * 0.1f, out var a), Is.True, "the scrub bar");
            Assert.That(Ed.TryTrackScreenPoint(len * 0.5f, out var b), Is.True);
            yield return _in.DragTo(a, b, 8);
            Assert.That(Ed.Opening.Time, Is.EqualTo(len * 0.5f).Within(len * 0.02f), "dragging along the bar sets the time");
            Assert.That(Ed.Opening.Paused, Is.True);
        }

        // ---------- Shots ----------

        [UnityTest]
        public IEnumerator SetFromMyViewChangesOnlyThatShot()
        {
            yield return OpenWithF9();
            yield return ClickListRow("shot:table");
            Assert.That(Ed.Selected != null && Ed.Selected.id == "shot:table", Is.True, "the title table's shot");
            var stage = Ed.Stage;
            int i = stage.PoseIndex("table");
            var before = new Vector3[stage.posePositions.Count];
            for (int k = 0; k < before.Length; k++) before[k] = stage.posePositions[k];
            yield return _in.Click(Point("button:look-through"));
            Assert.That(Vector3.Distance(Cam.position, stage.posePositions[i]), Is.LessThan(1e-3f), "'Look through it' puts the camera at the shot");

            _in.MoveTo(Ed.ViewScreenPoint());
            yield return null;
            _in.SetButton(MouseButton.Right, true);
            yield return Frames(2);
            yield return HoldKeyFor(Key.D, 0.3f);
            _in.SetButton(MouseButton.Right, false);
            yield return Frames(2);
            Vector3 view = Cam.position;
            Assert.That(Vector3.Distance(view, before[i]), Is.GreaterThan(0.05f), "flew away from it");

            yield return _in.Click(Point("button:set-from-view"));
            Assert.That(Vector3.Distance(stage.posePositions[i], view), Is.LessThan(1e-3f), "the shot films from the view now");
            for (int k = 0; k < before.Length; k++)
                if (k != i) Assert.That(Vector3.Distance(stage.posePositions[k], before[k]), Is.LessThan(1e-5f), "shot " + stage.poseNames[k] + " didn't change");
            Assert.That(Ed.StatusText, Does.Contain("Only this shot changed"));
        }

        // ---------- The phone in her hand ----------

        /// <summary>The creator: "I can't seem to move the phone when it's held in front of Nadia." While she holds it, the
        /// phone is where the held pose puts it every frame; the editable is the held pose itself ("phone-held").</summary>
        [UnityTest]
        public IEnumerator TheHeldPhoneMovesWhereSheHoldsIt()
        {
            yield return OpenWithF9();
            yield return _in.Click(Point("beat:Holding it: the voicemail"));
            var stage = Ed.Stage;
            Assert.That(stage.PhoneInHand, Is.True, "she holds the phone up at this beat");
            yield return _in.Tap(Key.L);

            // In the view: a click on the phone in her hand selects the held phone.
            Assert.That(Ed.TryThingScreenPoint("phone-held", out var p), Is.True, "the phone in her hand shows in the view");
            yield return _in.Click(p);
            Assert.That(Ed.Selected != null && Ed.Selected.id == "phone-held", Is.True, "a click on it selects the held phone (" + Ed.LastPress + ")");
            Assert.That(Ed.StatusText, Does.Contain("held in her hand"), "the status line says which phone");
            var held = Ed.Selected.target;
            var phone = stage.phone;

            // An arrow moves it, and the phone in her hand follows (or the status line says her reach stops it).
            Vector3 handle0 = held.position, phone0 = phone.position;
            Assert.That(Ed.TryHandleScreenPoint(0, out var x), Is.True, "its X arrow shows");
            var cam = stage.cam;
            Vector2 dir = ((Vector2)cam.WorldToScreenPoint(handle0 + Vector3.right * 0.03f) - (Vector2)cam.WorldToScreenPoint(handle0)).normalized;
            yield return _in.DragTo(x, x + dir * 50f, 8);
            yield return Frames(2);
            Vector3 d = held.position - handle0;
            Assert.That(d.x, Is.GreaterThan(0.004f), "the X arrow moved where she holds it (" + Ed.LastPress + ")");
            Assert.That(Mathf.Abs(d.y) + Mathf.Abs(d.z), Is.LessThan(1e-4f), "only along X");
            bool follows = Vector3.Distance(phone.position, held.position) < 1e-3f;
            Assert.That(follows || (stage.HoldClamped && Ed.StatusText.Contains("can't reach")), Is.True,
                "the phone in her hand follows, or the status line says her reach stops it (" + Ed.StatusText + ")");
            Assert.That(Vector3.Distance(phone.position, phone0) > 0.001f || Ed.StatusText.Contains("can't reach"), Is.True, "never a drag that silently does nothing");

            // A typed number.
            yield return _in.Click(Point("field:pos.z"));
            _in.Type("23");
            yield return Frames(2);
            yield return _in.Tap(Key.Enter);
            Assert.That(held.localPosition.z, Is.EqualTo(0.23f).Within(1e-4f), "23 cm in front of her eyes");

            // It stays there while the opening plays through the beat.
            Vector3 set = held.position;
            yield return _in.Tap(Key.Space);
            Assert.That(Ed.Playing, Is.True);
            float end = Time.realtimeSinceStartup + 0.4f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.That(stage.PhoneInHand, Is.True, "still holding it");
            Assert.That(Vector3.Distance(held.position, set), Is.LessThan(1e-4f), "the held place isn't overwritten while it plays");
            Assert.That(Vector3.Distance(phone.position, held.position) < 1e-3f || stage.HoldClamped, Is.True, "the phone is there while it plays");
            yield return _in.Tap(Key.Space);

            // Saved.
            yield return _in.Tap(Key.LeftCtrl, Key.S);
            Assert.That(File.Exists(_file), Is.True);
            Assert.That(VhSceneEditsData.FromJson(File.ReadAllText(_file)).Object("phone-held"), Is.Not.Null, "the held place is saved");

            // The list: the resting phone goes to where it rests, the held one to where she holds it.
            yield return _in.Tap(Key.Escape);
            yield return ClickListRow("phone");
            Assert.That(Ed.Selected != null && Ed.Selected.id == "phone", Is.True, "the resting phone, from the list");
            Assert.That(stage.PhoneInHand, Is.False, "it jumped to a beat where the phone rests");
            Assert.That(Ed.StatusText, Does.Contain("resting"));
            yield return ClickListRow("phone-held");
            Assert.That(Ed.Selected != null && Ed.Selected.id == "phone-held", Is.True, "the held phone, from the list");
            Assert.That(stage.PhoneInHand, Is.True, "it jumped to a beat where she holds it");

            // Built again with the save: she holds it there.
            yield return _in.Tap(Key.F9);
            var runner = GameRoot.Instance.GetComponent<VhTitleRunner>();
            runner.DestroyStage();
            Assert.That(runner.TryBuildStage(), Is.True);
            yield return Frames(2);
            Assert.That(runner.stage.heldPhone.localPosition.z, Is.EqualTo(0.23f).Within(1e-4f), "the saved held place applies when the sets are built");
        }

        // ---------- Saving ----------

        [UnityTest]
        public IEnumerator CtrlSSavesAndTheSetsBuildWithTheSave()
        {
            yield return OpenWithF9();
            yield return ClickThing("title-tablet");
            var t = Ed.Selected.target;
            Vector3 start = t.position;
            Assert.That(Ed.TryHandleScreenPoint(0, out var x), Is.True);
            var cam = Ed.Stage.cam;
            Vector2 dir = ((Vector2)cam.WorldToScreenPoint(start + Vector3.right * 0.1f) - (Vector2)cam.WorldToScreenPoint(start)).normalized;
            yield return _in.DragTo(x, x + dir * 60f, 8);
            Vector3 moved = t.position;
            Assert.That(moved.x - start.x, Is.GreaterThan(0.005f), "the drag moved it (" + Ed.LastPress + ")");
            yield return _in.Tap(Key.LeftCtrl, Key.S);
            Assert.That(Ed.Dirty, Is.False, "saved");
            Assert.That(Ed.StatusText, Does.Contain("Saved 1 change"));
            Assert.That(File.Exists(_file), Is.True, "the file is written");
            var data = VhSceneEditsData.FromJson(File.ReadAllText(_file));
            Assert.That(data.Object("title-tablet"), Is.Not.Null, "with the tablet in it");

            yield return _in.Tap(Key.F9);
            var runner = GameRoot.Instance.GetComponent<VhTitleRunner>();
            runner.DestroyStage();
            Assert.That(runner.TryBuildStage(), Is.True, "the sets build again");
            yield return Frames(2);
            var again = runner.stage.edits.Find("title-tablet");
            Assert.That(Vector3.Distance(again.target.position, moved), Is.LessThan(1e-3f), "with the saved move");
        }
    }
}
