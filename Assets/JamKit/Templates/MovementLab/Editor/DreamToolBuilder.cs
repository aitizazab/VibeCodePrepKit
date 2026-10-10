using System.Collections.Generic;
using JamKit.Movement;
using JamKit.Shell;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.MovementLab.EditorTools
{
    /// <summary>
    /// Builds the Movement Lab's dream tool chapter (core <see cref="DreamToolCourse"/>; its static floors, frames and walls
    /// are course pieces the <see cref="MovementLabBuilder"/> builds like any other): the pieces the tool changes (pastel
    /// glass tiles in the doorways, the bridge and the walls the sockets build, the pink and blue platforms), the targets
    /// (gold sockets, colour switches, a lock frame around each doorway), the gate and the finish arch, the tool's beam
    /// and hand pulse, and the <see cref="DreamTool"/> and <see cref="DreamToolRun"/> components. Placeholder look,
    /// kept clean: gold means "use this" (sockets, the stripes on runnable walls), pink and blue are the two colours,
    /// a solid colour is a grid and the other colour a faint ghost.
    /// </summary>
    public static class DreamToolBuilder
    {
        const string Mats = MovementLabBuilder.Root + "/Materials";
        public static readonly Color Pink = new Color(1f, 0.62f, 0.78f), Blue = new Color(0.55f, 0.74f, 1f);

        sealed class Look
        {
            public Material[] glass;
            public Material built, gold, pinkSolid, blueSolid, pinkGhost, blueGhost, socket, switchPink, switchBlue, halo, beam, pulse, gate, finish;
        }

        static Material Mat(string name, string shaderName)
        {
            string path = Mats + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Dream tool: shader " + shaderName + " not found.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Grid(string name, Color color, Color line, float glow = 0f)
        {
            var mat = Mat(name, "JamKit/MovementLab/Grid");
            mat.SetColor("_Color", color);
            mat.SetColor("_LineColor", line);
            mat.SetFloat("_Glow", glow);
            return mat;
        }

        /// <summary>Translucent with a bright rim (the Gauntlet ghost's shader): glass, ghosts, the beam.</summary>
        static Material Glass(string name, Color color, Color rim, float power = 2.5f)
        {
            var mat = Mat(name, "JamKit/MovementLab/Ghost");
            mat.SetColor("_Color", color);
            mat.SetColor("_RimColor", rim);
            mat.SetFloat("_RimPower", power);
            return mat;
        }

        static Look Materials()
        {
            var l = new Look
            {
                glass = new[]
                {
                    Glass("Dream_Glass_Lilac", new Color(0.78f, 0.7f, 1f, 0.55f), new Color(0.95f, 0.9f, 1f, 0.9f), 1.6f),
                    Glass("Dream_Glass_Mint", new Color(0.62f, 0.95f, 0.85f, 0.55f), new Color(0.9f, 1f, 0.96f, 0.9f), 1.6f),
                    Glass("Dream_Glass_Peach", new Color(1f, 0.8f, 0.68f, 0.55f), new Color(1f, 0.95f, 0.9f, 0.9f), 1.6f),
                },
                built = Grid("Grid_DreamBuilt", new Color(0.72f, 0.64f, 0.92f), new Color(0.9f, 0.86f, 1f)),
                pinkSolid = Grid("Grid_DreamPink", Pink, new Color(1f, 0.85f, 0.92f)),
                blueSolid = Grid("Grid_DreamBlue", Blue, new Color(0.84f, 0.92f, 1f)),
                pinkGhost = Glass("Dream_PinkGhost", new Color(Pink.r, Pink.g, Pink.b, 0.1f), new Color(1f, 0.75f, 0.88f, 0.45f), 3f),
                blueGhost = Glass("Dream_BlueGhost", new Color(Blue.r, Blue.g, Blue.b, 0.1f), new Color(0.7f, 0.85f, 1f, 0.45f), 3f),
                socket = Grid("Grid_DreamSocket", new Color(1f, 0.92f, 0.7f), new Color(1f, 1f, 0.9f), 1f),
                switchPink = Grid("Grid_DreamSwitchPink", Pink, new Color(1f, 0.92f, 0.96f), 1f),
                switchBlue = Grid("Grid_DreamSwitchBlue", Blue, new Color(0.92f, 0.96f, 1f), 1f),
                halo = Glass("Dream_Halo", new Color(1f, 0.9f, 0.6f, 0.08f), new Color(1f, 0.88f, 0.5f, 0.85f), 2f),
                beam = Glass("Dream_Beam", new Color(1f, 0.95f, 0.8f, 0.85f), new Color(1f, 1f, 1f, 1f), 1f),
                pulse = Glass("Dream_Pulse", new Color(1f, 0.95f, 0.85f, 0.6f), new Color(1f, 1f, 1f, 1f), 1.2f),
            };
            l.gold = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/Grid_Gold.mat") ?? Grid("Grid_Gold", new Color(1f, 0.78f, 0.32f), new Color(1f, 0.95f, 0.72f), 1f);
            l.gate = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/Grid_Checkpoint.mat") ?? l.socket;
            l.finish = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/Grid_Finish.mat") ?? l.socket;
            return l;
        }

        static GameObject Box(string name, Transform parent, Material mat, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        public static void Build(DreamToolCourse d, Transform root, MovementLabGame game)
        {
            var look = Materials();
            AssetDatabase.SaveAssets();
            var parent = new GameObject("Dream tool").transform;
            parent.SetParent(root, false);

            var pieceViews = new DreamToolPieceView[d.pieces.Count];
            for (int i = 0; i < d.pieces.Count; i++) pieceViews[i] = Piece(d, i, parent, look);
            var targetViews = new DreamToolTargetView[d.targets.Count];
            for (int i = 0; i < d.targets.Count; i++) targetViews[i] = Target(d, i, parent, look);

            Arch(parent, "Dream tool gate", d.run.gateS, d.startFloorY, look.gate, look.gold);
            Arch(parent, "Dream tool finish", d.run.finishS, d.finishFloorY, look.finish, look.finish);

            // The tool's beam and the pulse at her hand (no colliders; shown only for a moment after a shot).
            var beam = Box("Dream tool beam", parent, look.beam);
            var pulse = Box("Dream tool pulse", parent, look.pulse, PrimitiveType.Sphere);
            beam.SetActive(false);
            pulse.SetActive(false);

            var tool = game.gameObject.AddComponent<DreamTool>();
            var run = game.gameObject.AddComponent<DreamToolRun>();
            tool.game = game;
            tool.run = run;
            tool.body = game.player != null && game.player.cam != null ? game.player.cam.GetComponent<MovementBody>() : null;
            tool.beam = beam.transform;
            tool.beamRenderer = beam.GetComponent<MeshRenderer>();
            tool.pulse = pulse.transform;
            tool.pulseRenderer = pulse.GetComponent<MeshRenderer>();
            run.game = game;
            run.tool = tool;
            run.pieceViews = pieceViews;
            run.targetViews = targetViews;
        }

        static DreamToolPieceView Piece(DreamToolCourse d, int index, Transform parent, Look look)
        {
            var p = d.pieces[index];
            Vector3 size = U(p.Size), center = U(p.Center);
            var go = new GameObject("D " + p.name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            go.AddComponent<MovementSurface>().runnable = p.runnable;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            visual.localScale = size;
            var renderers = new List<Renderer>();
            Material solid = null, ghost = null;
            switch (p.role)
            {
                case DreamPieceRole.Tile:
                {
                    // A pane of pastel glass tiles, 2 across and 3 up, with thin gaps.
                    for (int x = 0; x < 2; x++)
                        for (int y = 0; y < 3; y++)
                        {
                            var t = Box("Tile " + (x + 1) + "," + (y + 1), visual, look.glass[(x + y) % look.glass.Length]);
                            t.transform.localPosition = new Vector3(-0.25f + 0.5f * x, -1f / 3f + y / 3f, 0f);
                            t.transform.localScale = new Vector3(0.5f - 0.04f / size.x, 1f / 3f - 0.04f / size.y, 1f);
                            renderers.Add(t.GetComponent<MeshRenderer>());
                        }
                    break;
                }
                case DreamPieceRole.Built:
                {
                    var b = Box("Body", visual, look.built);
                    renderers.Add(b.GetComponent<MeshRenderer>());
                    if (p.runnable)
                    {
                        // The gold stripes on its face toward the route, as on every runnable wall (the floor is 6 m below its top).
                        float laneX = d.course.LaneAt(p.Center.Z);
                        float face = laneX < p.Center.X ? -0.5f - 0.03f / size.x : 0.5f + 0.03f / size.x;
                        float floor = p.max.Y - 6f;
                        foreach (var (h, th) in new[] { (1.45f, 0.42f), (2.05f, 0.1f) })
                        {
                            var s = Box("Stripe", visual, look.gold);
                            s.transform.localPosition = new Vector3(face, (floor + h - center.y) / size.y, 0f);
                            s.transform.localScale = new Vector3(0.06f / size.x, th / size.y, (size.z - 0.4f) / size.z);
                            renderers.Add(s.GetComponent<MeshRenderer>());
                        }
                    }
                    else
                    {
                        // Gold edges along the bridge's sides.
                        for (int side = -1; side <= 1; side += 2)
                        {
                            var s = Box("Edge", visual, look.gold);
                            s.transform.localPosition = new Vector3(side * (0.5f - 0.1f / size.x), 0.5f + 0.03f / size.y, 0f);
                            s.transform.localScale = new Vector3(0.2f / size.x, 0.06f / size.y, 1f);
                            renderers.Add(s.GetComponent<MeshRenderer>());
                        }
                    }
                    break;
                }
                default:
                {
                    bool pink = p.role == DreamPieceRole.Pink;
                    solid = pink ? look.pinkSolid : look.blueSolid;
                    ghost = pink ? look.pinkGhost : look.blueGhost;
                    var b = Box("Body", visual, solid);
                    renderers.Add(b.GetComponent<MeshRenderer>());
                    break;
                }
            }
            var view = go.AddComponent<DreamToolPieceView>();
            view.index = index;
            view.role = p.role;
            view.rises = p.rises;
            view.solidCollider = col;
            view.visual = visual;
            view.renderers = renderers.ToArray();
            view.solidMaterial = solid;
            view.ghostMaterial = ghost;
            return view;
        }

        static DreamToolTargetView Target(DreamToolCourse d, int index, Transform parent, Look look)
        {
            var t = d.targets[index];
            var go = new GameObject("D Target " + (index + 1) + ": " + t.name);
            go.transform.SetParent(parent, false);
            go.transform.position = U(t.center);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            var view = go.AddComponent<DreamToolTargetView>();
            view.index = index;
            view.kind = t.kind;
            var renderers = new List<Renderer>();
            switch (t.kind)
            {
                case DreamTargetKind.Tile:
                {
                    // The lock frame: four gold bars around the doorway (the pane itself is the piece).
                    var frame = new GameObject("Lock frame");
                    frame.transform.SetParent(go.transform, false);
                    float hw = 2.05f, hh = 1.85f;
                    for (int i = 0; i < 4; i++)
                    {
                        bool horizontal = i < 2;
                        var bar = Box("Bar", frame.transform, look.gold);
                        float sign = i % 2 == 0 ? -1f : 1f;
                        bar.transform.localPosition = horizontal ? new Vector3(0f, sign * hh, -0.35f) : new Vector3(sign * hw, 0f, -0.35f);
                        bar.transform.localScale = horizontal ? new Vector3(hw * 2f + 0.12f, 0.12f, 0.06f) : new Vector3(0.12f, hh * 2f, 0.06f);
                    }
                    view.lockFrame = frame;
                    break;
                }
                case DreamTargetKind.Socket:
                {
                    // A gold ring facing the route with a glowing core; four notches show it turning.
                    var visual = new GameObject("Socket").transform;
                    visual.SetParent(go.transform, false);
                    visual.localRotation = Quaternion.Euler(90f, 0f, 0f); // local Y along the route: it faces you
                    var ring = Box("Ring", visual, look.gold, PrimitiveType.Cylinder);
                    ring.transform.localScale = new Vector3(1.5f, 0.04f, 1.5f);
                    var core = Box("Core", visual, look.socket, PrimitiveType.Sphere);
                    core.transform.localScale = Vector3.one * 0.7f;
                    renderers.Add(ring.GetComponent<MeshRenderer>());
                    renderers.Add(core.GetComponent<MeshRenderer>());
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f;
                        var notch = Box("Notch", visual, look.socket);
                        notch.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.78f, 0f, Mathf.Sin(a) * 0.78f);
                        notch.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f);
                        renderers.Add(notch.GetComponent<MeshRenderer>());
                    }
                    view.visual = visual;
                    view.lockFrame = Halo(go.transform, look, 2.1f);
                    break;
                }
                default:
                {
                    // A diamond in the colour a hit will make solid (set at run time by the state).
                    var visual = new GameObject("Switch").transform;
                    visual.SetParent(go.transform, false);
                    var gem = Box("Diamond", visual, look.switchBlue);
                    gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    gem.transform.localScale = Vector3.one * 1.1f;
                    renderers.Add(gem.GetComponent<MeshRenderer>());
                    view.visual = visual;
                    view.switchMaterials = new[] { look.switchPink, look.switchBlue };
                    view.lockFrame = Halo(go.transform, look, 2.4f);
                    break;
                }
            }
            view.renderers = renderers.ToArray();
            if (view.lockFrame != null) view.lockFrame.SetActive(false);
            return view;
        }

        static GameObject Halo(Transform parent, Look look, float size)
        {
            var h = Box("Lock halo", parent, look.halo, PrimitiveType.Sphere);
            h.transform.localScale = Vector3.one * size;
            return h;
        }

        static void Arch(Transform parent, string name, float z, float floorTop, Material posts, Material line)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var post = Box(name + (side < 0 ? " (left post)" : " (right post)"), parent, posts);
                post.transform.position = new Vector3(side * 9.2f, floorTop + 2.5f, z);
                post.transform.localScale = new Vector3(0.5f, 5f, 0.5f);
            }
            var beam = Box(name + " (beam)", parent, posts);
            beam.transform.position = new Vector3(0f, floorTop + 5.2f, z);
            beam.transform.localScale = new Vector3(18.9f, 0.5f, 0.5f);
            var l = Box(name + " (line)", parent, line);
            l.transform.position = new Vector3(0f, floorTop + 0.03f, z);
            l.transform.localScale = new Vector3(16f, 0.05f, 0.35f);
        }
    }
}
