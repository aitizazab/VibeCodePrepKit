using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>One thing moved in the opening editor: its place in its parent (position, rotation in degrees, scale).</summary>
    [Serializable]
    public class VhObjectEdit
    {
        public string id = "";
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
    }

    /// <summary>One shot's camera set in the opening editor: its place in its set (the bedroom or the title table) and its field of view.</summary>
    [Serializable]
    public class VhShotEdit
    {
        public string id = "";
        public Vector3 position;
        public Vector3 rotation;
        public float fov = 60f;
    }

    /// <summary>
    /// The opening editor's saved overrides (docs/OPENING-EDITOR.md): only what differs from the built scene, keyed by
    /// stable ids ("phone", "nightstand", "shot:table"...). The builder applies them every time it builds the sets.
    /// </summary>
    [Serializable]
    public class VhSceneEditsData
    {
        public int version = 1;
        public List<VhObjectEdit> objects = new List<VhObjectEdit>();
        public List<VhShotEdit> shots = new List<VhShotEdit>();

        public bool IsEmpty => (objects == null || objects.Count == 0) && (shots == null || shots.Count == 0);

        public VhObjectEdit Object(string id)
        {
            if (objects != null)
                foreach (var o in objects)
                    if (o != null && o.id == id) return o;
            return null;
        }

        public VhShotEdit Shot(string id)
        {
            if (shots != null)
                foreach (var o in shots)
                    if (o != null && o.id == id) return o;
            return null;
        }

        public string ToJson() => JsonUtility.ToJson(this, true);

        public static VhSceneEditsData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new VhSceneEditsData();
            var d = JsonUtility.FromJson<VhSceneEditsData>(json) ?? new VhSceneEditsData();
            if (d.objects == null) d.objects = new List<VhObjectEdit>();
            if (d.shots == null) d.shots = new List<VhShotEdit>();
            return d;
        }
    }

    /// <summary>Something the opening editor can move: a piece of the set (a group under one handle) or a shot's camera.</summary>
    public sealed class VhEditable
    {
        public string id = "", label = "", group = "", note = "";
        /// <summary>What moves (an empty handle over several parts, for things made of parts).</summary>
        public Transform target;
        public Vector3 defaultPosition, defaultScale = Vector3.one;
        public Quaternion defaultRotation = Quaternion.identity;
        /// <summary>Placed after the lighting (it positions these itself).</summary>
        public bool afterLighting;
        /// <summary>In the bedroom (false: at the title table).</summary>
        public bool bedroom = true;
        /// <summary>A shot's camera: its pose index on the stage, and its field of view.</summary>
        public bool isShot;
        public int shotIndex = -1;
        public float fov, defaultFov;
        /// <summary>Moved along with this one in the editor (things standing on it, the shots from her eyes).</summary>
        public string[] carries = new string[0];
        /// <summary>Called after it moved, with the move (world); what follows it (her hand, her eyes, the phone's rest).</summary>
        public Action<Matrix4x4> moved;
        public bool canScale = true;
    }

    /// <summary>
    /// What the opening editor can move in the stage, by stable ids; applies saved overrides (<see cref="VhSceneEditsData"/>)
    /// and captures the current ones. Things made of several parts get one handle (an empty parent made here, where they
    /// stand), so moving them never touches their parts' own placement. Made by <see cref="VhStageBuilder"/>.
    /// </summary>
    public sealed class VhSceneEdits
    {
        public const string FileName = "VisitingHoursOpeningEdits.json";
        /// <summary>Resources path of the committed overrides (Assets/JamKit/Templates/VisitingHours/Resources/JamKit/).</summary>
        public const string ResourcePath = "JamKit/VisitingHoursOpeningEdits";
        /// <summary>Where they're committed, relative to the repository.</summary>
        public const string RepoPath = "Assets/JamKit/Templates/VisitingHours/Resources/JamKit/" + FileName;

        /// <summary>Tests: where the builder takes overrides from instead of the saved ones (null = the saved ones).</summary>
        public static Func<VhSceneEditsData> Source;

        /// <summary>Tests and the editor's capture tour: one file that <see cref="Save"/> writes to and <see cref="Load"/>
        /// reads from instead of the build's and the repository's (null = the real places).</summary>
        public static string FileOverride;

        readonly VhStage _stage;
        readonly List<VhEditable> _all = new List<VhEditable>();
        readonly Dictionary<string, VhEditable> _byId = new Dictionary<string, VhEditable>();

        public IReadOnlyList<VhEditable> All => _all;
        public VhStage Stage => _stage;

        VhSceneEdits(VhStage stage) => _stage = stage;

        public VhEditable Find(string id) => id != null && _byId.TryGetValue(id, out var e) ? e : null;

        // ---------- Loading and saving ----------

        /// <summary>
        /// The saved overrides: the ones this build saved last (its StreamingAssets), else the committed ones (Resources),
        /// else none (the opening exactly as built).
        /// </summary>
        public static VhSceneEditsData Load()
        {
            if (Source != null) return Source() ?? new VhSceneEditsData();
            try
            {
                if (FileOverride != null)
                    return File.Exists(FileOverride) ? VhSceneEditsData.FromJson(File.ReadAllText(FileOverride)) : new VhSceneEditsData();
                if (!Application.isEditor)
                {
                    string local = BuildPath();
                    if (File.Exists(local)) return VhSceneEditsData.FromJson(File.ReadAllText(local));
                }
#if UNITY_EDITOR
                string inProject = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", RepoPath);
                if (File.Exists(inProject)) return VhSceneEditsData.FromJson(File.ReadAllText(inProject));
#endif
                var asset = Resources.Load<TextAsset>(ResourcePath);
                if (asset != null) return VhSceneEditsData.FromJson(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("JAMKIT_VH opening edits couldn't be read (the opening plays as built): " + e.Message);
            }
            return new VhSceneEditsData();
        }

        /// <summary>This build's own copy (next to its data, in StreamingAssets).</summary>
        public static string BuildPath() => Path.Combine(Application.streamingAssetsPath, FileName);

        /// <summary>
        /// The repository the edits are committed in, found from here: a "VibeJamKit" folder beside one of the folders
        /// this build (or project) sits in, else the checkout it sits in. Null when there's none.
        /// </summary>
        public static string FindRepoFile()
        {
            try
            {
                var dir = new DirectoryInfo(Application.dataPath);
                string fallback = null;
                for (; dir != null; dir = dir.Parent)
                {
                    string sibling = Path.Combine(dir.FullName, "VibeJamKit");
                    if (Directory.Exists(Path.Combine(sibling, "Assets/JamKit/Templates/VisitingHours"))) return Path.Combine(sibling, RepoPath);
                    if (fallback == null && Directory.Exists(Path.Combine(dir.FullName, "Assets/JamKit/Templates/VisitingHours")))
                        fallback = Path.Combine(dir.FullName, RepoPath);
                }
                return fallback;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Saves <paramref name="data"/>: in a build, to its StreamingAssets and into the repository (when it's
        /// found); in the Unity editor, into the project. Returns where it went (one line each).</summary>
        public static List<string> Save(VhSceneEditsData data)
        {
            var wrote = new List<string>();
            string json = data.ToJson();
            void Write(string path)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                    File.WriteAllText(path, json);
                    wrote.Add(Path.GetFullPath(path));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("JAMKIT_VH opening edits: couldn't write " + path + ": " + e.Message);
                }
            }
            if (FileOverride != null)
            {
                Write(FileOverride);
                return wrote;
            }
            if (!Application.isEditor) Write(BuildPath());
            string repo = FindRepoFile();
            if (repo != null) Write(repo);
            foreach (var w in wrote) Debug.Log("JAMKIT_VH opening edits saved: " + w);
            return wrote;
        }

        // ---------- Applying and capturing ----------

        /// <summary>Applies saved overrides (the ones placed before or after the lighting); unknown ids are skipped with a warning.</summary>
        public void Apply(VhSceneEditsData data, bool afterLighting)
        {
            if (data == null) return;
            if (data.objects != null)
                foreach (var o in data.objects)
                {
                    if (o == null) continue;
                    var e = Find(o.id);
                    if (e == null || e.isShot)
                    {
                        if (afterLighting && !_warned.Contains(o.id))
                        {
                            _warned.Add(o.id);
                            Debug.LogWarning("JAMKIT_VH opening edits: nothing called \"" + o.id + "\" in the scene any more; that edit is skipped.");
                        }
                        continue;
                    }
                    if (e.afterLighting != afterLighting) continue;
                    Set(e, o.position, Quaternion.Euler(o.rotation), o.scale);
                }
            if (!afterLighting || data.shots == null) return;
            foreach (var sh in data.shots)
            {
                if (sh == null) continue;
                var e = Find(sh.id);
                if (e == null || !e.isShot)
                {
                    if (!_warned.Contains(sh.id))
                    {
                        _warned.Add(sh.id);
                        Debug.LogWarning("JAMKIT_VH opening edits: no shot called \"" + sh.id + "\" any more; that edit is skipped.");
                    }
                    continue;
                }
                e.fov = sh.fov > 1f ? sh.fov : e.defaultFov;
                Set(e, sh.position, Quaternion.Euler(sh.rotation), Vector3.one);
            }
        }

        readonly HashSet<string> _warned = new HashSet<string>();

        /// <summary>Everything as built, then <paramref name="data"/> (undo, redo, loading).</summary>
        public void ApplyAll(VhSceneEditsData data)
        {
            ResetAll();
            Apply(data, false);
            Apply(data, true);
            if (_stage.blanketDirty) VhStageBuilder.ReshapeBlanket(_stage);
        }

        /// <summary>Everything back where the builder put it.</summary>
        public void ResetAll()
        {
            foreach (var e in _all) Reset(e);
        }

        public void Reset(VhEditable e)
        {
            if (e.isShot) e.fov = e.defaultFov;
            Set(e, e.defaultPosition, e.defaultRotation, e.defaultScale);
        }

        /// <summary>Moves <paramref name="e"/> to a place in its parent and lets what follows it know.</summary>
        public void Set(VhEditable e, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            if (e == null || e.target == null) return;
            var t = e.target;
            Matrix4x4 before = t.localToWorldMatrix;
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            if (!e.isShot && e.canScale) t.localScale = localScale;
            Matrix4x4 delta = t.localToWorldMatrix * before.inverse;
            e.moved?.Invoke(delta);
        }

        /// <summary>Moves <paramref name="e"/> by a world move (the editor's handles), and what it carries along.</summary>
        public void MoveBy(VhEditable e, Matrix4x4 worldDelta, bool carry = true)
        {
            if (e == null || e.target == null) return;
            ToWorld(e, worldDelta * e.target.localToWorldMatrix);
            if (!carry) return;
            foreach (var id in e.carries)
            {
                var c = Find(id);
                if (c != null && c != e) ToWorld(c, worldDelta * c.target.localToWorldMatrix);
            }
        }

        void ToWorld(VhEditable e, Matrix4x4 world)
        {
            var parent = e.target.parent;
            Matrix4x4 local = parent != null ? parent.worldToLocalMatrix * world : world;
            Vector3 pos = local.GetColumn(3);
            Quaternion rot = local.rotation;
            Vector3 scale = local.lossyScale;
            if (!e.canScale || e.isShot) scale = e.target.localScale;
            Set(e, pos, rot, scale);
        }

        /// <summary>What differs from the built scene now (what the editor saves).</summary>
        public VhSceneEditsData Capture()
        {
            var d = new VhSceneEditsData();
            foreach (var e in _all)
            {
                var t = e.target;
                if (t == null) continue;
                bool moved = (t.localPosition - e.defaultPosition).sqrMagnitude > 1e-8f || Quaternion.Angle(t.localRotation, e.defaultRotation) > 0.01f;
                if (e.isShot)
                {
                    if (moved || Mathf.Abs(e.fov - e.defaultFov) > 0.01f)
                        d.shots.Add(new VhShotEdit { id = e.id, position = t.localPosition, rotation = t.localRotation.eulerAngles, fov = e.fov });
                }
                else if (moved || (t.localScale - e.defaultScale).sqrMagnitude > 1e-8f)
                    d.objects.Add(new VhObjectEdit { id = e.id, position = t.localPosition, rotation = t.localRotation.eulerAngles, scale = t.localScale });
            }
            return d;
        }

        // ---------- What can be moved ----------

        VhEditable Add(string id, string label, string group, Transform target, bool bedroom, string note = "", params string[] carries)
        {
            if (target == null || _byId.ContainsKey(id)) return null;
            var e = new VhEditable
            {
                id = id, label = label, group = group, note = note ?? "", target = target, bedroom = bedroom, carries = carries ?? new string[0],
                defaultPosition = target.localPosition, defaultRotation = target.localRotation, defaultScale = target.localScale,
            };
            _all.Add(e);
            _byId[id] = e;
            return e;
        }

        static Transform Child(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        static readonly List<Transform> Found = new List<Transform>();

        /// <summary>One handle over the parts (direct children of <paramref name="parent"/> with these names), standing at
        /// <paramref name="pivot"/>; null when none of them is there.</summary>
        static Transform Handle(Transform parent, string name, Vector3 pivot, params string[] parts)
        {
            Found.Clear();
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (Array.IndexOf(parts, c.name) >= 0) Found.Add(c);
            }
            if (Found.Count == 0) return null;
            var h = new GameObject(name).transform;
            h.SetParent(parent, false);
            h.localPosition = pivot;
            foreach (var c in Found) c.SetParent(h, true);
            return h;
        }

        const string Bedroom = "In the bedroom", Shots = "Shots (cameras)", Title = "At the title table", Lights = "Lights";
        const string FollowNote = "Each shot is one key: changing it changes only this shot, and the moves between shots follow.";

        /// <summary>Everything the opening editor can move, by stable ids (built after the sets and the shots).</summary>
        public static VhSceneEdits Register(VhStage s)
        {
            var r = new VhSceneEdits(s);
            var b = s.bedroom;
            // The bed with Nadia and her blanket: one handle at the bed's foot-print centre.
            var nadiaGo = s.nadia != null ? s.nadia.transform : null;
            string nadiaName = nadiaGo != null ? nadiaGo.name : "Nadia (model)";
            var bed = Handle(b, "Edit: the bed", new Vector3(0.2f, 0f, 0.61f), "Bed frame", "Headboard", "Mattress", "Pillow (hers)", "Pillow", "Blanket", nadiaName);
            if (bed != null) s.blanketOrigin = bed.localPosition;
            var eBed = r.Add("bed", "The bed (with Nadia and the blanket)", Bedroom, bed, true,
                "Nadia, her blanket and the shots from her eyes move with it. Keep the phone within her reach (about 45 cm from her shoulder).",
                "shot:pillow", "shot:nightstand", "shot:away");
            if (eBed != null) eBed.moved = d => CarryNadia(s, d);
            // Nadia: a handle at her eyes (her rig places her body from her eyes each time her pose changes, so the
            // handle, not her body, is what moves; her eyes and rest pose are carried along with it).
            var nadiaHandle = nadiaGo != null && bed != null
                ? Handle(bed, "Edit: Nadia", bed.InverseTransformPoint(b.TransformPoint(VhStageBuilder.Eyes)), nadiaName)
                : null;
            if (nadiaHandle != null)
            {
                var eN = r.Add("nadia", "Nadia", Bedroom, nadiaHandle, true,
                    "Moving her moves her eyes: the 'on her pillow', 'looking at the nightstand' and 'rolling away' shots follow. The blanket reshapes over her when you let go. Her arm and hand follow the phone: to change where her hand goes, move The phone (resting) or The phone (held in her hand).",
                    "shot:pillow", "shot:nightstand", "shot:away");
                if (eN != null)
                {
                    eN.canScale = false;
                    eN.moved = d =>
                    {
                        CarryNadia(s, d);
                        s.blanketDirty = true;
                    };
                }
            }
            var stand = Handle(b, "Edit: the nightstand", new Vector3(-0.9f, 0f, 1.36f), "nightstand", "Nightstand", "Nightstand drawer");
            r.Add("nightstand", "The nightstand (and what's on it)", Bedroom, stand, true,
                "Carries the lamp, the phone, the photo and the shot from above.", "lamp", "phone", "photo-frame", "shot:overhead");
            var lampPart = Child(b, "lamp") ?? Child(b, "Lamp base");
            if (lampPart != null) r.Add("lamp", "The lamp", Bedroom, Handle(b, "Edit: the lamp", lampPart.localPosition, "lamp", "Lamp base", "Lamp stem", "Lamp shade"), true);
            var rest = Child(b, VhStageBuilder.PhoneRestName);
            var ePhone = r.Add("phone", "The phone (resting)", Bedroom, rest, true,
                "Where it lies on the nightstand (before she picks it up and after she puts it down). She takes it by its bottom end, back to her palm: keep that end a few centimetres over the nightstand's edge so her fingers fit under it. Where she holds it up is 'The phone (held in her hand)'.");
            if (ePhone != null)
            {
                ePhone.canScale = false;
                ePhone.moved = d => s.SetPhoneRest(rest.position, rest.rotation);
            }
            r.Add("photo-frame", "The framed photo", Bedroom, Child(b, "Framed photo"), true);
            r.Add("chair", "The chair (and her scrubs)", Bedroom, Handle(b, "Edit: the chair", new Vector3(1.3f, 0f, -0.95f), "chair", "Chair (stand-in)", "Scrubs (folded)", "Scrubs (a leg hanging)"), true);
            r.Add("rug", "The rug", Bedroom, Handle(b, "Edit: the rug", new Vector3(0.25f, 0f, -1.05f), "rug", "Rug"), true);
            r.Add("bookcase", "The bookcase (and books)", Bedroom, Handle(b, "Edit: the bookcase", new Vector3(1.6f, 0f, 0.4f), "shelf", "Bookcase", "books", "Book"), true);
            r.Add("plant", "The plant", Bedroom, Handle(b, "Edit: the plant", new Vector3(1.45f, 0f, 1.38f), "plant", "Pot", "Plant"), true);
            r.Add("door", "The door", Bedroom, Handle(b, "Edit: the door", new Vector3(0.95f, 0f, -1.69f), "Door", "Door handle"), true);
            r.Add("streetlight", "The streetlight (and its light)", Lights,
                Handle(b, "Edit: the streetlight", new Vector3(-3.3f, 0.46f, 0.3f), "Streetlight pole", "Streetlight arm", "Streetlight lamp", "Streetlight (light)"), true,
                "The blinds' stripes it throws are worked out when the scene is built: press 'Rebuild with my edits' to see them follow.");
            r.Add("moon", "The moonlight", Lights, Child(b, "Moon (light)"), true, "Only its direction matters.");

            // The title table.
            var t = s.tableRoot;
            string[] props = { "tablet", "mug", "notepad", "pen", "badge" };
            string[] carried = new string[props.Length];
            for (int i = 0; i < props.Length; i++) carried[i] = "title-" + props[i];
            AddPiece(r, s, "title-table", "The table (and everything on it)", "table", "Carries the tablet, the mug, the notepad, the pen and the badge.", carried);
            AddPiece(r, s, "title-room", "The room behind the table (floor and wall)", "room", "");
            AddPiece(r, s, "title-tablet", "The tablet (Play)", "tablet", "It doesn't turn to face a moved title camera by itself: turn it.");
            AddPiece(r, s, "title-mug", VhStageBuilder.QuitVariant() == "despair" ? "The pill bottle (Give Up: the Despair route's Quit)" : "The coffee mug (Quit)", "mug",
                "The quit piece. On the Despair route it's the pill bottle, in this same place.");
            AddPiece(r, s, "title-notepad", "The notepad (Credits)", "notepad", "");
            AddPiece(r, s, "title-pen", "The pen", "pen", "");
            AddPiece(r, s, "title-badge", "The ID badge (Settings)", "badge", "");
            var sun = Child(t, VhStageBuilder.TableLampName);
            var eSun = r.Add("title-lamp", "The lamp over the table", Lights, sun, false, "The lighting places it over the table: your move is applied after that.");
            if (eSun != null) eSun.afterLighting = true;

            // The shots: a handle for each pose, in its set; moving it moves the pose (one key each).
            AddShot(r, s, "pillow", "Shot: on her pillow (the start)", b, FollowNote);
            AddShot(r, s, "nightstand", "Shot: looking at the nightstand", b, FollowNote + " Where she holds the phone up follows this shot.");
            AddShot(r, s, "away", "Shot: rolling away", b, FollowNote);
            AddShot(r, s, "overhead", "Shot: the phone from above", b, FollowNote + " Its slow push-in starts from here.");
            AddShot(r, s, "table", "Shot: the title table", t, FollowNote);
            AddHeldPhone(r, s);
            return r;
        }

        /// <summary>Where she holds the phone up: a handle in the "looking at the nightstand" shot's frame (so it follows that
        /// shot, and the bed and Nadia that carry it), which the stage's held pose reads. Listed after the resting phone.</summary>
        static void AddHeldPhone(VhSceneEdits r, VhStage s)
        {
            var shot = r.Find("shot:nightstand");
            var resting = r.Find("phone");
            if (shot == null || s.phone == null) return;
            var h = new GameObject("Edit: the phone held").transform;
            h.SetParent(shot.target, false);
            h.localPosition = VhStage.HeldOffset;
            h.localRotation = VhStage.HeldTurn;
            s.heldPhone = h;
            var e = r.Add("phone-held", "The phone (held in her hand)", Bedroom, h, true,
                "Where she holds it up in front of her, from when she lifts it to when she puts it down (one place for all those beats). It's kept relative to her eyes: it moves with the 'looking at the nightstand' shot, the bed and Nadia. Her hand follows it, but her arm reaches only so far: past that the phone stops at her reach, and the status line says so.");
            if (e == null) return;
            e.canScale = false;
            if (resting != null)
            {
                r._all.Remove(e);
                r._all.Insert(r._all.IndexOf(resting) + 1, e);
            }
        }

        static void AddPiece(VhSceneEdits r, VhStage s, string id, string label, string piece, string note, params string[] carries)
        {
            var p = s.table != null ? s.table.Find(piece) : null;
            if (p == null) return;
            var e = r.Add(id, label, Title, p.transform, false, note, carries);
            if (e != null) e.moved = d => p.Rebase();
        }

        static void AddShot(VhSceneEdits r, VhStage s, string pose, string label, Transform space, string note)
        {
            int i = s.PoseIndex(pose);
            if (i < 0 || space == null) return;
            var h = new GameObject("Edit: shot " + pose).transform;
            h.SetParent(space, false);
            h.SetPositionAndRotation(s.posePositions[i], s.poseRotations[i]);
            var e = r.Add("shot:" + pose, label, Shots, h, space == s.bedroom, note);
            if (e == null) return;
            e.isShot = true;
            e.canScale = false;
            e.shotIndex = i;
            e.fov = e.defaultFov = s.poseFov[i];
            e.moved = d =>
            {
                s.posePositions[i] = h.position;
                s.poseRotations[i] = h.rotation;
                s.poseFov[i] = e.fov;
                if (s.Pose == pose) s.RefreshPose();
            };
        }

        /// <summary>Her eyes, shoulder and rest pose follow a move of the bed (or of her).</summary>
        static void CarryNadia(VhStage s, Matrix4x4 d)
        {
            s.shoulder = d.MultiplyPoint3x4(s.shoulder);
            if (s.nadia != null) s.nadia.Carry(d);
        }

        /// <summary>The lamps the lighting adds itself (after it ran): each one by its name.</summary>
        public void RegisterLights()
        {
            // What the lighting places itself (the morning sun) is as built where the lighting left it: that's its default.
            foreach (var e in _all)
                if (e.afterLighting && e.target != null)
                {
                    e.defaultPosition = e.target.localPosition;
                    e.defaultRotation = e.target.localRotation;
                    e.defaultScale = e.target.localScale;
                }
            var seen = new HashSet<Transform>();
            foreach (var e in _all) if (e.target != null) seen.Add(e.target);
            foreach (var l in _stage.GetComponentsInChildren<Light>(true))
            {
                var t = l.transform;
                if (seen.Contains(t) || (_stage.phone != null && t.IsChildOf(_stage.phone))) continue;
                // The tablet's glow follows the tablet every frame (the lighting rig): not placed by hand.
                var rig = _stage.GetComponent<VhLightingRig>();
                if (rig != null && rig.tabletGlow != null && t == rig.tabletGlow.transform) continue;
                bool inside = false;
                foreach (var e in _all)
                    if (e.target != null && t.IsChildOf(e.target)) inside = true;
                if (inside) continue;
                string name = t.name.Replace(" (light)", "");
                string id = "light:" + name.ToLowerInvariant().Replace(' ', '-');
                if (_byId.ContainsKey(id)) continue;
                var ed = Add(id, "Light: " + name, Lights, t, _stage.bedroom != null && t.IsChildOf(_stage.bedroom),
                    "Added by the lighting; your move is applied after it places it.");
                if (ed != null) ed.afterLighting = true;
            }
        }
    }
}
