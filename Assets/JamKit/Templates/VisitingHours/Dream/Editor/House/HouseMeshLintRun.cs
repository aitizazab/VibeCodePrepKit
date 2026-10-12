using System.Collections.Generic;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Runs core <see cref="HouseMeshLint"/> on what the rebuild actually drew (house-1.md: the box lint passed while the
    /// rendered models lay on their sides, floated and went through each other). <see cref="Begin"/> starts recording
    /// (every <see cref="MeshBuilder"/> made while it records logs its boxes and shapes with their part); <see cref="Finish"/>
    /// adds the fitted models (their real bounds in the house's frame, their upright and scale checks, and each mesh of a
    /// model touching the rest of it), the memories and the cut-outs, runs the lint and logs every issue as an error
    /// (<c>JAMKIT_HOUSE_MESHLINT</c>), then the count. The rebuild exits 3 when there are any.
    /// </summary>
    public static class HouseMeshLintRun
    {
        public static readonly List<HouseDrawn> Drawn = new List<HouseDrawn>();
        static NVec3 N(Vector3 v) => new NVec3(v.x, v.y, v.z);

        public static void Begin()
        {
            Drawn.Clear();
            HouseFurnishing.Fitted.Clear();
            MeshBuilder.DefaultRecord = Drawn;
        }

        /// <summary>Stops recording (before building things that aren't the house: the escape, the stand-ins).</summary>
        public static void Pause() => MeshBuilder.DefaultRecord = null;

        /// <summary>Adds the models and the free things, runs the lint, logs it; returns the number of issues.</summary>
        public static int Finish(DreamHouse h, Transform houseRoot, IEnumerable<GameObject> freeThings, IEnumerable<GameObject> hungThings)
        {
            MeshBuilder.DefaultRecord = null;
            var extra = new List<string>();
            foreach (var f in HouseFurnishing.Fitted)
            {
                if (f.go == null) continue;
                var b = HouseFurnishing.LocalBounds(houseRoot, f.go);
                Drawn.Add(new HouseDrawn
                {
                    name = f.name + " (" + f.id + ")", kind = f.hangs ? DrawnKind.Hanging : DrawnKind.Model, owner = f.owner,
                    min = N(b.min), max = N(b.max), realSize = N(f.spec.dims), fittedSize = N(f.modelSize),
                });
                FloatingMeshes(f, houseRoot, extra);
            }
            if (freeThings != null) foreach (var go in freeThings) AddThing(go, houseRoot, DrawnKind.Free);
            if (hungThings != null) foreach (var go in hungThings) AddThing(go, houseRoot, DrawnKind.Hanging);
            var issues = HouseMeshLint.Check(h.parts, Drawn);
            foreach (var i in issues) Debug.LogError("JAMKIT_HOUSE_MESHLINT " + i);
            foreach (var e in extra) Debug.LogError("JAMKIT_HOUSE_MESHLINT [floating-mesh] " + e);
            int n = issues.Count + extra.Count;
            int models = 0;
            foreach (var d in Drawn) if (d.kind == DrawnKind.Model) models++;
            if (n == 0) Debug.Log($"JAMKIT_HOUSE_MESHLINT 0 issues in {Drawn.Count} drawn things ({models} models)");
            else Debug.LogError($"JAMKIT_HOUSE_MESHLINT FAIL: {n} issues in {Drawn.Count} drawn things ({models} models): see the lines above");
            return n;
        }

        static void AddThing(GameObject go, Transform root, DrawnKind kind)
        {
            if (go == null) return;
            bool was = go.activeSelf;
            go.SetActive(true);
            var b = HouseFurnishing.LocalBounds(root, go);
            go.SetActive(was);
            if (b.size == Vector3.zero) return;
            Drawn.Add(new HouseDrawn { name = go.name, kind = kind, owner = -1, min = N(b.min), max = N(b.max) });
        }

        /// <summary>A model made of several meshes: each must touch another of them or stand on the box's floor (the
        /// den TV's box hung in the air over its stand).</summary>
        static void FloatingMeshes(HouseFurnishing.FittedModel f, Transform root, List<string> issues)
        {
            var rs = f.go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length < 2) return;
            var bs = new Bounds[rs.Length];
            for (int i = 0; i < rs.Length; i++) bs[i] = HouseFurnishing.LocalBounds(root, rs[i].gameObject);
            const float touch = HouseMeshLint.Touch;
            for (int i = 0; i < rs.Length; i++)
            {
                if (bs[i].size == Vector3.zero) continue;
                if (Mathf.Abs(bs[i].min.y - f.boxMin.y) < touch || f.hangs && Mathf.Abs(bs[i].max.y - f.boxMax.y) < touch) continue;
                var grown = bs[i];
                grown.Expand(touch * 2f);
                bool ok = false;
                for (int j = 0; j < rs.Length && !ok; j++) if (j != i && bs[j].size != Vector3.zero && grown.Intersects(bs[j])) ok = true;
                if (!ok) issues.Add($"\"{rs[i].name}\" of \"{f.name}\" ({f.id}) touches nothing of its model");
            }
        }
    }
}
