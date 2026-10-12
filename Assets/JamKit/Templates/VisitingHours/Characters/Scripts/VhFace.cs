using System;
using UnityEngine;

namespace JamKit.VisitingHours.Characters
{
    /// <summary>
    /// A character's face: expressions, talking and blinking (docs/redesign/people.md "Faces").
    ///
    /// Two backends, either or both:
    /// - Blendshapes on a skinned mesh (<see cref="shapes"/>): each expression is a set of blendshape weights; talking
    ///   drives <see cref="mouthOpenShape"/>, blinking <see cref="blinkShape"/>.
    /// - A face decal (<see cref="decal"/>): a renderer hugging the face whose texture is an atlas, one row per expression
    ///   (row 0 = the top row = <see cref="expressions"/>[0]) and <see cref="decalColumns"/> columns:
    ///   mouth closed, mouth half open, mouth open, then the same three with the eyes closed. The atlas cell is picked
    ///   through a MaterialPropertyBlock (_BaseMap_ST), so no material is copied.
    ///
    /// The cast uses both at once (docs/redesign/people.md "Faces", the hybrid cartoon face): blendshapes move the jaw, the
    /// cheeks, the mouth corners and the brow ridge; the decal, a skinned copy of the face's front carrying the same
    /// blendshapes (listed in <see cref="followers"/>), carries the drawn brows, eyes and mouth (the model's own painted
    /// features are painted out of its texture), one atlas row per expression. Weights and the decal cell are only
    /// written when they change.
    ///
    /// Talking is a syllable rhythm (a new mouth target every 70-150 ms, eased), not lip-sync to audio.
    /// Nothing here allocates per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VhFace : MonoBehaviour
    {
        [Serializable]
        public struct ShapeWeight
        {
            public string shape;
            [Range(0f, 100f)] public float weight;
        }

        [Serializable]
        public sealed class Expression
        {
            public string name = "neutral";
            public ShapeWeight[] shapes = new ShapeWeight[0];
            [NonSerialized] public int[] indices;
        }

        [Header("Expressions (the first is the default)")]
        public Expression[] expressions = { new Expression() };
        [Tooltip("Seconds to blend from one expression to the next.")]
        public float expressionFade = 0.2f;

        [Header("Blendshape backend")]
        public SkinnedMeshRenderer shapes;
        [Tooltip("Meshes that copy the face's blendshape weights by name (the face decal): they move with it.")]
        public SkinnedMeshRenderer[] followers = new SkinnedMeshRenderer[0];
        public string mouthOpenShape = "MouthOpen";
        public string blinkShape = "Blink";

        [Header("Decal backend")]
        public Renderer decal;
        public int decalColumns = 6;
        public int decalRows = 1;

        [Header("Talking and blinking")]
        [Range(0f, 1f)] public float talkStrength = 0.85f;
        public Vector2 syllableSeconds = new Vector2(0.07f, 0.15f);
        public Vector2 blinkEverySeconds = new Vector2(2.2f, 5.5f);
        public float blinkSeconds = 0.13f;
        public bool blinking = true;

        int current, previous;
        float blend = 1f, fadeSeconds = 0.2f;
        bool talking;
        float mouth, mouthTarget, nextSyllable;
        float nextBlink, blinkUntil;
        int mouthIndex = -1, blinkIndex = -1;
        float[] weights, written;
        float[][] followerWritten;
        int lastCell = -1;
        int[][] followerMap;
        MaterialPropertyBlock block;
        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

        public bool IsTalking => talking;
        public string CurrentExpression => expressions != null && current < expressions.Length ? expressions[current].name : "";
        public int ExpressionCount => expressions == null ? 0 : expressions.Length;

        void Awake() { Cache(); }

        void Cache()
        {
            if (shapes != null && shapes.sharedMesh != null)
            {
                var mesh = shapes.sharedMesh;
                weights = new float[mesh.blendShapeCount];
                written = new float[mesh.blendShapeCount];
                for (int i = 0; i < written.Length; i++) written[i] = float.NaN;
                mouthIndex = string.IsNullOrEmpty(mouthOpenShape) ? -1 : mesh.GetBlendShapeIndex(mouthOpenShape);
                blinkIndex = string.IsNullOrEmpty(blinkShape) ? -1 : mesh.GetBlendShapeIndex(blinkShape);
                if (expressions != null)
                    foreach (var e in expressions)
                    {
                        e.indices = new int[e.shapes == null ? 0 : e.shapes.Length];
                        for (int i = 0; i < e.indices.Length; i++) e.indices[i] = mesh.GetBlendShapeIndex(e.shapes[i].shape);
                    }
                // Each follower's blendshape index for each of ours (-1 = it has none of that name).
                followerMap = new int[followers == null ? 0 : followers.Length][];
                followerWritten = new float[followerMap.Length][];
                for (int f = 0; f < followerMap.Length; f++)
                {
                    followerWritten[f] = new float[mesh.blendShapeCount];
                    for (int i = 0; i < mesh.blendShapeCount; i++) followerWritten[f][i] = float.NaN;
                    var fm = followers[f] != null ? followers[f].sharedMesh : null;
                    followerMap[f] = new int[mesh.blendShapeCount];
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                        followerMap[f][i] = fm == null ? -1 : fm.GetBlendShapeIndex(mesh.GetBlendShapeName(i));
                }
            }
            ScheduleBlink(Time.time);
        }

        /// <summary>Index of an expression by name (case-insensitive), or -1.</summary>
        public int IndexOf(string expressionName)
        {
            if (expressions == null || string.IsNullOrEmpty(expressionName)) return -1;
            for (int i = 0; i < expressions.Length; i++)
                if (string.Equals(expressions[i].name, expressionName, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>Blends to the named expression. Returns false (and keeps the current one) if there is none.</summary>
        public bool SetExpression(string expressionName, float fade = -1f)
        {
            int i = IndexOf(expressionName);
            if (i < 0) return false;
            if (i == current) return true;
            previous = current;
            current = i;
            fadeSeconds = fade < 0f ? expressionFade : fade;
            blend = fadeSeconds <= 0f ? 1f : 0f;
            return true;
        }

        public void SetTalking(bool on)
        {
            if (talking == on) return;
            talking = on;
            nextSyllable = 0f;
        }

        /// <summary>Blinks now (e.g. on a beat in the line).</summary>
        public void Blink() { blinkUntil = Time.time + blinkSeconds; ScheduleBlink(blinkUntil); }

        /// <summary>Blinks at a given clock time (captures drive the face with their own clock through <see cref="Tick"/>).</summary>
        public void BlinkAt(float now) { blinkUntil = now + blinkSeconds; ScheduleBlink(blinkUntil); }

        /// <summary>Restarts the talking and blinking rhythm on a given clock (captures; deterministic with Random.InitState).</summary>
        public void ResetClock(float now)
        {
            if (weights == null && shapes != null) Cache();
            nextSyllable = now;
            blinkUntil = -1f;
            ScheduleBlink(now);
            ForgetWritten();
        }

        void ScheduleBlink(float from) { nextBlink = from + UnityEngine.Random.Range(blinkEverySeconds.x, blinkEverySeconds.y); }

        void Update() { Tick(Time.time, Time.deltaTime); }

        /// <summary>Advances the face by dt at time now (Update calls it; captures call it directly in edit mode).</summary>
        public void Tick(float now, float dt)
        {
            if (weights == null && shapes != null) Cache();   // edit mode (captures): Awake has not run
            if (blend < 1f) blend = fadeSeconds <= 0f ? 1f : Mathf.Min(1f, blend + dt / fadeSeconds);

            if (talking)
            {
                if (now >= nextSyllable)
                {
                    // Open, half, closed in a loose rhythm; a closed beat now and then reads as word breaks.
                    float r = UnityEngine.Random.value;
                    mouthTarget = r < 0.18f ? 0f : r < 0.55f ? 0.5f : 1f;
                    nextSyllable = now + UnityEngine.Random.Range(syllableSeconds.x, syllableSeconds.y);
                }
            }
            else mouthTarget = 0f;
            mouth = Mathf.MoveTowards(mouth, mouthTarget * talkStrength, dt * 9f);

            if (blinking && now >= nextBlink && now >= blinkUntil) { blinkUntil = now + blinkSeconds; ScheduleBlink(blinkUntil); }
            bool eyesClosed = now < blinkUntil;

            ApplyShapes(eyesClosed);
            ApplyDecal(eyesClosed);
        }

        /// <summary>Shows a fixed face (captures and editor previews): an expression, a mouth opening 0-1, eyes open or closed.</summary>
        public void Preview(int expressionIndex, float mouthOpen, bool eyesClosed)
        {
            if (weights == null && shapes != null) Cache();
            current = previous = Mathf.Max(0, expressionIndex);
            blend = 1f;
            mouth = Mathf.Clamp01(mouthOpen);
            ForgetWritten();
            ApplyShapes(eyesClosed);
            ApplyDecal(eyesClosed);
        }

        /// <summary>Forgets what was last written, so the next apply writes every weight and the decal cell again.</summary>
        public void ForgetWritten()
        {
            lastCell = -1;
            if (written != null) for (int i = 0; i < written.Length; i++) written[i] = float.NaN;
            if (followerWritten != null) foreach (var fw in followerWritten) for (int i = 0; i < fw.Length; i++) fw[i] = float.NaN;
        }

        void OnEnable() { ForgetWritten(); }

        void ApplyShapes(bool eyesClosed)
        {
            if (shapes == null || weights == null) return;
            Array.Clear(weights, 0, weights.Length);
            if (expressions != null && expressions.Length > 0)
            {
                Accumulate(expressions[Mathf.Clamp(previous, 0, expressions.Length - 1)], 1f - blend);
                Accumulate(expressions[Mathf.Clamp(current, 0, expressions.Length - 1)], blend);
            }
            if (mouthIndex >= 0) weights[mouthIndex] = Mathf.Max(weights[mouthIndex], mouth * 100f);
            if (blinkIndex >= 0 && eyesClosed) weights[blinkIndex] = 100f;
            // Only the weights that changed are written (each SetBlendShapeWeight dirties the skinned mesh).
            for (int i = 0; i < weights.Length; i++)
            {
                float w = weights[i];
                if (Mathf.Abs(w - written[i]) < 0.01f) continue;   // NaN (never written) always fails the test
                shapes.SetBlendShapeWeight(i, w);
                written[i] = w;
            }
            if (followerMap == null) return;
            for (int f = 0; f < followerMap.Length; f++)
            {
                var fr = followers[f];
                if (fr == null) continue;
                var map = followerMap[f];
                var fw = followerWritten[f];
                for (int i = 0; i < map.Length; i++)
                {
                    if (map[i] < 0) continue;
                    float w = weights[i];
                    if (Mathf.Abs(w - fw[i]) < 0.01f) continue;
                    fr.SetBlendShapeWeight(map[i], w);
                    fw[i] = w;
                }
            }
        }

        void Accumulate(Expression e, float k)
        {
            if (k <= 0f || e.indices == null) return;
            for (int i = 0; i < e.indices.Length; i++)
                if (e.indices[i] >= 0) weights[e.indices[i]] += e.shapes[i].weight * k;
        }

        void ApplyDecal(bool eyesClosed)
        {
            if (decal == null || decalColumns <= 0 || decalRows <= 0) return;
            int row = Mathf.Clamp(blend < 0.5f ? previous : current, 0, decalRows - 1);
            int col = (mouth < 0.25f ? 0 : mouth < 0.7f ? 1 : 2) + (eyesClosed && decalColumns >= 6 ? 3 : 0);
            int cell = row * decalColumns + col;
            if (cell == lastCell) return;   // the property block is only touched when the cell changes
            lastCell = cell;
            float w = 1f / decalColumns, h = 1f / decalRows;
            // Row 0 is the top of the atlas; UV v grows upwards.
            var st = new Vector4(w, h, col * w, 1f - (row + 1) * h);
            if (block == null) block = new MaterialPropertyBlock();
            decal.GetPropertyBlock(block);
            block.SetVector(BaseMapSt, st);
            block.SetVector(MainTexSt, st);
            decal.SetPropertyBlock(block);
        }
    }
}
