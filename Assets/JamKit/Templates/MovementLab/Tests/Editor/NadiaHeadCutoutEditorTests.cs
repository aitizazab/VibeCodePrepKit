using JamKit.Movement;
using JamKit.MovementLab.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.MovementLab.Tests
{
    /// <summary>
    /// Nadia's first-person body never shows her own head (the creator saw it looking down, 2026-10-12: the neck pitched
    /// the whole head forward and down around the neck, out of a cut-away centred on the camera). Her real model is posed
    /// by <see cref="MixamoBodyRig"/> at every pitch from 85° up to 85° down, standing, sliding and mantling, with the camera
    /// on her eyes and with the camera's landing dip ahead of the body's; each vertex is skinned on the CPU the way the GPU
    /// does it. Every vertex of her head and hair is inside the cut-away the visible material gets, and looking 70° down
    /// her chest is outside it.
    /// </summary>
    public class NadiaHeadCutoutEditorTests
    {
        const float Yaw = 30f;

        sealed class Body
        {
            public GameObject go;
            public MixamoBodyRig rig;
            public Transform cam;
            public Vector3[] verts, world;
            public BoneWeight[] weights;
            public Matrix4x4[] bindposes, skin;
            public Transform[] bones;
            public bool[] head, chest;
            public int headCount, chestCount;
        }

        static bool Named(Transform t, string name)
        {
            string n = t.name;
            return n == name || n.EndsWith(":" + name, System.StringComparison.Ordinal) || n.EndsWith("_" + name, System.StringComparison.Ordinal);
        }

        static Body Make()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MovementLabBuilder.NadiaPath);
            Assert.That(prefab, Is.Not.Null, MovementLabBuilder.NadiaPath);
            var b = new Body { go = Object.Instantiate(prefab) };
            b.rig = b.go.AddComponent<MixamoBodyRig>();
            b.rig.model = b.go.transform;
            Assert.That(b.rig.Bind(), Is.True, "Mixamo bones found");
            b.cam = new GameObject("test camera").transform;

            Assert.That(b.rig.VisibleRenderers.Count, Is.GreaterThan(0));
            var smr = b.rig.VisibleRenderers[0];
            var mesh = smr.sharedMesh;
            b.verts = mesh.vertices;
            b.weights = mesh.boneWeights;
            b.bindposes = mesh.bindposes;
            b.bones = smr.bones;
            b.skin = new Matrix4x4[b.bones.Length];
            b.world = new Vector3[b.verts.Length];
            Transform headBone = null, neckBone = null, chestBone = null;
            foreach (var t in b.bones)
            {
                if (t == null) continue;
                if (Named(t, "Head")) headBone = t;
                if (Named(t, "Neck")) neckBone = t;
                if (Named(t, "Spine2")) chestBone = t;
            }
            Assert.That(headBone, Is.Not.Null);
            Assert.That(neckBone, Is.Not.Null);
            Assert.That(chestBone, Is.Not.Null);
            var hs = headBone.lossyScale;
            float headRadius = b.rig.headRadius * Mathf.Max(Mathf.Abs(hs.x), Mathf.Max(Mathf.Abs(hs.y), Mathf.Abs(hs.z)));
            TestContext.WriteLine($"Her head, hair and neck: a {headRadius:0.000} m sphere");
            Assert.That(headRadius, Is.InRange(0.1f, 0.3f), "her head, hair and neck measured (m)");
            var inHead = new bool[b.bones.Length];
            int chestIdx = System.Array.IndexOf(b.bones, chestBone);
            for (int i = 0; i < b.bones.Length; i++) inHead[i] = b.bones[i] != null && b.bones[i].IsChildOf(headBone);

            // The bind pose: her chest is what's skinned mostly to Spine2 and sits more than 6 cm below the neck joint.
            Skin(b);
            float chestTop = neckBone.position.y - 0.06f;
            b.head = new bool[b.verts.Length];
            b.chest = new bool[b.verts.Length];
            for (int v = 0; v < b.verts.Length; v++)
            {
                var w = b.weights[v];
                float hw = (inHead[w.boneIndex0] ? w.weight0 : 0f) + (inHead[w.boneIndex1] ? w.weight1 : 0f)
                    + (inHead[w.boneIndex2] ? w.weight2 : 0f) + (inHead[w.boneIndex3] ? w.weight3 : 0f);
                b.head[v] = hw >= 0.5f;
                if (b.head[v]) b.headCount++;
                b.chest[v] = w.boneIndex0 == chestIdx && w.weight0 >= 0.5f && b.world[v].y < chestTop;
                if (b.chest[v]) b.chestCount++;
            }
            Assert.That(b.headCount, Is.GreaterThan(1000), "her head and hair (skinned mostly to the Head bone or its children)");
            Assert.That(b.chestCount, Is.GreaterThan(20), "her chest (skinned mostly to Spine2)");
            return b;
        }

        static void Destroy(Body b)
        {
            if (b == null) return;
            if (b.go != null) Object.DestroyImmediate(b.go);
            if (b.cam != null) Object.DestroyImmediate(b.cam.gameObject);
        }

        /// <summary>Linear blend skinning, as the GPU does it: world = sum of weight x bone x bindpose x vertex.</summary>
        static void Skin(Body b)
        {
            for (int i = 0; i < b.bones.Length; i++)
                b.skin[i] = b.bones[i] != null ? b.bones[i].localToWorldMatrix * b.bindposes[i] : Matrix4x4.identity;
            for (int v = 0; v < b.verts.Length; v++)
            {
                var w = b.weights[v];
                var p = b.verts[v];
                b.world[v] = b.skin[w.boneIndex0].MultiplyPoint3x4(p) * w.weight0 + b.skin[w.boneIndex1].MultiplyPoint3x4(p) * w.weight1
                    + b.skin[w.boneIndex2].MultiplyPoint3x4(p) * w.weight2 + b.skin[w.boneIndex3].MultiplyPoint3x4(p) * w.weight3;
            }
        }

        enum Kind { Standing, Sliding, Mantling }

        /// <summary>The feel's own pose for a second of standing, sliding (her recline and legs fully in) or mantling a 1.2 m ledge.</summary>
        static BodyFeelPose PoseFor(Kind kind)
        {
            var feel = new BodyFeel(new BodyFeelDef());
            var input = new BodyFeelInput { dt = 1f / 60f, motion = 1f, sprintSpeed = 9f };
            if (kind == Kind.Sliding)
            {
                input.mode = MoveMode.Slide;
                input.speed = 11f;
                input.velocityBody = new NVec3(0f, 0f, 11f);
                input.crouchHeld = true;
            }
            else if (kind == Kind.Mantling)
            {
                input.mode = MoveMode.Mantle;
                input.speed = 3f;
                input.velocityBody = new NVec3(0f, 0f, 3f);
                input.hasLedge = true;
                input.ledgeHeight = 1.2f;
                input.ledgeLeft = new NVec3(-0.25f, -0.3f, 0.45f);
                input.ledgeRight = new NVec3(0.25f, -0.3f, 0.45f);
            }
            else input.mode = MoveMode.Grounded;
            BodyFeelPose pose = null;
            for (int i = 0; i < 60; i++) pose = feel.Tick(input);
            if (kind == Kind.Sliding) Assert.That(pose.torsoPitch, Is.LessThan(-20f), "the slide's recline is in");
            return pose;
        }

        /// <summary>Poses her: the camera at the eyes (plus <paramref name="cameraDip"/> along its own up, the camera's landing
        /// dip ahead of the body's), looking <paramref name="pitch"/> degrees down; skins her.</summary>
        static void PoseAt(Body b, BodyFeelPose pose, Kind kind, float pitch, float cameraDip, bool third = false)
        {
            Vector3 feet = new Vector3(2f, 0.5f, -1f);
            var yaw = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 eyes = feet + Vector3.up * (kind == Kind.Sliding ? 0.8f : 1.5f);
            float nod = kind == Kind.Mantling ? 3f : 0f; // MovementCamera's mantle nod
            var camRot = Quaternion.Euler(pitch + nod, Yaw, 0f);
            b.cam.SetPositionAndRotation(eyes + camRot * new Vector3(0f, cameraDip, 0f), camRot);
            b.rig.Apply(pose, new MovementBodyFrame
            {
                camera = b.cam,
                bodyRotation = yaw,
                feet = feet,
                eyes = eyes,
                pitch = pitch,
                thirdPerson = third,
            });
            Skin(b);
        }

        /// <summary>The farthest any vertex in <paramref name="mask"/> pokes out of the cut-away (m; negative: all inside),
        /// and the nearest one to it from outside (m; negative: some inside).</summary>
        static void Against(Body b, bool[] mask, out float worstOut, out float nearestOutside)
        {
            var c = b.rig.Cutout;
            Vector3 centre = new Vector3(c.x, c.y, c.z);
            worstOut = float.MinValue;
            nearestOutside = float.MaxValue;
            for (int v = 0; v < b.world.Length; v++)
            {
                if (!mask[v]) continue;
                float d = Vector3.Distance(b.world[v], centre) - c.w;
                worstOut = Mathf.Max(worstOut, d);
                nearestOutside = Mathf.Min(nearestOutside, d);
            }
        }

        [Test]
        public void HerHeadAndHairAreInsideTheCutAwayAtEveryPitch()
        {
            Body b = null;
            try
            {
                b = Make();
                var block = new MaterialPropertyBlock();
                int id = Shader.PropertyToID(b.rig.cutoutProperty);
                foreach (Kind kind in System.Enum.GetValues(typeof(Kind)))
                {
                    var pose = PoseFor(kind);
                    foreach (float dip in new[] { 0f, -0.12f })
                    {
                        float worst = float.MinValue, worstPitch = 0f, widest = 0f;
                        for (int pitch = -85; pitch <= 85; pitch += 5)
                        {
                            PoseAt(b, pose, kind, pitch, dip);
                            Against(b, b.head, out float outBy, out _);
                            if (outBy > worst) { worst = outBy; worstPitch = pitch; }
                            widest = Mathf.Max(widest, b.rig.Cutout.w);
                            // The visible material gets exactly this cut-away.
                            b.rig.VisibleRenderers[0].GetPropertyBlock(block);
                            Assert.That(Vector4.Distance(block.GetVector(id), b.rig.Cutout), Is.LessThan(1e-5f), "the material's cut-away");
                        }
                        TestContext.WriteLine($"{kind}, camera dip {dip:0.00} m: the head's farthest vertex {-worst:0.000} m inside the cut-away (at {worstPitch}°), widest cut-away {widest:0.000} m");
                        Assert.That(worst, Is.LessThan(0f), $"{kind}, camera dip {dip}: every head and hair vertex inside the cut-away (worst at {worstPitch}°)");
                    }
                }
            }
            finally { Destroy(b); }
        }

        [Test]
        public void LookingDownSheSeesHerChest()
        {
            Body b = null;
            try
            {
                b = Make();
                foreach (Kind kind in System.Enum.GetValues(typeof(Kind)))
                {
                    PoseAt(b, PoseFor(kind), kind, 70f, 0f);
                    Against(b, b.chest, out _, out float nearest);
                    TestContext.WriteLine($"{kind}, 70° down: cut-away {b.rig.Cutout.w:0.000} m, her chest at least {nearest:0.000} m outside it");
                    Assert.That(nearest, Is.GreaterThan(0f), kind + ": every chest vertex outside the cut-away");
                }
            }
            finally { Destroy(b); }
        }

        [Test]
        public void TheOldFullNodShowedHerHead()
        {
            // The test's own reach: with the neck following the whole pitch and a 0.24 m cut-away around the camera (before
            // 2026-10-12), looking 70° down puts part of her head outside it.
            Body b = null;
            try
            {
                b = Make();
                b.rig.neckFollow = 1f;
                b.rig.neckFollowMax = 180f;
                PoseAt(b, PoseFor(Kind.Standing), Kind.Standing, 70f, 0f);
                Vector3 cam = b.cam.position;
                int outside = 0;
                for (int v = 0; v < b.world.Length; v++)
                    if (b.head[v] && Vector3.Distance(b.world[v], cam) > 0.24f) outside++;
                TestContext.WriteLine($"The old way, 70° down: {outside} of {b.headCount} head vertices outside 0.24 m of the camera");
                Assert.That(outside, Is.GreaterThan(0));
            }
            finally { Destroy(b); }
        }

        [Test]
        public void ThirdPersonShowsHerHead()
        {
            Body b = null;
            try
            {
                b = Make();
                PoseAt(b, PoseFor(Kind.Standing), Kind.Standing, 40f, 0f, third: true);
                Assert.That(b.rig.CutoutRadius, Is.EqualTo(0f), "no cut-away in the orbit view");
                Assert.That(b.rig.Cutout.w, Is.EqualTo(0f));
            }
            finally { Destroy(b); }
        }
    }
}
