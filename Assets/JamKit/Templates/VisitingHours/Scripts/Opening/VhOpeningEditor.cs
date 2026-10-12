using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The opening editor (docs/OPENING-EDITOR.md), in the build as in the Unity editor: F9 (or the presenter panel's
    /// "Edit the opening", or the -edit-opening flag) pauses the opening and lets the creator scrub to any beat, pick a
    /// thing (in the view or from the list, by plain names), move, turn or size it with handles or numbers, fly a free
    /// camera, set each shot's camera from that view, undo, reset, save, and play on from any point to check. What moves
    /// is <see cref="VhSceneEdits"/>; the saved overrides are applied by the builder every time it builds the sets.
    ///
    /// Input: the editor reads the mouse and keyboard straight from the Input System every frame (the shell's UI input
    /// module only carries the left button, so the right and middle buttons never reach UI Toolkit). Its panels are
    /// hit-tested here too; one full-screen element under them stops the shell's own pointer events from reaching the
    /// title behind. Files: this one (state, input, the camera, edits), VhOpeningEditorUi.cs (the panels and their
    /// controls), VhOpeningEditorView.cs (picking, outlines and handles in the view).
    /// </summary>
    public sealed partial class VhOpeningEditor : MonoBehaviour
    {
        public enum Tool { Move, Rotate, Scale }

        enum Drag { None, Ui, Look, Pan, Orbit, Handle, Scrub, Number, ListScroll }

        struct Beat
        {
            public string name;
            public float time;
        }

        struct UndoStep
        {
            public string json;
            public string what;
        }

        static VhOpeningEditor _instance;
        /// <summary>Closing saves unsaved edits (tests turn it off so nothing is written).</summary>
        public static bool SaveOnClose = true;
        public static VhOpeningEditor Instance => _instance;

        /// <summary>Starts the game straight in the editor ("Edit the opening.cmd" next to the exe passes it).</summary>
        public const string LaunchFlag = "-edit-opening";

        /// <summary>Mouse-look: degrees per pixel of mouse movement while the right button is held.</summary>
        public const float LookDegreesPerPixel = 0.15f;
        /// <summary>Alt + left drag: degrees per pixel of orbit.</summary>
        public const float OrbitDegreesPerPixel = 0.3f;
        /// <summary>The handles' length on screen, in the panel's pixels (1920 x 1080), whatever the distance.</summary>
        public const float HandlePixels = 120f;
        public const float DefaultFlySpeed = 1.5f;
        const float SnapMove = 0.01f, SnapTurn = 5f, SnapSize = 0.05f;

        VhTitleRunner _runner;
        VhStage _stage;
        VhOpening _opening;
        bool _open, _openWhenReady, _playing, _free = true, _snap = true, _dirty, _world, _workLight, _panelsHidden;
        float _flyHeld;
        /// <summary>The free camera sits where a shot put it and hasn't been moved since: it follows the shots as you
        /// jump and scrub (so each beat shows its shot), until you move it.</summary>
        bool _camAuto;
        string _dropped;
        Tool _tool = Tool.Move;
        VhEditable _sel, _hover, _throughShot;
        readonly List<Beat> _beats = new List<Beat>();
        readonly List<UndoStep> _undo = new List<UndoStep>(), _redo = new List<UndoStep>();

        // The free camera (roll is kept from a shot looked through, so "Set from my view" keeps it).
        Vector3 _camPos;
        float _yaw, _pitch, _roll, _fov = 55f, _flySpeed = DefaultFlySpeed, _pivotDist = 1.5f;

        // The pointer.
        Drag _drag;
        Vector2 _screen, _mouse, _pressMouse;
        bool _mouseSeen, _cursorLocked;
        Vector2 _lockedAt;
        float _lastPick = -1f;
        Vector2 _lastPickMouse = new Vector2(-1f, -1f);

        // A drag on a handle.
        int _dragHandle = -1, _hoverHandle = -1;
        Vector3 _dragPivot, _dragAxis, _dragRing, _dragPlane0, _dragScale0;
        Matrix4x4 _dragWorld0;
        Vector2 _dragMouse0;
        float _dragT0, _dragLength0, _dragAmount;
        bool _dragParallel;

        // Typing into a number box.
        Keyboard _textKb;
        readonly List<char> _typed = new List<char>(16);

        public bool IsOpen => _open;
        public VhEditable Selected => _sel;
        public VhStage Stage => _stage;
        public VhOpening Opening => _opening;
        /// <summary>The free camera (true) or the opening's own shot camera (false).</summary>
        public bool FreeCamera => _free;
        public Tool CurrentTool => _tool;
        public bool Snap => _snap;
        /// <summary>Changes not saved yet.</summary>
        public bool Dirty => _dirty;
        public bool Playing => _playing;
        public float FlySpeed => _flySpeed;
        /// <summary>What the last left press hit (a control, a panel, a handle, a thing in the view): for test messages.</summary>
        public string LastPress => _lastPress;
        string _lastPress = "";
        /// <summary>The work light (L) is on: an even light so the set shows at dark moments (only while editing).</summary>
        public bool WorkLight => _workLight;
        /// <summary>Tab hid the panels (a clean view).</summary>
        public bool PanelsHidden => _panelsHidden;
        /// <summary>The free camera follows the shots (it hasn't been moved since a shot placed it).</summary>
        public bool CameraFollowsShots => _free && _camAuto;
        /// <summary>What the status line says (what just happened).</summary>
        public string StatusText => _statusText;
        /// <summary>The thing under the mouse in the view (what a click would select), or null.</summary>
        public VhEditable Hovered => _hover;
        public IReadOnlyList<string> BeatNames
        {
            get
            {
                var n = new List<string>();
                foreach (var b in _beats) n.Add(b.name);
                return n;
            }
        }

        void Awake()
        {
            _instance = this;
            ApplyLaunchArgs(System.Environment.GetCommandLineArgs());
        }

        /// <summary>With <see cref="LaunchFlag"/> among <paramref name="args"/>: opens the editor as soon as the title's
        /// set is up (as the presenter panel's entry does). True when it's there.</summary>
        public bool ApplyLaunchArgs(string[] args)
        {
            if (args == null || System.Array.IndexOf(args, LaunchFlag) < 0) return false;
            _openWhenReady = true;
            return true;
        }

        void OnDestroy()
        {
            UnhookText();
            UnlockCursor();
            if (_instance == this) _instance = null;
        }

        // ---------- Opening and closing ----------

        /// <summary>The presenter panel's "Edit the opening": from anywhere (it goes to the title first).</summary>
        public static void OpenFromAnywhere()
        {
            var ed = Instance;
            if (ed == null || !GameRoot.Exists) return;
            if (GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene && VhBoot.Stage != null)
            {
                ed.Open();
                return;
            }
            ed._openWhenReady = true;
            VhBoot.PresenterOpening();
        }

        /// <summary>Opens the editor on the opening (paused where it is, or on the title if it's over). False when there's no set here.</summary>
        public bool Open()
        {
            if (_open) return true;
            _runner = GetComponent<VhTitleRunner>();
            if (_runner == null || !GameRoot.Exists) return false;
            if (_runner.stage == null && !_runner.TryBuildStage()) return false;
            _stage = _runner.stage;
            if (_stage.edits == null) return false;
            bool fresh = _runner.opening == null;
            if (fresh) _runner.PlayOpening(1f);
            _opening = _runner.opening;
            if (_opening == null) return false;
            _open = true;
            _opening.Paused = true;
            _opening.blackScale = 0.2f;
            foreach (var a in _stage.Sounds()) a.Pause();
            _undo.Clear();
            _redo.Clear();
            _dirty = false;
            _playing = false;
            _drag = Drag.None;
            _sel = _hover = _throughShot = null;
            BuildBeats();
            BuildPickables();
            BuildUi();
            GrabCameraFromStage();
            if (fresh) SeekTo(TitleTime());
            SetFree(true, false);
            Select((VhEditable)null, false);
            RefreshChanges();
            Status("Editing the opening. Click a thing to select it; hold the right mouse button and use W A S D to fly. H for help, F9 closes (and saves).");
            return true;
        }

        /// <summary>Closes the editor: saves unsaved edits, then the opening plays on from where it is (or the title, at its end).</summary>
        public void Close()
        {
            if (!_open) return;
            if (_editing != null) CommitNumber();
            if (_drag == Drag.Handle) EndHandleDrag(true);
            if (_dirty && SaveOnClose) Save();
            _open = false;
            _drag = Drag.None;
            UnlockCursor();
            UnhookText();
            RemoveUi();
            if (_stage != null)
            {
                _stage.editorCamera = false;
                _stage.workLight = 0f;
            }
            if (_opening != null && _opening.Running)
            {
                _opening.blackScale = 1f;
                if (_opening.Time >= TitleTime() - 0.01f) _opening.Finish(false);
                else _opening.PlayFromHere();
            }
            else if (_stage != null) _stage.ShowTitleFinal();
            _opening = null;
        }

        void Update()
        {
            if (!GameRoot.Exists) return;
            var kb = Keyboard.current;
            bool onTitle = GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene;
            var run = _runner != null ? _runner : GetComponent<VhTitleRunner>();
            // (Once the title's own opening, if one is due, has started: the editor holds that one.)
            if (_openWhenReady && onTitle && VhBoot.Stage != null && run != null && !run.openingWanted && !run.openingOnTitle)
            {
                _openWhenReady = false;
                Open();
                return;
            }
            if (!_open)
            {
                if (kb != null && kb.f9Key.wasPressedThisFrame && onTitle && (VhBoot.IsActive || VhBoot.Stage != null)) Open();
                return;
            }
            if (_runner == null || _runner.stage != _stage || _stage == null || !onTitle)
            {
                // The set went (the title left, a rebuild from outside): close quietly.
                _open = false;
                _drag = Drag.None;
                UnlockCursor();
                UnhookText();
                RemoveUi();
                return;
            }
            if (_opening == null || !_opening.Running) Rebind();
            if (_stage.table != null && _stage.table.interactive) _stage.SetInteractive(false);

            // An overlay on top (Settings, the presenter panel): the editor steps aside until it closes.
            var top = GameRoot.Ui.Top;
            bool covered = top != null && top != GameRoot.Ui.Title;
            if (_root != null) _root.style.display = covered ? DisplayStyle.None : DisplayStyle.Flex;
            if (covered)
            {
                if (_drag == Drag.Handle) EndHandleDrag(true);
                _drag = Drag.None;
                UnlockCursor();
                ApplyFreeCamera();
                return;
            }
            // The editor is driven with the mouse: a pad touched by accident mustn't hide the cursor.
            if (GameRoot.Input.PadMode) GameRoot.Input.SetPadMode(false);

            HookText(kb);
            Keys(kb);
            if (!_open) return;
            Pointer(Mouse.current, kb);
            _typed.Clear();
            Fly(kb);
            ApplyFreeCamera();
            _stage.workLight = _workLight ? 1f : 0f;
            if (_playing && _opening != null && !_opening.Running) _playing = false;
            RefreshUi();
            PlaceTags();
            _view?.MarkDirtyRepaint();
            _track?.MarkDirtyRepaint();
        }

        /// <summary>The opening ended (played through): a fresh one, held at the title, so the timeline still works.</summary>
        void Rebind()
        {
            _runner.PlayOpening(1f);
            _opening = _runner.opening;
            if (_opening == null) return;
            _opening.Paused = true;
            _opening.blackScale = 0.2f;
            _playing = false;
            bool tableBefore = _stage.Pose == "table";
            _opening.Seek(TitleTime());
            if (_free && tableBefore != (_stage.Pose == "table")) CameraToCurrentShot();
        }

        // ---------- The timeline ----------

        void BuildBeats()
        {
            _beats.Clear();
            var cues = _opening.Cues;
            OpeningCue Find(OpeningCueKind k, string text = null, float value = -1f)
            {
                foreach (var c in cues)
                    if (c.kind == k && (text == null || c.text == text) && (value < 0f || Mathf.Abs(c.value - value) < 0.01f)) return c;
                return null;
            }
            void Add(string name, OpeningCue c, float after)
            {
                if (c != null) _beats.Add(new Beat { name = name, time = c.at + after });
            }
            Add("The room, from her pillow", Find(OpeningCueKind.Fade, "in"), 1.2f);
            Add("The phone on the nightstand", Find(OpeningCueKind.Camera, "nightstand"), 1.6f);
            Add("She reaches", Find(OpeningCueKind.Arm, "reach"), 0.8f);
            Add("The grab", Find(OpeningCueKind.Arm, "grab"), 0.2f);
            Add("Holding it: the voicemail", Find(OpeningCueKind.Arm, "tap"), 0.5f);
            Add("She puts it down", Find(OpeningCueKind.Arm, "drop"), 0.3f);
            Add("Falling asleep", Find(OpeningCueKind.VoiceLevel, null, 0f), 1.2f);
            Add("The phone from above", Find(OpeningCueKind.Camera, "overhead"), 0.8f);
            Add("G'night", Find(OpeningCueKind.Lights, null, 0f), 0.5f);
            Add("The news report", Find(OpeningCueKind.Voice, "news"), 0.6f);
            Add("The pieces arrive", Find(OpeningCueKind.Show, "mug"), 1.0f);
            _beats.Add(new Beat { name = "The title", time = TitleTime() });
            _beats.Sort((a, b) => a.time.CompareTo(b.time));
        }

        float TitleTime()
        {
            if (_opening == null) return 0f;
            foreach (var c in _opening.Cues)
                if (c.kind == OpeningCueKind.Interactive) return c.at + 0.05f;
            return _opening.Length;
        }

        int BeatIndexAt(float t)
        {
            int i = -1;
            for (int k = 0; k < _beats.Count; k++)
                if (_beats[k].time <= t + 0.01f) i = k;
            return i;
        }

        string BeatAt(float t)
        {
            int i = BeatIndexAt(t);
            return i >= 0 ? _beats[i].name : "Before the first beat";
        }

        static string Clock(float t) => ((int)(t / 60f)).ToString("0") + ":" + (t % 60f).ToString("00.0");

        /// <summary>Jumps the opening to <paramref name="t"/> seconds, paused. When that's in the other set (the bedroom or
        /// the title table), the free camera goes to that moment's shot so there's something to see.</summary>
        public void SeekTo(float t)
        {
            if (_opening == null || !_opening.Running) Rebind();
            if (_opening == null) return;
            bool tableBefore = _stage.Pose == "table";
            _playing = false;
            _opening.Paused = true;
            _opening.blackScale = 0.2f;
            _opening.Seek(Mathf.Clamp(t, 0f, _opening.Length));
            _setMoved = tableBefore != (_stage.Pose == "table");
            if (_free && (_setMoved || _camAuto)) CameraToCurrentShot();
            // A selection in the set that's gone from view now: let go of it (the panel would show a thing you can't see).
            _dropped = null;
            if (_setMoved && _sel != null && _sel.target != null && !_sel.target.gameObject.activeInHierarchy)
            {
                _dropped = Short(_sel);
                _sel = null;
                RefreshSelectionPanel();
            }
            SyncPhone();
            if (!_free) _stage.RefreshPose();
            ShowFields();
        }

        bool _setMoved;
        string _phoneSwap;

        /// <summary>The phone selected, but the other phone is the one in use at this moment (she holds it, or it rests):
        /// select that one, so a drag always moves what you see.</summary>
        void SyncPhone()
        {
            _phoneSwap = null;
            if (_sel == null || _stage == null || _stage.edits == null) return;
            string want = _sel.id == "phone" && _stage.PhoneInHand ? "phone-held" : _sel.id == "phone-held" && !_stage.PhoneInHand ? "phone" : null;
            var other = want != null ? _stage.edits.Find(want) : null;
            if (other == null) return;
            _sel = other;
            _phoneSwap = want == "phone-held" ? "She holds the phone at this moment: now editing The phone (held in her hand)." : "The phone rests on the nightstand at this moment: now editing The phone (resting).";
            RefreshSelectionPanel();
        }

        /// <summary>The held phone was pulled back to her reach: say so (the handle shows where you put it).</summary>
        string ReachNote() => _sel != null && _sel.id == "phone-held" && _stage.HoldClamped
            ? " Her arm can't reach that far: the phone stops at her reach (the arrows show where you put it)."
            : "";

        /// <summary>What a jump to the other set did, for the status line.</summary>
        string SetNote()
        {
            if (!_setMoved) return _phoneSwap != null ? " " + _phoneSwap : "";
            bool bed = _stage.Pose != "table";
            string s = " The camera went to " + (bed ? "the bedroom." : "the title table.");
            if (_dropped != null) s += " (" + _dropped + " isn't in this set: nothing is selected now.)";
            if (bed && !_workLight) s += " Too dark to see? L turns on a work light (only while editing).";
            if (_phoneSwap != null) s += " " + _phoneSwap;
            return s;
        }

        /// <summary>Jumps to a beat by its name (see <see cref="BeatNames"/>); false if there's none.</summary>
        public bool SeekBeat(string name)
        {
            foreach (var b in _beats)
                if (b.name == name)
                {
                    SeekTo(b.time);
                    return true;
                }
            return false;
        }

        void GoToBeat(int i)
        {
            if (i < 0 || i >= _beats.Count) return;
            SeekTo(_beats[i].time);
            Status("At \"" + _beats[i].name + "\" (" + Clock(_beats[i].time) + ")." + SetNote());
        }

        /// <summary>The time a beat starts at (0 when there's no such beat).</summary>
        public float BeatTime(string name)
        {
            foreach (var b in _beats)
                if (b.name == name) return b.time;
            return 0f;
        }

        void StepBeat(int dir)
        {
            float t = _opening != null ? _opening.Time : 0f;
            if (dir > 0)
            {
                for (int i = 0; i < _beats.Count; i++)
                    if (_beats[i].time > t + 0.05f)
                    {
                        GoToBeat(i);
                        return;
                    }
                Status("That's the last beat (the title).");
            }
            else
            {
                for (int i = _beats.Count - 1; i >= 0; i--)
                    if (_beats[i].time < t - 0.05f)
                    {
                        GoToBeat(i);
                        return;
                    }
                Status("That's the first beat.");
            }
        }

        void TogglePlay()
        {
            if (_opening == null) return;
            if (_playing)
            {
                _playing = false;
                _opening.Paused = true;
                _opening.blackScale = 0.2f;
                foreach (var a in _stage.Sounds()) a.Pause();
                SyncPhone();
                Status("Paused at " + Clock(_opening.Time) + " (" + BeatAt(_opening.Time) + "). Space plays on." + (_phoneSwap != null ? " " + _phoneSwap : ""));
                return;
            }
            if (_drag == Drag.Handle) EndHandleDrag(true);
            if (_opening.Time >= _opening.Length - 0.05f) SeekTo(0f);
            _playing = true;
            SetFree(false, false);
            _opening.blackScale = 1f; // as it plays: the real blacks
            _opening.PlayFromHere();
            Status("Playing from " + Clock(_opening.Time) + " through the shot cameras. Space pauses; C flies the free camera.");
        }

        // ---------- Selecting ----------

        /// <summary>Selects a thing by its id (null: nothing).</summary>
        public bool Select(string id)
        {
            var e = id != null ? _stage?.edits?.Find(id) : null;
            Select(e, false);
            return e != null || id == null;
        }

        /// <summary>Selects <paramref name="e"/> (a thing in the other set: the opening goes there first). The status line
        /// says what's selected and what to do with it when <paramref name="say"/>.</summary>
        void Select(VhEditable e, bool say)
        {
            if (_editing != null) CommitNumber();
            bool changed = e != _sel;
            _sel = e;
            if (e != _throughShot) _throughShot = null;
            string moved = "";
            if (e != null && _stage != null && !_playing)
            {
                bool atTable = _stage.Pose == "table";
                if (e.id == "phone-held" && (atTable || !_stage.PhoneInHand))
                {
                    SeekTo(BeatTime("Holding it: the voicemail"));
                    moved = " (Jumped to \"Holding it: the voicemail\", where she holds it.)";
                }
                else if (e.id == "phone" && (atTable || _stage.PhoneInHand))
                {
                    SeekTo(BeatTime("The phone on the nightstand"));
                    moved = " (Jumped to \"The phone on the nightstand\", where it rests.)";
                }
                else if (e.bedroom == atTable)
                {
                    float t = e.bedroom ? BeatTime("The phone on the nightstand") : TitleTime();
                    SeekTo(t);
                    moved = " (It's " + (e.bedroom ? "in the bedroom: jumped to \"The phone on the nightstand\"." : "at the title table: jumped to the title.") + ")";
                }
            }
            RefreshSelectionPanel();
            if (!say) return;
            if (e == null) Status(changed ? "Nothing selected." : "Nothing to select there.");
            else Status("Selected " + e.label + ". " + ToolHint(e) + moved);
        }

        string ToolHint(VhEditable e)
        {
            switch (_tool)
            {
                case Tool.Rotate: return "Drag a ring to turn it, or type numbers on the right.";
                case Tool.Scale:
                    return e.canScale && !e.isShot ? "Drag a box to size it (the middle one sizes it all), or type numbers." : "It can't be sized: W moves it, E turns it.";
                default: return "Drag an arrow to move it along that axis (the middle square moves it across the view), or type numbers.";
            }
        }

        static string Short(VhEditable e)
        {
            if (e == null) return "";
            string s = e.label;
            int i = s.IndexOf(" (", System.StringComparison.Ordinal);
            return i > 0 ? s.Substring(0, i) : s;
        }

        // ---------- Edits, undo, save ----------

        /// <summary>Moves the selected thing by a world move (and what it carries); one undo step.</summary>
        public void MoveSelected(Matrix4x4 worldDelta)
        {
            if (_sel == null) return;
            PushUndo("moved " + Short(_sel));
            _stage.edits.MoveBy(_sel, worldDelta);
            Finished();
        }

        /// <summary>After any change: the blanket reshapes if she moved, the numbers and the list of changes follow.</summary>
        void Finished()
        {
            if (_stage.blanketDirty) VhStageBuilder.ReshapeBlanket(_stage);
            _stage.blanketDirty = false;
            _dirty = true;
            ShowFields();
            RefreshChanges();
        }

        void PushUndo(string what)
        {
            _undo.Add(new UndoStep { json = _stage.edits.Capture().ToJson(), what = what });
            if (_undo.Count > 200) _undo.RemoveAt(0);
            _redo.Clear();
        }

        /// <summary>Undoes the last change (true if there was one).</summary>
        public bool Undo() => StepUndo(_undo, _redo, "Undid");

        public bool Redo() => StepUndo(_redo, _undo, "Redid");

        bool StepUndo(List<UndoStep> from, List<UndoStep> to, string said)
        {
            if (from.Count == 0)
            {
                Status(said == "Undid" ? "Nothing to undo." : "Nothing to redo.");
                return false;
            }
            var step = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(new UndoStep { json = _stage.edits.Capture().ToJson(), what = step.what });
            _stage.edits.ApplyAll(VhSceneEditsData.FromJson(step.json));
            _stage.blanketDirty = false;
            _dirty = true;
            ShowFields();
            RefreshChanges();
            Status(said + ": " + (string.IsNullOrEmpty(step.what) ? "the last change" : step.what) + ".");
            return true;
        }

        void ResetSelected()
        {
            if (_sel == null) return;
            ResetOne(_sel);
        }

        void ResetOne(VhEditable e)
        {
            PushUndo("reset " + Short(e));
            _stage.edits.Reset(e);
            Finished();
            Status(e.label + " is back where it was built (Ctrl+Z undoes).");
        }

        void ResetEverything()
        {
            PushUndo("reset everything");
            _stage.edits.ApplyAll(new VhSceneEditsData());
            _stage.blanketDirty = false;
            _dirty = true;
            ShowFields();
            RefreshChanges();
            Status("Everything is back where it was built (Ctrl+Z undoes).");
        }

        /// <summary>Saves the edits; the status line says where.</summary>
        public void Save()
        {
            var data = _stage.edits.Capture();
            var wrote = VhSceneEdits.Save(data);
            if (wrote.Count == 0)
            {
                Status("Couldn't save (the log says why). Your changes are still here.");
                return;
            }
            _dirty = false;
            int n = data.objects.Count + data.shots.Count;
            Status("Saved " + (n == 0 ? "(nothing differs from the built scene)" : n + (n == 1 ? " change" : " changes")) + " to " + string.Join("  and  ", wrote));
        }

        void Rebuild()
        {
            var keep = _stage.edits.Capture();
            float t = _opening != null ? _opening.Time : 0f;
            string sel = _sel != null ? _sel.id : null;
            var prev = VhSceneEdits.Source;
            VhSceneEdits.Source = () => keep;
            try
            {
                _runner.DestroyStage();
                _runner.TryBuildStage();
            }
            finally
            {
                VhSceneEdits.Source = prev;
            }
            _stage = _runner.stage;
            _renderers.Clear();
            if (_stage == null || _root == null) return;
            _runner.PlayOpening(1f);
            _opening = _runner.opening;
            _opening.Paused = true;
            _opening.blackScale = 0.2f;
            BuildBeats();
            BuildPickables();
            BuildList();
            BuildBeatChips();
            SeekTo(t);
            SetFree(_free, false);
            _sel = null;
            Select(sel != null ? _stage.edits.Find(sel) : null, false);
            RefreshChanges();
            Status("Rebuilt the sets with your edits (the lighting and the blinds' stripes worked out again).");
        }

        // ---------- The camera ----------

        Quaternion CamRot => Quaternion.Euler(_pitch, _yaw, _roll);

        void SetRot(Quaternion q)
        {
            var e = q.eulerAngles;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
            _yaw = e.y;
            _roll = e.z > 180f ? e.z - 360f : e.z;
        }

        void GrabCameraFromStage()
        {
            if (_stage == null || _stage.cam == null) return;
            var c = _stage.cam.transform;
            _camPos = c.position;
            SetRot(c.rotation);
            _fov = _stage.cam.fieldOfView;
            _camAuto = true;
        }

        /// <summary>The free camera jumps to the shot the opening is on now (after a jump to the other set).</summary>
        void CameraToCurrentShot()
        {
            int i = _stage.PoseIndex(_stage.Pose);
            if (i < 0) return;
            _camPos = _stage.posePositions[i];
            SetRot(_stage.poseRotations[i]);
            _fov = _stage.poseFov[i];
            _throughShot = null;
            _camAuto = true;
        }

        void SetFree(bool free, bool say)
        {
            if (free && !_free) GrabCameraFromStage();
            _free = free;
            _stage.editorCamera = free;
            if (!free)
            {
                _throughShot = null;
                _stage.RefreshPose();
            }
            ApplyFreeCamera();
            if (!say) return;
            Status(free
                ? "Free camera: hold the right mouse button and use W A S D (Q E down and up) to fly; the wheel moves in and out."
                : "Shot camera: you see what the opening shows at this moment. C goes back to the free camera.");
        }

        /// <summary>Any camera move while on the shot camera switches to the free camera, starting from the same view.</summary>
        void TakeCamera()
        {
            if (_free) return;
            SetFree(true, false);
        }

        void ApplyFreeCamera()
        {
            if (!_free || _stage == null || _stage.cam == null) return;
            _stage.editorCamera = true;
            _stage.cam.transform.SetPositionAndRotation(_camPos, CamRot);
            _stage.cam.fieldOfView = _fov;
        }

        void LockCursor()
        {
            if (_cursorLocked) return;
            _cursorLocked = true;
            _lockedAt = _screen;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        void UnlockCursor()
        {
            if (!_cursorLocked) return;
            _cursorLocked = false;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            // Back where the drag started (as the Scene view does), not the middle of the window.
            var m = Mouse.current;
            if (m != null && !Application.isBatchMode) m.WarpCursorPosition(_lockedAt);
        }

        void Fly(Keyboard kb)
        {
            if (_drag != Drag.Look || kb == null) return;
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += Vector3.forward;
            if (kb.sKey.isPressed) move += Vector3.back;
            if (kb.aKey.isPressed) move += Vector3.left;
            if (kb.dKey.isPressed) move += Vector3.right;
            if (kb.eKey.isPressed) move += Vector3.up;
            if (kb.qKey.isPressed) move += Vector3.down;
            if (move == Vector3.zero)
            {
                _flyHeld = 0f;
                return;
            }
            // Held over a second, it speeds up (to 3x after three seconds): short taps stay precise, long holds cross the room.
            _flyHeld += Time.unscaledDeltaTime;
            float speed = _flySpeed * (kb.shiftKey.isPressed ? 4f : 1f) * (1f + 2f * Mathf.Clamp01((_flyHeld - 1f) / 2f));
            _camAuto = false;
            // Up and down are the world's (Q E), the rest the view's.
            Vector3 world = CamRot * new Vector3(move.x, 0f, move.z) + Vector3.up * move.y;
            _camPos += world * (speed * Time.unscaledDeltaTime);
        }

        Vector3 OrbitPivot()
        {
            var fwd = CamRot * Vector3.forward;
            if (_sel != null && _sel.target != null && _sel.target.gameObject.activeInHierarchy)
            {
                Vector3 c = BoundsOf(_sel).center;
                if (Vector3.Dot(c - _camPos, fwd) > 0.05f) return c;
            }
            return _camPos + fwd * _pivotDist;
        }

        void Look(Vector2 d)
        {
            if (d.sqrMagnitude > 0f) _camAuto = false;
            _yaw += d.x * LookDegreesPerPixel;
            _pitch = Mathf.Clamp(_pitch - d.y * LookDegreesPerPixel, -89f, 89f);
        }

        void Orbit(Vector2 d)
        {
            if (d.sqrMagnitude > 0f) _camAuto = false;
            Vector3 pivot = _orbitPivot;
            var rot = CamRot;
            var right = rot * Vector3.right;
            float dPitch = -d.y * OrbitDegreesPerPixel;
            if (_pitch + dPitch > 89f || _pitch + dPitch < -89f) dPitch = 0f;
            var q = Quaternion.AngleAxis(d.x * OrbitDegreesPerPixel, Vector3.up) * Quaternion.AngleAxis(dPitch, right);
            _camPos = pivot + q * (_camPos - pivot);
            SetRot(q * rot);
        }

        Vector3 _orbitPivot;

        /// <summary>World size of one screen pixel at <paramref name="distance"/> in front of the camera.</summary>
        float WorldPerScreenPixel(float distance) =>
            2f * Mathf.Max(0.01f, distance) * Mathf.Tan(_fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, Screen.height);

        void Pan(Vector2 d)
        {
            if (d.sqrMagnitude > 0f) _camAuto = false;
            var rot = CamRot;
            float dist = Vector3.Dot(OrbitPivot() - _camPos, rot * Vector3.forward);
            float k = WorldPerScreenPixel(dist);
            // Grab the view: what's under the mouse follows it.
            _camPos -= (rot * Vector3.right) * (d.x * k) + (rot * Vector3.up) * (d.y * k);
        }

        void Dolly(float notches)
        {
            _camAuto = false;
            var fwd = CamRot * Vector3.forward;
            float dist = Mathf.Max(0.05f, Vector3.Dot(OrbitPivot() - _camPos, fwd));
            float step = Mathf.Max(0.02f, dist * 0.15f) * notches;
            _camPos += fwd * step;
            _pivotDist = Mathf.Max(0.15f, _pivotDist - step);
        }

        void FrameSelected()
        {
            if (_sel == null)
            {
                Status("Select something first; then F frames it.");
                return;
            }
            TakeCamera();
            var b = BoundsOf(_sel);
            float radius = Mathf.Max(0.04f, b.extents.magnitude);
            // Fit it in the part of the view the panels leave free (not the whole screen: the bars would cover it), with
            // a margin, whatever the field of view (a shot's narrow lens included), and centre it there.
            var full = _root != null ? _root.worldBound : new Rect(0f, 0f, 1920f, 1080f);
            var area = ViewArea();
            float fracV = Mathf.Clamp(area.height / Mathf.Max(1f, full.height), 0.3f, 1f);
            float fracH = Mathf.Clamp(area.width / Mathf.Max(1f, full.width), 0.3f, 1f);
            float tanV = Mathf.Tan(_fov * 0.5f * Mathf.Deg2Rad), aspect = Screen.width / Mathf.Max(1f, (float)Screen.height);
            float half = Mathf.Atan(Mathf.Min(tanV * fracV, tanV * aspect * fracH));
            float dist = radius / Mathf.Sin(half) * 1.12f;
            var rot = CamRot;
            float ox = (area.center.x - full.center.x) / Mathf.Max(1f, full.width * 0.5f);
            float oy = -(area.center.y - full.center.y) / Mathf.Max(1f, full.height * 0.5f);
            Vector3 rel = rot * new Vector3(ox * dist * tanV * aspect, oy * dist * tanV, dist);
            _camPos = b.center - rel;
            _pivotDist = dist;
            _camAuto = false;
            _throughShot = null;
            ApplyFreeCamera();
            Status("Framed " + _sel.label + ". Alt + left mouse orbits around it; the wheel moves in and out.");
        }

        void LookThroughShot()
        {
            if (_sel == null || !_sel.isShot) return;
            TakeCamera();
            _camPos = _sel.target.position;
            SetRot(_sel.target.rotation);
            _fov = _sel.fov;
            _throughShot = _sel;
            _camAuto = false;
            ApplyFreeCamera();
            Status("Looking through " + _sel.label + ". Fly to where it should be (right mouse + W A S D), then press \"Set from my view\".");
        }

        void ShotFromView()
        {
            if (_sel == null || !_sel.isShot) return;
            PushUndo("set " + Short(_sel) + " from your view");
            var cam = _stage.cam.transform;
            _sel.fov = _stage.cam.fieldOfView;
            var t = _sel.target;
            var world = Matrix4x4.TRS(cam.position, cam.rotation, Vector3.one);
            _stage.edits.MoveBy(_sel, world * Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse);
            Finished();
            Status(_sel.label + " now films from your view (field of view " + _sel.fov.ToString("0") + "°). Only this shot changed.");
        }

        // ---------- The keyboard ----------

        void HookText(Keyboard kb)
        {
            if (kb == _textKb) return;
            UnhookText();
            _textKb = kb;
            if (kb != null) kb.onTextInput += OnText;
        }

        void UnhookText()
        {
            if (_textKb != null) _textKb.onTextInput -= OnText;
            _textKb = null;
            _typed.Clear();
        }

        void OnText(char c)
        {
            if (_typed.Count < 32) _typed.Add(c);
        }

        void Keys(Keyboard kb)
        {
            if (kb == null) return;
            if (kb.f9Key.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (_editing != null)
            {
                NumberKeys(kb);
                return;
            }
            if (_confirmShown)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) ConfirmResetAll(true);
                else if (kb.escapeKey.wasPressedThisFrame) ConfirmResetAll(false);
                return;
            }
            if (_helpShown && (kb.escapeKey.wasPressedThisFrame || kb.hKey.wasPressedThisFrame))
            {
                ShowHelp(false);
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (_drag == Drag.Handle) CancelHandleDrag();
                else if (_sel != null) Select((VhEditable)null, true);
                return;
            }
            // While flying, W A S D Q E are the camera's.
            if (_drag == Drag.Look) return;
            bool ctrl = kb.ctrlKey.isPressed;
            if (ctrl)
            {
                if (kb.zKey.wasPressedThisFrame)
                {
                    if (kb.shiftKey.isPressed) Redo();
                    else Undo();
                }
                else if (kb.yKey.wasPressedThisFrame) Redo();
                else if (kb.sKey.wasPressedThisFrame) Save();
                return;
            }
            if (kb.altKey.isPressed) return;
            bool dragging = _drag != Drag.None && _drag != Drag.Ui;
            if (kb.spaceKey.wasPressedThisFrame) TogglePlay();
            else if (kb.hKey.wasPressedThisFrame) ShowHelp(!_helpShown);
            else if (dragging) return;
            else if (kb.wKey.wasPressedThisFrame) SetTool(Tool.Move, true);
            else if (kb.eKey.wasPressedThisFrame) SetTool(Tool.Rotate, true);
            else if (kb.rKey.wasPressedThisFrame) SetTool(Tool.Scale, true);
            else if (kb.xKey.wasPressedThisFrame) SetSnap(!_snap);
            else if (kb.cKey.wasPressedThisFrame) SetFree(!_free, true);
            else if (kb.fKey.wasPressedThisFrame) FrameSelected();
            else if (kb.lKey.wasPressedThisFrame) SetWorkLight(!_workLight);
            else if (kb.tabKey.wasPressedThisFrame) SetPanelsHidden(!_panelsHidden);
            else if (kb.rightArrowKey.wasPressedThisFrame) StepBeat(1);
            else if (kb.leftArrowKey.wasPressedThisFrame) StepBeat(-1);
        }

        void SetTool(Tool t, bool say)
        {
            _tool = t;
            if (!say) return;
            switch (t)
            {
                case Tool.Move: Status("Move (W): drag an arrow to move along it, the middle square to move across the view."); break;
                case Tool.Rotate: Status("Turn (E): drag a ring to turn around that axis (red X, green Y, blue Z)."); break;
                default:
                    Status("Size (R): drag a box to size along it, the middle box to size it all." +
                           (_sel != null && (!_sel.canScale || _sel.isShot) ? " " + _sel.label + " can't be sized." : ""));
                    break;
            }
        }

        void SetWorkLight(bool on)
        {
            _workLight = on;
            _stage.workLight = on ? 1f : 0f;
            Status(on ? "Work light on: an even light so you can see the set (only while editing; L turns it off to judge the real lighting)."
                : "Work light off: the lighting as it plays.");
        }

        void SetPanelsHidden(bool hidden)
        {
            _panelsHidden = hidden;
            ApplyPanelPlaces();
            Status(hidden ? "Panels hidden: a clean view (Tab brings them back)." : "Panels back (Tab hides them).");
        }

        void SetSnap(bool on)
        {
            _snap = on;
            Status(on ? "Snapping on: 1 cm, 5°, 5% (X turns it off; Ctrl held while dragging flips it)." : "Snapping off (X turns it on; Ctrl held while dragging flips it).");
        }

        // ---------- The mouse ----------

        void Pointer(Mouse m, Keyboard kb)
        {
            if (m == null) return;
            _screen = m.position.ReadValue();
            _mouse = ScreenToPanel(_screen);
            bool alt = kb != null && kb.altKey.isPressed, ctrl = kb != null && kb.ctrlKey.isPressed, shift = kb != null && kb.shiftKey.isPressed;
            Vector2 delta = m.delta.ReadValue();

            Ctl ctl = _drag == Drag.None || _drag == Drag.Ui ? HitControl(_mouse) : null;
            bool overUi = ctl != null || OverUi(_mouse);
            if (_drag == Drag.None)
            {
                SetHoverControl(ctl);
                bool viewHover = !overUi && !_helpShown && !_confirmShown;
                _hoverHandle = viewHover && !_playing ? HitHandle(_mouse, out _) : -1;
                if (!viewHover || _hoverHandle >= 0) _hover = null;
                else if (!_mouseSeen || (_mouse - _lastPickMouse).sqrMagnitude > 1f || Time.unscaledTime - _lastPick > 0.25f)
                {
                    _hover = PickThing(_mouse);
                    _lastPickMouse = _mouse;
                    _lastPick = Time.unscaledTime;
                }
                _mouseSeen = true;
            }
            else if (_drag != Drag.Ui) SetHoverControl(null);

            if (m.leftButton.wasPressedThisFrame) LeftDown(ctl, overUi, alt);
            if (m.rightButton.wasPressedThisFrame && _drag == Drag.None && !overUi && !Modal)
            {
                TakeCamera();
                _drag = Drag.Look;
                _hover = null;
                LockCursor();
            }
            if (m.middleButton.wasPressedThisFrame && _drag == Drag.None && !overUi && !Modal)
            {
                TakeCamera();
                _drag = Drag.Pan;
                _hover = null;
            }

            switch (_drag)
            {
                case Drag.Look:
                    if (!m.rightButton.isPressed) EndCameraDrag();
                    else Look(delta);
                    break;
                case Drag.Pan:
                    if (!m.middleButton.isPressed) EndCameraDrag();
                    else Pan(delta);
                    break;
                case Drag.Orbit:
                    if (!m.leftButton.isPressed) EndCameraDrag();
                    else Orbit(delta);
                    break;
                case Drag.Handle:
                    if (!m.leftButton.isPressed) EndHandleDrag(true);
                    else HandleDrag(ctrl);
                    break;
                case Drag.Scrub:
                    if (!m.leftButton.isPressed)
                    {
                        _drag = Drag.None;
                        Status("At " + Clock(_opening.Time) + " (" + BeatAt(_opening.Time) + ").");
                    }
                    else ScrubTo(_mouse.x);
                    break;
                case Drag.Number:
                    if (!m.leftButton.isPressed) NumberRelease();
                    else NumberDrag(shift);
                    break;
                case Drag.ListScroll:
                    if (!m.leftButton.isPressed) _drag = Drag.None;
                    else ScrollListTo(_mouse.y);
                    break;
                case Drag.Ui:
                    if (!m.leftButton.isPressed)
                    {
                        _drag = Drag.None;
                        if (_pressCtl != null && HitControl(_mouse) == _pressCtl) Click(_pressCtl);
                        _pressCtl = null;
                    }
                    break;
            }

            float wheel = m.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.001f)
            {
                float notches = Mathf.Abs(wheel) >= 30f ? wheel / 120f : wheel;
                Wheel(notches, overUi);
            }
        }

        bool Modal => _helpShown || _confirmShown;

        void LeftDown(Ctl ctl, bool overUi, bool alt)
        {
            _lastPress = "at panel " + _mouse.ToString("F0") + " (screen " + _screen.ToString("F0") + ", " + Screen.width + "x" + Screen.height + "): " +
                         (ctl != null ? "control " + ctl.id : overUi ? "a panel" : "the view") + ", tool " + _tool + ", drag " + _drag;
            if (_editing != null && (ctl == null || ctl.number != _editing)) CommitNumber();
            if (_helpShown)
            {
                ShowHelp(false);
                _drag = Drag.Ui;
                _pressCtl = null;
                return;
            }
            if (_drag != Drag.None) return;
            if (ctl != null)
            {
                if (ctl.number != null)
                {
                    _drag = Drag.Number;
                    _pressCtl = ctl;
                    _pressMouse = _mouse;
                    NumberPress(ctl.number);
                    return;
                }
                if (ctl.track)
                {
                    _drag = Drag.Scrub;
                    ScrubTo(_mouse.x);
                    return;
                }
                if (ctl.scrollbar)
                {
                    _drag = Drag.ListScroll;
                    ScrollListTo(_mouse.y);
                    return;
                }
                _drag = Drag.Ui;
                _pressCtl = ctl;
                return;
            }
            if (overUi || _confirmShown)
            {
                // A panel's background: nothing (and never the view behind it).
                _drag = Drag.Ui;
                _pressCtl = null;
                return;
            }
            if (alt)
            {
                TakeCamera();
                _orbitPivot = OrbitPivot();
                _drag = Drag.Orbit;
                _hover = null;
                return;
            }
            if (_playing)
            {
                Status("Pause first (Space) to pick and move things.");
                return;
            }
            int h = HitHandle(_mouse, out var ring);
            _lastPress += ", handle " + h;
            if (h >= 0)
            {
                BeginHandleDrag(h, ring);
                return;
            }
            var picked = PickThing(_mouse);
            _lastPress += ", picked " + (picked != null ? picked.id : "nothing");
            bool wall = PickedSomething;
            if (picked == null && _sel == null)
            {
                Status(wall ? "That can't be moved (the walls, floor and window stay put). Click another thing, or pick one from the list." : "Nothing there to select.");
                return;
            }
            Select(picked, true);
            if (picked == null && wall) Status("Nothing selected (that's a wall, the floor or the window: they stay put).");
        }

        void EndCameraDrag()
        {
            var was = _drag;
            _drag = Drag.None;
            UnlockCursor();
            if (was == Drag.Look) _lastPick = -1f;
        }

        void Wheel(float notches, bool overUi)
        {
            if (Modal) return;
            if (overUi)
            {
                // Anywhere over the list's panel (its header and edges too), the wheel scrolls the list.
                if (_list != null && Shown(_list) && Over(_left, _mouse) && TopPanelAt(_mouse) == _left) _list.scrollOffset -= new Vector2(0f, notches * 48f);
                return;
            }
            if (_drag == Drag.Look)
            {
                _flySpeed = Mathf.Clamp(_flySpeed * Mathf.Pow(1.25f, notches), 0.1f, 20f);
                Status("Fly speed: " + _flySpeed.ToString("0.0") + " m/s (the wheel, while flying; Shift is 4x).");
                return;
            }
            if (_drag != Drag.None) return;
            TakeCamera();
            Dolly(notches);
        }

        // ---------- Handles: dragging ----------

        Ray MouseRay() => _stage.cam.ScreenPointToRay(new Vector3(_screen.x, _screen.y, 0f));

        /// <summary>Where along the axis line (through the drag's pivot) the mouse ray passes closest.</summary>
        bool AxisParam(Ray ray, out float t)
        {
            t = 0f;
            Vector3 a = _dragAxis, d = ray.direction, w = _dragPivot - ray.origin;
            float b = Vector3.Dot(a, d);
            float denom = 1f - b * b;
            if (denom < 0.004f) return false;
            t = (b * Vector3.Dot(d, w) - Vector3.Dot(a, w)) / denom;
            return true;
        }

        bool PlaneHit(Ray ray, out Vector3 hit)
        {
            var plane = new Plane(_stage.cam.transform.forward, _dragPivot);
            hit = Vector3.zero;
            if (!plane.Raycast(ray, out float enter)) return false;
            hit = ray.GetPoint(enter);
            return true;
        }

        void BeginHandleDrag(int h, Vector3 ring)
        {
            var t = _sel.target;
            PushUndo(null);
            _drag = Drag.Handle;
            _dragHandle = h;
            _dragPivot = Pivot(_sel);
            _dragWorld0 = t.localToWorldMatrix;
            _dragScale0 = t.localScale;
            _dragMouse0 = _mouse;
            _dragRing = ring;
            _dragAmount = 0f;
            _dragLength0 = HandleLength(_sel);
            _dragAxis = h < 3 ? Axis(_sel, h) : Vector3.zero;
            _dragParallel = false;
            var ray = MouseRay();
            if (h < 3 && _tool != Tool.Rotate) _dragParallel = !AxisParam(ray, out _dragT0);
            if (h == 3 && _tool == Tool.Move) PlaneHit(ray, out _dragPlane0);
            _hover = null;
        }

        static readonly string[] AxisNames = { "X (red)", "Y (green, up)", "Z (blue)" };

        void HandleDrag(bool ctrl)
        {
            var e = _sel;
            if (e == null || e.target == null) return;
            bool snap = _snap != ctrl;
            var ray = MouseRay();
            Vector2 md = _mouse - _dragMouse0;
            string name = Short(e);
            if (_tool == Tool.Move)
            {
                Vector3 shift;
                if (_dragHandle == 3)
                {
                    if (!PlaneHit(ray, out var hit)) return;
                    shift = hit - _dragPlane0;
                    if (snap) shift = new Vector3(Round(shift.x, SnapMove), Round(shift.y, SnapMove), Round(shift.z, SnapMove));
                    _dragAmount = shift.magnitude;
                    Status("Moving " + name + " across the view: " + Cm(shift.magnitude) + (snap ? " (snapping)" : "") + ReachNote());
                }
                else
                {
                    float along;
                    if (!_dragParallel && AxisParam(ray, out float t)) along = t - _dragT0;
                    else along = ScreenAlong(md, _dragAxis, _dragLength0);
                    if (snap) along = Round(along, SnapMove);
                    shift = _dragAxis * along;
                    _dragAmount = along;
                    Status("Moving " + name + " along " + AxisNames[_dragHandle] + ": " + SignedCm(along) + (snap ? " (snapping)" : "") + ReachNote());
                }
                var target = Matrix4x4.Translate(shift) * _dragWorld0;
                _stage.edits.MoveBy(e, target * e.target.localToWorldMatrix.inverse);
            }
            else if (_tool == Tool.Rotate)
            {
                if (!ToPanel(_dragPivot, out _)) return;
                Vector3 tangent = Vector3.Cross(_dragAxis, _dragRing - _dragPivot).normalized;
                if (!ToPanel(_dragRing, out var r0) || !ToPanel(_dragRing + tangent * (_dragLength0 * 0.2f), out var r1)) return;
                Vector2 ts = (r1 - r0).normalized;
                float angle = Vector2.Dot(md, ts) * 0.5f;
                if (snap) angle = Round(angle, SnapTurn);
                _dragAmount = angle;
                var target = Matrix4x4.Translate(_dragPivot) * Matrix4x4.Rotate(Quaternion.AngleAxis(angle, _dragAxis)) * Matrix4x4.Translate(-_dragPivot) * _dragWorld0;
                _stage.edits.MoveBy(e, target * e.target.localToWorldMatrix.inverse);
                Status("Turning " + name + " around " + AxisNames[_dragHandle] + ": " + angle.ToString("0.0") + "°" + (snap ? " (snapping)" : ""));
            }
            else
            {
                float k;
                if (_dragHandle == 3) k = 1f + (md.x - md.y) / 200f;
                else
                {
                    float along;
                    if (!_dragParallel && AxisParam(ray, out float t)) along = t - _dragT0;
                    else along = ScreenAlong(md, _dragAxis, _dragLength0);
                    k = 1f + along / Mathf.Max(0.01f, _dragLength0);
                }
                if (snap) k = Round(k, SnapSize);
                k = Mathf.Max(0.05f, k);
                _dragAmount = k - 1f;
                Vector3 s = _dragScale0;
                if (_dragHandle == 3) s *= k;
                else s[_dragHandle] *= k;
                _stage.edits.Set(e, e.target.localPosition, e.target.localRotation, s);
                Status("Sizing " + name + (_dragHandle == 3 ? "" : " along " + AxisNames[_dragHandle]) + ": " + (k * 100f).ToString("0") + "%" + (snap ? " (snapping)" : ""));
            }
            ShowFields();
        }

        /// <summary>A move along an axis from the mouse's travel on screen (when the axis points at the camera).</summary>
        float ScreenAlong(Vector2 md, Vector3 axis, float length)
        {
            if (!ToPanel(_dragPivot, out var p0) || !ToPanel(_dragPivot + axis * length, out var p1)) return 0f;
            Vector2 ds = p1 - p0;
            if (ds.sqrMagnitude < 1f) return 0f;
            return Vector2.Dot(md, ds.normalized) / ds.magnitude * length;
        }

        void EndHandleDrag(bool commit)
        {
            if (_drag != Drag.Handle) return;
            _drag = Drag.None;
            var e = _sel;
            if (!commit || e == null) return;
            if (Mathf.Abs(_dragAmount) < 1e-5f)
            {
                // A click on a handle without a move: no undo step, no change.
                if (_undo.Count > 0) _undo.RemoveAt(_undo.Count - 1);
                Status(_tool == Tool.Move ? "Drag the arrow (hold the button and move the mouse) to move " + Short(e) + "." : "Hold the button and move the mouse to " + (_tool == Tool.Rotate ? "turn " : "size ") + Short(e) + ".");
                return;
            }
            string what;
            if (_tool == Tool.Move) what = _dragHandle == 3 ? "moved " + Short(e) + " " + Cm(_dragAmount) + " across the view" : "moved " + Short(e) + " " + SignedCm(_dragAmount) + " along " + AxisNames[_dragHandle];
            else if (_tool == Tool.Rotate) what = "turned " + Short(e) + " " + _dragAmount.ToString("0.0") + "° around " + AxisNames[_dragHandle];
            else what = "sized " + Short(e) + " to " + ((1f + _dragAmount) * 100f).ToString("0") + "%";
            if (_undo.Count > 0)
            {
                var top = _undo[_undo.Count - 1];
                top.what = what;
                _undo[_undo.Count - 1] = top;
            }
            Finished();
            Status(char.ToUpperInvariant(what[0]) + what.Substring(1) + ". (Ctrl+Z undoes.)" + ReachNote());
        }

        void CancelHandleDrag()
        {
            if (_drag != Drag.Handle) return;
            _drag = Drag.None;
            if (_undo.Count > 0)
            {
                var step = _undo[_undo.Count - 1];
                _undo.RemoveAt(_undo.Count - 1);
                _stage.edits.ApplyAll(VhSceneEditsData.FromJson(step.json));
                _stage.blanketDirty = false;
            }
            ShowFields();
            Status("Cancelled: " + Short(_sel) + " is back where it was.");
        }

        static float Round(float v, float step) => Mathf.Round(v / step) * step;

        static string Cm(float metres) => (metres * 100f).ToString("0.0") + " cm";

        static string SignedCm(float metres) => (metres >= 0f ? "+" : "") + (metres * 100f).ToString("0.0") + " cm";

        // ---------- The timeline's scrub bar ----------

        void ScrubTo(float panelX)
        {
            if (_track == null || _opening == null) return;
            var r = _track.worldBound;
            float k = Mathf.Clamp01((panelX - r.xMin) / Mathf.Max(1f, r.width));
            float t = k * _opening.Length;
            if (Mathf.Abs(t - _opening.Time) < 0.02f) return;
            SeekTo(t);
            Status("At " + Clock(_opening.Time) + " (" + BeatAt(_opening.Time) + ")." + SetNote());
        }
    }
}
