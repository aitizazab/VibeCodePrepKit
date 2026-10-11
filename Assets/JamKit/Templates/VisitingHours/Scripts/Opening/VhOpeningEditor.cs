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
    /// "Edit the opening") pauses the opening and lets the creator scrub to any beat, pick a thing (in the view or from the
    /// list, by plain names), move, turn or size it with handles or numbers, set each shot's camera from a free-flying
    /// view, undo, reset, save, and play on from any point to check. What moves is <see cref="VhSceneEdits"/>; the saved
    /// overrides are applied by the builder every time it builds the sets.
    /// </summary>
    public sealed class VhOpeningEditor : MonoBehaviour
    {
        enum Tool { Move, Rotate, Scale }

        struct Beat
        {
            public string name;
            public float time;
        }

        static VhOpeningEditor _instance;
        /// <summary>Closing saves unsaved edits (tests turn it off so nothing is written).</summary>
        public static bool SaveOnClose = true;
        public static VhOpeningEditor Instance => _instance;

        VhTitleRunner _runner;
        VhStage _stage;
        VhOpening _opening;
        bool _open, _openWhenReady, _playing, _free = true, _snap = true, _looking, _typing, _dirty;
        Tool _tool = Tool.Move;
        VhEditable _sel;
        readonly List<Beat> _beats = new List<Beat>();
        readonly List<string> _undo = new List<string>(), _redo = new List<string>();
        readonly Dictionary<VhEditable, Renderer[]> _renderers = new Dictionary<VhEditable, Renderer[]>();
        readonly List<Button> _listButtons = new List<Button>();
        readonly List<VhEditable> _listItems = new List<VhEditable>();

        // The free camera.
        Vector3 _flyPos;
        float _yaw, _pitch, _flyFov = 55f, _flySpeed = 1.2f;

        // A drag on a handle.
        int _dragAxis = -1, _hoverAxis = -1;
        Vector2 _dragStart, _dragMouse;
        Matrix4x4 _dragWorld;
        Vector3 _dragScale, _dragPivot, _dragRingPoint;
        float _dragApplied;

        // The interface.
        VisualElement _root, _view, _left, _right, _top, _help, _list;
        Label _status, _timeLabel, _name, _note, _toolLabel, _camLabel;
        Slider _time;
        FloatField[] _pos, _rot, _scl;
        FloatField _fov;
        VisualElement _shotRow, _scaleRow;
        Toggle _snapToggle;
        Button _playButton;

        public bool IsOpen => _open;
        public VhEditable Selected => _sel;
        public VhStage Stage => _stage;
        public VhOpening Opening => _opening;
        public IReadOnlyList<string> BeatNames
        {
            get
            {
                var n = new List<string>();
                foreach (var b in _beats) n.Add(b.name);
                return n;
            }
        }

        /// <summary>Starts the game straight in the editor ("Edit the opening.cmd" next to the exe passes it).</summary>
        public const string LaunchFlag = "-edit-opening";

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
            BuildBeats();
            BuildUi();
            var c = _stage.cam.transform;
            _flyPos = c.position;
            var e = c.rotation.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
            _flyFov = _stage.cam.fieldOfView;
            if (fresh) SeekTo(TitleTime());
            SetFree(true);
            Select((VhEditable)null);
            Status("Editing the opening. F9 closes (your edits are saved). H for help.");
            return true;
        }

        /// <summary>Closes the editor: saves unsaved edits, then the opening plays on from where it is (or the title, at its end).</summary>
        public void Close()
        {
            if (!_open) return;
            if (_dirty && SaveOnClose) Save();
            _open = false;
            _dragAxis = -1;
            _looking = false;
            if (_root != null) _root.RemoveFromHierarchy();
            _root = null;
            if (_stage != null) _stage.editorCamera = false;
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
            }
            if (!_open)
            {
                if (kb != null && kb.f9Key.wasPressedThisFrame && onTitle && (VhBoot.IsActive || VhBoot.Stage != null)) Open();
                return;
            }
            if (_runner == null || _runner.stage != _stage || _stage == null || !onTitle)
            {
                // The set went (the title left, a rebuild): close quietly.
                _open = false;
                if (_root != null) _root.RemoveFromHierarchy();
                _root = null;
                return;
            }
            if (_opening == null || !_opening.Running) Rebind();
            if (_stage.table != null && _stage.table.interactive) _stage.SetInteractive(false);
            Keys(kb);
            FlyCamera();
            if (_playing && _opening != null)
            {
                _time.SetValueWithoutNotify(_opening.Time);
                if (!_opening.Running) _playing = false;
            }
            if (_timeLabel != null && _opening != null) _timeLabel.text = Clock(_opening.Time) + " / " + Clock(_opening.Length) + "   " + BeatAt(_opening.Time);
            _view?.MarkDirtyRepaint();
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
            _opening.Seek(TitleTime());
            _time?.SetValueWithoutNotify(_opening.Time);
            if (_playButton != null) _playButton.text = "Play from here (Space)";
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

        string BeatAt(float t)
        {
            string name = "";
            foreach (var b in _beats)
                if (b.time <= t + 0.01f) name = b.name;
            return name;
        }

        static string Clock(float t) => ((int)(t / 60f)).ToString("0") + ":" + (t % 60f).ToString("00.0");

        /// <summary>Jumps the opening to <paramref name="t"/> seconds, paused.</summary>
        public void SeekTo(float t)
        {
            if (_opening == null || !_opening.Running) Rebind();
            if (_opening == null) return;
            _playing = false;
            _opening.Paused = true;
            _opening.Seek(Mathf.Clamp(t, 0f, _opening.Length));
            _time?.SetValueWithoutNotify(_opening.Time);
            if (_playButton != null) _playButton.text = "Play from here (Space)";
            if (_sel != null) ShowFields();
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

        void StepBeat(int dir)
        {
            float t = _opening != null ? _opening.Time : 0f;
            if (dir > 0)
            {
                foreach (var b in _beats)
                    if (b.time > t + 0.05f)
                    {
                        SeekTo(b.time);
                        return;
                    }
            }
            else
                for (int i = _beats.Count - 1; i >= 0; i--)
                    if (_beats[i].time < t - 0.05f)
                    {
                        SeekTo(_beats[i].time);
                        return;
                    }
        }

        void TogglePlay()
        {
            if (_opening == null) return;
            if (_playing)
            {
                _playing = false;
                _opening.Paused = true;
                foreach (var a in _stage.Sounds()) a.Pause();
                _playButton.text = "Play from here (Space)";
                return;
            }
            _playing = true;
            _dragAxis = -1;
            SetFree(false);
            _opening.PlayFromHere();
            _playButton.text = "Pause (Space)";
        }

        // ---------- Selecting and moving ----------

        /// <summary>Selects a thing by its id (null: nothing).</summary>
        public bool Select(string id)
        {
            var e = id != null ? _stage?.edits?.Find(id) : null;
            Select(e);
            return e != null || id == null;
        }

        void Select(VhEditable e)
        {
            _sel = e;
            for (int i = 0; i < _listButtons.Count; i++)
                _listButtons[i].style.backgroundColor = _listItems[i] == e ? new Color(0.25f, 0.42f, 0.62f) : new Color(0.16f, 0.17f, 0.2f);
            if (_right == null) return;
            _right.style.display = e != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (e == null) return;
            // A thing at the other set: go where it is (the title table, or the bedroom's first beat).
            bool atTable = _stage.Pose == "table";
            if (e.bedroom == atTable && !_playing) SeekTo(e.bedroom ? BeatTime("The phone on the nightstand") : TitleTime());
            _name.text = e.label;
            _note.text = e.note;
            _note.style.display = string.IsNullOrEmpty(e.note) ? DisplayStyle.None : DisplayStyle.Flex;
            _shotRow.style.display = e.isShot ? DisplayStyle.Flex : DisplayStyle.None;
            _scaleRow.style.display = e.isShot || !e.canScale ? DisplayStyle.None : DisplayStyle.Flex;
            ShowFields();
        }

        float BeatTime(string name)
        {
            foreach (var b in _beats)
                if (b.name == name) return b.time;
            return 0f;
        }

        void ShowFields()
        {
            if (_sel == null || _pos == null) return;
            var t = _sel.target;
            Vector3 p = t.localPosition, r = t.localRotation.eulerAngles, s = t.localScale;
            for (int i = 0; i < 3; i++)
            {
                _pos[i].SetValueWithoutNotify(Round(p[i], 3));
                _rot[i].SetValueWithoutNotify(Round(r[i] > 180f ? r[i] - 360f : r[i], 1));
                _scl[i].SetValueWithoutNotify(Round(s[i], 3));
            }
            _fov.SetValueWithoutNotify(Round(_sel.fov, 1));
        }

        static float Round(float v, int digits)
        {
            float k = Mathf.Pow(10f, digits);
            return Mathf.Round(v * k) / k;
        }

        /// <summary>Moves the selected thing by a world move (and what it carries); one undo step.</summary>
        public void MoveSelected(Matrix4x4 worldDelta)
        {
            if (_sel == null) return;
            PushUndo();
            _stage.edits.MoveBy(_sel, worldDelta);
            Finished();
        }

        void FromFields()
        {
            if (_sel == null) return;
            PushUndo();
            var t = _sel.target;
            var local = Matrix4x4.TRS(new Vector3(_pos[0].value, _pos[1].value, _pos[2].value),
                Quaternion.Euler(_rot[0].value, _rot[1].value, _rot[2].value),
                _sel.canScale && !_sel.isShot ? new Vector3(_scl[0].value, _scl[1].value, _scl[2].value) : t.localScale);
            var world = t.parent != null ? t.parent.localToWorldMatrix * local : local;
            if (_sel.isShot) _sel.fov = Mathf.Clamp(_fov.value, 10f, 120f);
            _stage.edits.MoveBy(_sel, world * t.localToWorldMatrix.inverse);
            if (_sel.canScale && !_sel.isShot) _stage.edits.Set(_sel, t.localPosition, t.localRotation, local.lossyScale);
            Finished();
        }

        /// <summary>After any change: the blanket reshapes if she moved, the fields show the new numbers.</summary>
        void Finished()
        {
            if (_stage.blanketDirty) VhStageBuilder.ReshapeBlanket(_stage);
            _dirty = true;
            ShowFields();
        }

        void PushUndo()
        {
            _undo.Add(_stage.edits.Capture().ToJson());
            if (_undo.Count > 100) _undo.RemoveAt(0);
            _redo.Clear();
        }

        /// <summary>Undoes the last change (true if there was one).</summary>
        public bool Undo() => Step(_undo, _redo, "Undone.");

        public bool Redo() => Step(_redo, _undo, "Redone.");

        bool Step(List<string> from, List<string> to, string said)
        {
            if (from.Count == 0) return false;
            to.Add(_stage.edits.Capture().ToJson());
            string json = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            _stage.edits.ApplyAll(VhSceneEditsData.FromJson(json));
            _dirty = true;
            ShowFields();
            Status(said);
            return true;
        }

        void ResetSelected()
        {
            if (_sel == null) return;
            PushUndo();
            _stage.edits.Reset(_sel);
            Finished();
            Status(_sel.label + ": back where it was built.");
        }

        void ResetEverything()
        {
            PushUndo();
            _stage.edits.ApplyAll(new VhSceneEditsData());
            _dirty = true;
            ShowFields();
            Status("Everything is back where it was built (Ctrl+Z to undo).");
        }

        /// <summary>Saves the edits; the status line says where.</summary>
        public void Save()
        {
            var wrote = VhSceneEdits.Save(_stage.edits.Capture());
            _dirty = false;
            Status(wrote.Count > 0 ? "Saved to " + string.Join("  and  ", wrote) : "Couldn't save (see the log).");
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
            BuildList();
            SeekTo(t);
            SetFree(_free);
            Select(sel);
            Status("Rebuilt with your edits (the lighting worked out again).");
        }

        // ---------- The camera ----------

        void SetFree(bool free)
        {
            _free = free;
            _stage.editorCamera = free;
            if (free)
            {
                _stage.cam.transform.SetPositionAndRotation(_flyPos, Quaternion.Euler(_pitch, _yaw, 0f));
                _stage.cam.fieldOfView = _flyFov;
            }
            else _stage.RefreshPose();
            if (_camLabel != null) _camLabel.text = free ? "Camera: free (C for the shot's)" : "Camera: the shot's (C for free)";
        }

        void FlyCamera()
        {
            if (!_free || _stage == null) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (_looking && mouse != null)
            {
                if (!mouse.rightButton.isPressed) _looking = false;
                else
                {
                    Vector2 d = mouse.delta.ReadValue();
                    _yaw += d.x * 0.12f;
                    _pitch = Mathf.Clamp(_pitch - d.y * 0.12f, -89f, 89f);
                }
            }
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            if (_looking && kb != null)
            {
                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed) move += Vector3.forward;
                if (kb.sKey.isPressed) move += Vector3.back;
                if (kb.aKey.isPressed) move += Vector3.left;
                if (kb.dKey.isPressed) move += Vector3.right;
                if (kb.eKey.isPressed) move += Vector3.up;
                if (kb.qKey.isPressed) move += Vector3.down;
                float speed = _flySpeed * (kb.leftShiftKey.isPressed ? 3f : 1f);
                _flyPos += rot * move * (speed * Time.unscaledDeltaTime);
            }
            _stage.cam.transform.SetPositionAndRotation(_flyPos, rot);
            _stage.cam.fieldOfView = _flyFov;
        }

        void FrameSelected()
        {
            if (_sel == null) return;
            if (!_free) SetFree(true);
            var b = BoundsOf(_sel);
            float size = Mathf.Max(0.15f, b.extents.magnitude);
            var dir = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
            _flyPos = b.center - dir * (size * 2.6f + 0.1f);
        }

        void LookThroughShot()
        {
            if (_sel == null || !_sel.isShot) return;
            SetFree(true);
            _flyPos = _sel.target.position;
            var e = _sel.target.rotation.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
            _flyFov = _sel.fov;
            Status("Looking through " + _sel.label + ". Fly to where it should be, then 'Set from my view'.");
        }

        void ShotFromView()
        {
            if (_sel == null || !_sel.isShot) return;
            PushUndo();
            var cam = _stage.cam.transform;
            _sel.fov = _stage.cam.fieldOfView;
            var t = _sel.target;
            var world = Matrix4x4.TRS(cam.position, cam.rotation, Vector3.one);
            _stage.edits.MoveBy(_sel, world * Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse);
            Finished();
            Status(_sel.label + ": set from your view (only this shot changed).");
        }

        // ---------- Keys ----------

        void Keys(Keyboard kb)
        {
            if (kb == null) return;
            if (kb.f9Key.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (_typing || _looking) return;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (ctrl && kb.zKey.wasPressedThisFrame)
            {
                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) Redo();
                else Undo();
            }
            else if (ctrl && kb.yKey.wasPressedThisFrame) Redo();
            else if (ctrl && kb.sKey.wasPressedThisFrame) Save();
            else if (kb.spaceKey.wasPressedThisFrame) TogglePlay();
            else if (kb.wKey.wasPressedThisFrame) SetTool(Tool.Move);
            else if (kb.eKey.wasPressedThisFrame) SetTool(Tool.Rotate);
            else if (kb.rKey.wasPressedThisFrame) SetTool(Tool.Scale);
            else if (kb.cKey.wasPressedThisFrame) SetFree(!_free);
            else if (kb.fKey.wasPressedThisFrame) FrameSelected();
            else if (kb.xKey.wasPressedThisFrame) _snapToggle.value = !_snapToggle.value;
            else if (kb.hKey.wasPressedThisFrame) _help.style.display = _help.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            else if (kb.rightArrowKey.wasPressedThisFrame) StepBeat(1);
            else if (kb.leftArrowKey.wasPressedThisFrame) StepBeat(-1);
            else if (kb.escapeKey.wasPressedThisFrame) Select((VhEditable)null);
        }

        void SetTool(Tool t)
        {
            _tool = t;
            if (_toolLabel != null) _toolLabel.text = "Handles: " + (t == Tool.Move ? "move (W)" : t == Tool.Rotate ? "turn (E)" : "size (R)");
        }

        void Status(string text)
        {
            if (_status != null) _status.text = text;
        }

        // ---------- Handles (drawn and picked in screen space) ----------

        bool ToPanel(Vector3 world, out Vector2 p)
        {
            p = Vector2.zero;
            var cam = _stage.cam;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.01f || _view?.panel == null) return false;
            p = RuntimePanelUtils.ScreenToPanel(_view.panel, new Vector2(sp.x, Screen.height - sp.y));
            return true;
        }

        Vector3 Pivot(VhEditable e) => e.target.position;

        float HandleLength(VhEditable e) => Mathf.Max(0.03f, Vector3.Distance(_stage.cam.transform.position, Pivot(e)) * 0.18f);

        Vector3 Axis(VhEditable e, int i)
        {
            Vector3 a = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            return _tool == Tool.Scale ? e.target.rotation * a : a;
        }

        static readonly Color[] AxisColors = { new Color(0.95f, 0.3f, 0.3f), new Color(0.4f, 0.9f, 0.35f), new Color(0.35f, 0.55f, 1f) };

        void Draw(MeshGenerationContext ctx)
        {
            if (!_open || _stage == null) return;
            var p = ctx.painter2D;
            // Shots and lights show as small marks so they can be found and picked.
            foreach (var e in _stage.edits.All)
            {
                if (e.target == null || !e.target.gameObject.activeInHierarchy || (!e.isShot && Renderers(e).Length > 0)) continue;
                if (!ToPanel(e.target.position, out var c)) continue;
                p.strokeColor = e == _sel ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.7f);
                p.lineWidth = 2f;
                p.BeginPath();
                p.Arc(c, e.isShot ? 9f : 6f, 0f, 360f);
                p.Stroke();
                if (e.isShot && ToPanel(e.target.position + e.target.forward * 0.15f, out var f))
                {
                    p.BeginPath();
                    p.MoveTo(c);
                    p.LineTo(f);
                    p.Stroke();
                }
            }
            if (_sel == null || _sel.target == null || !_sel.target.gameObject.activeInHierarchy || _playing) return;
            Vector3 o = Pivot(_sel);
            if (!ToPanel(o, out var po)) return;
            float L = HandleLength(_sel);
            for (int i = 0; i < 3; i++)
            {
                Color col = (_dragAxis == i || (_dragAxis < 0 && _hoverAxis == i)) ? new Color(1f, 0.9f, 0.3f) : AxisColors[i];
                p.strokeColor = col;
                p.fillColor = col;
                p.lineWidth = 3f;
                Vector3 a = Axis(_sel, i);
                if (_tool == Tool.Rotate)
                {
                    bool started = false;
                    p.BeginPath();
                    for (int k = 0; k <= 48; k++)
                    {
                        if (!ToPanel(RingPoint(o, a, L * 0.85f, k / 48f * Mathf.PI * 2f), out var q)) { started = false; continue; }
                        if (!started) p.MoveTo(q);
                        else p.LineTo(q);
                        started = true;
                    }
                    p.Stroke();
                    continue;
                }
                if (_tool == Tool.Scale && (_sel.isShot || !_sel.canScale)) continue;
                if (!ToPanel(o + a * L, out var pe)) continue;
                p.BeginPath();
                p.MoveTo(po);
                p.LineTo(pe);
                p.Stroke();
                p.BeginPath();
                if (_tool == Tool.Move) p.Arc(pe, 6f, 0f, 360f);
                else
                {
                    p.MoveTo(pe + new Vector2(-5f, -5f));
                    p.LineTo(pe + new Vector2(5f, -5f));
                    p.LineTo(pe + new Vector2(5f, 5f));
                    p.LineTo(pe + new Vector2(-5f, 5f));
                    p.ClosePath();
                }
                p.Fill();
            }
            if (_tool == Tool.Scale && _sel.canScale && !_sel.isShot)
            {
                p.fillColor = _dragAxis == 3 || (_dragAxis < 0 && _hoverAxis == 3) ? new Color(1f, 0.9f, 0.3f) : Color.white;
                p.BeginPath();
                p.Arc(po, 7f, 0f, 360f);
                p.Fill();
            }
        }

        static Vector3 RingPoint(Vector3 o, Vector3 axis, float r, float angle)
        {
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            return o + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * r;
        }

        static float SegmentDistance(Vector2 m, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(m - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(m, a + ab * t);
        }

        /// <summary>The handle under the mouse (0..2 an axis, 3 the middle when sizing), or -1.</summary>
        int HitHandle(Vector2 m, out Vector3 ringPoint)
        {
            ringPoint = Vector3.zero;
            if (_sel == null || _sel.target == null || !_sel.target.gameObject.activeInHierarchy) return -1;
            Vector3 o = Pivot(_sel);
            if (!ToPanel(o, out var po)) return -1;
            float L = HandleLength(_sel);
            if (_tool == Tool.Scale && _sel.canScale && !_sel.isShot && Vector2.Distance(m, po) < 11f) return 3;
            int best = -1;
            float bestD = 10f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 a = Axis(_sel, i);
                if (_tool == Tool.Rotate)
                {
                    for (int k = 0; k < 48; k++)
                    {
                        Vector3 w0 = RingPoint(o, a, L * 0.85f, k / 48f * Mathf.PI * 2f), w1 = RingPoint(o, a, L * 0.85f, (k + 1) / 48f * Mathf.PI * 2f);
                        if (!ToPanel(w0, out var q0) || !ToPanel(w1, out var q1)) continue;
                        float d = SegmentDistance(m, q0, q1);
                        if (d < bestD)
                        {
                            bestD = d;
                            best = i;
                            ringPoint = (w0 + w1) * 0.5f;
                        }
                    }
                    continue;
                }
                if (_tool == Tool.Scale && (_sel.isShot || !_sel.canScale)) continue;
                if (!ToPanel(o + a * L, out var pe)) continue;
                float dd = SegmentDistance(m, po, pe);
                if (dd < bestD)
                {
                    bestD = dd;
                    best = i;
                }
            }
            return best;
        }

        void OnPointerDown(PointerDownEvent ev)
        {
            if (!_open) return;
            Vector2 m = ev.localPosition;
            if (ev.button == 1)
            {
                if (!_free) SetFree(true);
                _looking = true;
                return;
            }
            if (ev.button != 0 || _playing) return;
            int h = HitHandle(m, out var ring);
            if (h >= 0)
            {
                PushUndo();
                _dragAxis = h;
                _dragStart = m;
                _dragMouse = m;
                _dragWorld = _sel.target.localToWorldMatrix;
                _dragScale = _sel.target.localScale;
                _dragPivot = Pivot(_sel);
                _dragRingPoint = ring;
                _dragApplied = 0f;
                _view.CapturePointer(ev.pointerId);
                return;
            }
            Select(Pick(m));
        }

        void OnPointerMove(PointerMoveEvent ev)
        {
            if (!_open) return;
            Vector2 m = ev.localPosition;
            if (_dragAxis < 0)
            {
                _hoverAxis = _playing ? -1 : HitHandle(m, out _);
                return;
            }
            _dragMouse = m;
            Drag();
        }

        void OnPointerUp(PointerUpEvent ev)
        {
            if (ev.button == 1) _looking = false;
            if (_dragAxis < 0 || ev.button != 0) return;
            _dragAxis = -1;
            if (_view.HasPointerCapture(ev.pointerId)) _view.ReleasePointer(ev.pointerId);
            Finished();
        }

        void Drag()
        {
            var e = _sel;
            if (e == null) return;
            Vector2 delta = _dragMouse - _dragStart;
            float L = HandleLength(e);
            if (_dragAxis == 3)
            {
                float k = 1f + (delta.x - delta.y) / 150f;
                if (_snap) k = Mathf.Round(k / 0.05f) * 0.05f;
                k = Mathf.Max(0.05f, k);
                _stage.edits.Set(e, e.target.localPosition, e.target.localRotation, _dragScale * k);
                ShowFields();
                return;
            }
            Vector3 axis = Axis(e, _dragAxis);
            if (!ToPanel(_dragPivot, out var p0)) return;
            if (_tool == Tool.Rotate)
            {
                Vector3 tangent = Vector3.Cross(axis, _dragRingPoint - _dragPivot).normalized;
                if (!ToPanel(_dragRingPoint, out var r0) || !ToPanel(_dragRingPoint + tangent * (L * 0.2f), out var r1)) return;
                Vector2 ts = (r1 - r0).normalized;
                float angle = Vector2.Dot(delta, ts) * 0.6f;
                if (_snap) angle = Mathf.Round(angle / 5f) * 5f;
                var target = Matrix4x4.Translate(_dragPivot) * Matrix4x4.Rotate(Quaternion.AngleAxis(angle, axis)) * Matrix4x4.Translate(-_dragPivot) * _dragWorld;
                _stage.edits.MoveBy(e, target * e.target.localToWorldMatrix.inverse);
                ShowFields();
                return;
            }
            if (!ToPanel(_dragPivot + axis * L, out var p1)) return;
            Vector2 ds = p1 - p0;
            if (ds.sqrMagnitude < 1f) return;
            float along = Vector2.Dot(delta, ds.normalized) / ds.magnitude * L;
            if (_tool == Tool.Move)
            {
                if (_snap) along = Mathf.Round(along / 0.01f) * 0.01f;
                var target = Matrix4x4.Translate(axis * along) * _dragWorld;
                _stage.edits.MoveBy(e, target * e.target.localToWorldMatrix.inverse);
            }
            else
            {
                float k = 1f + along / L;
                if (_snap) k = Mathf.Round(k / 0.05f) * 0.05f;
                k = Mathf.Max(0.05f, k);
                Vector3 s = _dragScale;
                s[_dragAxis] *= k;
                _stage.edits.Set(e, e.target.localPosition, e.target.localRotation, s);
            }
            ShowFields();
        }

        Renderer[] Renderers(VhEditable e)
        {
            if (!_renderers.TryGetValue(e, out var r) || r == null)
            {
                r = e.target != null ? e.target.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                _renderers[e] = r;
            }
            return r;
        }

        Bounds BoundsOf(VhEditable e)
        {
            var rs = Renderers(e);
            if (rs.Length == 0) return new Bounds(e.target.position, Vector3.one * 0.2f);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>The thing under the mouse: the nearest one whose drawn parts the ray meets, or a shot or light mark near it.</summary>
        VhEditable Pick(Vector2 m)
        {
            VhEditable best = null;
            float bestD = float.MaxValue;
            // Marks (shots, lights) first: they're small and sit inside other things.
            foreach (var e in _stage.edits.All)
            {
                if (e.target == null || !e.target.gameObject.activeInHierarchy || (!e.isShot && Renderers(e).Length > 0)) continue;
                if (ToPanel(e.target.position, out var c) && Vector2.Distance(c, m) < 12f) return e;
            }
            var tree = _view.panel.visualTree.layout;
            float sx = tree.width > 1f ? Screen.width / tree.width : 1f, sy = tree.height > 1f ? Screen.height / tree.height : 1f;
            var ray = _stage.cam.ScreenPointToRay(new Vector3(m.x * sx, Screen.height - m.y * sy, 0f));
            foreach (var e in _stage.edits.All)
            {
                if (e.isShot || e.target == null || !e.target.gameObject.activeInHierarchy) continue;
                foreach (var r in Renderers(e))
                {
                    if (r == null || !r.gameObject.activeInHierarchy) continue;
                    if (r.bounds.IntersectRay(ray, out float d) && d < bestD)
                    {
                        // Big things (the bed, the table) lose to smaller things in front of or on them.
                        float size = r.bounds.size.magnitude;
                        float score = d + size * 0.15f;
                        if (score < bestD)
                        {
                            bestD = score;
                            best = e;
                        }
                    }
                }
            }
            return best;
        }

        // ---------- The interface ----------

        static VisualElement Box(VisualElement parent, string name)
        {
            var e = new VisualElement { name = name };
            parent?.Add(e);
            return e;
        }

        static Label Text(VisualElement parent, string text, int size = 13, bool bold = false)
        {
            var l = new Label(text);
            l.style.color = new Color(0.93f, 0.93f, 0.95f);
            l.style.fontSize = size;
            l.style.whiteSpace = WhiteSpace.Normal;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent?.Add(l);
            return l;
        }

        static Button Btn(VisualElement parent, string text, System.Action click)
        {
            var b = new Button(click) { text = text, focusable = false };
            b.style.fontSize = 12;
            b.style.marginTop = 1;
            b.style.marginBottom = 1;
            parent?.Add(b);
            return b;
        }

        static void Panel(VisualElement e)
        {
            e.style.backgroundColor = new Color(0.07f, 0.075f, 0.09f, 0.9f);
            e.style.paddingLeft = e.style.paddingRight = 8;
            e.style.paddingTop = e.style.paddingBottom = 6;
            e.style.position = Position.Absolute;
        }

        FloatField[] Row(VisualElement parent, string label)
        {
            var row = Box(parent, label);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var l = Text(row, label);
            l.style.width = 62;
            var f = new FloatField[3];
            string[] axes = { "x", "y", "z" };
            for (int i = 0; i < 3; i++)
            {
                f[i] = new FloatField(axes[i]);
                f[i].style.flexGrow = 1;
                f[i].style.minWidth = 60;
                f[i].labelElement.style.minWidth = 10;
                f[i].labelElement.style.color = AxisColors[i];
                f[i].isDelayed = true;
                f[i].RegisterValueChangedCallback(_ => FromFields());
                f[i].RegisterCallback<FocusInEvent>(_ => _typing = true);
                f[i].RegisterCallback<FocusOutEvent>(_ => _typing = false);
                row.Add(f[i]);
            }
            return f;
        }

        void BuildUi()
        {
            _root = new VisualElement { name = "vh-opening-editor", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = _root.style.right = _root.style.top = _root.style.bottom = 0;

            // The view: picking, handles, the free camera's look.
            _view = Box(_root, "view");
            _view.style.position = Position.Absolute;
            _view.style.left = _view.style.right = _view.style.top = _view.style.bottom = 0;
            _view.pickingMode = PickingMode.Position;
            _view.generateVisualContent += Draw;
            _view.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _view.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _view.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _view.RegisterCallback<WheelEvent>(ev => _flySpeed = Mathf.Clamp(_flySpeed * (ev.delta.y > 0 ? 0.8f : 1.25f), 0.1f, 10f));

            // The timeline along the top.
            _top = Box(_root, "timeline");
            Panel(_top);
            _top.style.left = 230;
            _top.style.right = 290;
            _top.style.top = 6;
            var row = Box(_top, "row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            _playButton = Btn(row, "Play from here (Space)", TogglePlay);
            Btn(row, "< beat", () => StepBeat(-1));
            Btn(row, "beat >", () => StepBeat(1));
            _timeLabel = Text(row, "");
            _timeLabel.style.marginLeft = 8;
            _time = new Slider(0f, _opening.Length) { name = "time" };
            _time.style.flexGrow = 1;
            _time.RegisterValueChangedCallback(ev => SeekTo(ev.newValue));
            _top.Add(_time);
            var beats = Box(_top, "beats");
            beats.style.flexDirection = FlexDirection.Row;
            beats.style.flexWrap = Wrap.Wrap;
            foreach (var b in _beats)
            {
                float at = b.time;
                Btn(beats, b.name, () => SeekTo(at));
            }

            // The things, by plain names, on the left.
            _left = Box(_root, "things");
            Panel(_left);
            _left.style.left = 6;
            _left.style.top = 6;
            _left.style.bottom = 40;
            _left.style.width = 216;
            Text(_left, "Opening editor", 15, true);
            _list = new ScrollView();
            _list.style.flexGrow = 1;
            _left.Add(_list);
            BuildList();

            // The selected thing, on the right.
            _right = Box(_root, "selected");
            Panel(_right);
            _right.style.right = 6;
            _right.style.top = 6;
            _right.style.width = 278;
            _name = Text(_right, "", 14, true);
            _note = Text(_right, "", 12);
            _note.style.color = new Color(0.8f, 0.85f, 0.95f);
            _note.style.marginBottom = 4;
            _pos = Row(_right, "Position");
            _rot = Row(_right, "Turn (°)");
            _scaleRow = Box(_right, "scale");
            _scl = Row(_scaleRow, "Size");
            _shotRow = Box(_right, "shot");
            _fov = new FloatField("Field of view") { isDelayed = true };
            _fov.RegisterValueChangedCallback(_ => FromFields());
            _fov.RegisterCallback<FocusInEvent>(_ => _typing = true);
            _fov.RegisterCallback<FocusOutEvent>(_ => _typing = false);
            _shotRow.Add(_fov);
            Btn(_shotRow, "Set from my view", ShotFromView);
            Btn(_shotRow, "Look through it", LookThroughShot);
            Btn(_right, "Frame it in my view (F)", FrameSelected);
            Btn(_right, "Reset this one", ResetSelected);
            _right.style.display = DisplayStyle.None;

            // Tools and file, along the bottom.
            var bottom = Box(_root, "tools");
            Panel(bottom);
            bottom.style.left = 6;
            bottom.style.right = 6;
            bottom.style.bottom = 6;
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.alignItems = Align.Center;
            bottom.style.flexWrap = Wrap.Wrap;
            Btn(bottom, "Move (W)", () => SetTool(Tool.Move));
            Btn(bottom, "Turn (E)", () => SetTool(Tool.Rotate));
            Btn(bottom, "Size (R)", () => SetTool(Tool.Scale));
            _snapToggle = new Toggle("Snap (X)") { value = _snap };
            _snapToggle.labelElement.style.color = Color.white;
            _snapToggle.RegisterValueChangedCallback(ev => _snap = ev.newValue);
            bottom.Add(_snapToggle);
            Btn(bottom, "Undo (Ctrl+Z)", () => Undo());
            Btn(bottom, "Redo (Ctrl+Y)", () => Redo());
            Btn(bottom, "Camera (C)", () => SetFree(!_free));
            Btn(bottom, "Reset everything", ResetEverything);
            Btn(bottom, "Rebuild with my edits", Rebuild);
            Btn(bottom, "Save (Ctrl+S)", Save);
            Btn(bottom, "Help (H)", () => _help.style.display = _help.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None);
            Btn(bottom, "Close (F9)", Close);
            _toolLabel = Text(bottom, "");
            _toolLabel.style.marginLeft = 8;
            _camLabel = Text(bottom, "");
            _camLabel.style.marginLeft = 8;
            _status = Text(bottom, "");
            _status.style.marginLeft = 8;
            _status.style.flexShrink = 1;

            _help = Box(_root, "help");
            Panel(_help);
            _help.style.left = 240;
            _help.style.top = 150;
            _help.style.width = 430;
            _help.pickingMode = PickingMode.Ignore;
            Text(_help,
                "THE OPENING EDITOR\n\n" +
                "Click a thing in the view, or pick it from the list on the left.\n" +
                "Drag the coloured handles: W move, E turn, R size (the white dot sizes it all). X snaps (1 cm, 5°).\n" +
                "Or type numbers on the right (in its parent: the room, the bed, the table).\n\n" +
                "Hold the RIGHT mouse button to look around; with it held, W A S D move, Q E down/up, Shift faster, the wheel sets the speed.\n" +
                "C: the free camera or the shot's own camera. F: frame the selected thing.\n" +
                "Shots (cameras): 'Look through it', fly where it should be, then 'Set from my view'.\n\n" +
                "Space plays from here (and pauses). Left/right arrows jump between beats; the slider scrubs.\n" +
                "Ctrl+Z undo, Ctrl+Y redo, Ctrl+S save. F9 closes (and saves). H hides this.", 12);
            SetTool(_tool);
            GameRoot.Ui.Root.Add(_root);
        }

        void BuildList()
        {
            if (_list == null) return;
            _list.Clear();
            _listButtons.Clear();
            _listItems.Clear();
            string group = null;
            foreach (var e in _stage.edits.All)
            {
                if (e.group != group)
                {
                    group = e.group;
                    var h = Text(null, group, 12, true);
                    h.style.marginTop = 6;
                    h.style.color = new Color(0.7f, 0.78f, 0.9f);
                    _list.Add(h);
                }
                var item = e;
                var b = Btn(null, e.label, () => Select(item));
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                b.style.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
                b.style.color = Color.white;
                _list.Add(b);
                _listButtons.Add(b);
                _listItems.Add(e);
            }
        }
    }
}
