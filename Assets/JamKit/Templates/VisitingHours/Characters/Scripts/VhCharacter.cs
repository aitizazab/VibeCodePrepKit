using UnityEngine;

namespace JamKit.VisitingHours.Characters
{
    /// <summary>
    /// The one component other agents use to drive a 3D person (docs/redesign/people.md "The component").
    ///
    ///   var lena = VhCharacter.Spawn("Lena", pos, rot);   // Resources/People/Lena.prefab
    ///   lena.Play("idle");                                // a looping state, cross-faded
    ///   lena.PlayOnce("wave");                             // a one-shot, then back to idle (or to the given state)
    ///   lena.LookAt(player.transform);  lena.StopLooking();
    ///   lena.Talk(true);  lena.Say(2.5f);                  // mouth on/off, or on for a while
    ///   lena.SetExpression("hurt");
    ///
    /// Animation states are named after the clips in Models/Animations (the list in people.md); every prefab's controller
    /// has the same names, so code can ask any character for "idle", "talk", "sit". A missing state warns once and is
    /// ignored. Look-at turns the chest, neck and head after the animation (LateUpdate), clamped and eased, so it works
    /// with any clip. No per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VhCharacter : MonoBehaviour
    {
        public const string ResourceFolder = "People/";

        [Tooltip("The name the cast uses (also the prefab's name under Resources/People).")]
        public string characterName;
        public Animator animator;
        public VhFace face;
        public string idleState = "idle";
        [Tooltip("Default cross-fade, seconds.")]
        public float fade = 0.25f;

        [Header("Look at")]
        [Range(0f, 1f)] public float lookWeight = 1f;
        public float maxYaw = 70f;
        public float maxPitch = 30f;
        [Tooltip("How fast the head turns towards the target (higher = snappier).")]
        public float lookSharpness = 5f;
        [Tooltip("Share of the turn taken by the chest, neck and head (they add up to 1).")]
        public Vector3 lookShares = new Vector3(0.2f, 0.3f, 0.5f);

        [Header("Life")]
        [Tooltip("Breathing, degrees of chest pitch. 0 = off.")]
        public float breathe = 1.2f;
        public float breathsPerMinute = 14f;

        Transform chest, neck, head;
        Transform lookTarget;
        Vector3 lookPoint;
        bool looking;
        float yaw, pitch;
        int oneShotHash, returnHash;
        bool warnedMissing;
        float breathPhase;
        float talkUntil = -1f;

        public bool IsLooking => looking;
        public bool IsTalking => face != null && face.IsTalking;
        public Transform Head => head;

        /// <summary>Instantiates Resources/People/&lt;name&gt;.prefab. Returns null (with an error) if there is none.</summary>
        public static VhCharacter Spawn(string name, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var prefab = Resources.Load<GameObject>(ResourceFolder + name);
            if (prefab == null) { Debug.LogError("VhCharacter: no prefab Resources/" + ResourceFolder + name); return null; }
            var go = Instantiate(prefab, position, rotation, parent);
            go.name = name;
            return go.GetComponent<VhCharacter>();
        }

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (face == null) face = GetComponentInChildren<VhFace>();
            CacheBones();
            breathPhase = Random.value * 6.2832f;
        }

        void CacheBones()
        {
            if (animator == null || !animator.isHuman) return;
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        }

        // ---- Animation ----

        public bool Has(string state) => animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, Animator.StringToHash(state));

        /// <summary>Cross-fades to a state (looping states keep looping). fadeSeconds &lt; 0 uses the default.</summary>
        public bool Play(string state, float fadeSeconds = -1f)
        {
            oneShotHash = 0;
            return CrossFade(state, fadeSeconds);
        }

        /// <summary>Plays a state once, then cross-fades to thenState (the idle when null).</summary>
        public bool PlayOnce(string state, string thenState = null, float fadeSeconds = -1f)
        {
            if (!CrossFade(state, fadeSeconds)) return false;
            oneShotHash = Animator.StringToHash(state);
            returnHash = Animator.StringToHash(string.IsNullOrEmpty(thenState) ? idleState : thenState);
            return true;
        }

        bool CrossFade(string state, float fadeSeconds)
        {
            if (animator == null) return false;
            int hash = Animator.StringToHash(state);
            if (animator.runtimeAnimatorController == null || !animator.HasState(0, hash))
            {
                if (!warnedMissing) { Debug.LogWarning("VhCharacter " + characterName + ": no animation state '" + state + "'"); warnedMissing = true; }
                return false;
            }
            animator.CrossFadeInFixedTime(hash, fadeSeconds < 0f ? fade : fadeSeconds, 0);
            return true;
        }

        /// <summary>The state playing on the base layer (its short name hash).</summary>
        public int CurrentStateHash => animator == null ? 0 : animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        public bool IsPlaying(string state) => CurrentStateHash == Animator.StringToHash(state);

        // ---- Look at ----

        public void LookAt(Transform target) { lookTarget = target; looking = target != null; }

        public void LookAt(Vector3 worldPoint) { lookTarget = null; lookPoint = worldPoint; looking = true; }

        public void StopLooking() { looking = false; lookTarget = null; }

        // ---- Face ----

        /// <summary>Mouth moving (true) or still (false). Cancels a running Say.</summary>
        public void Talk(bool on) { talkUntil = -1f; if (face != null) face.SetTalking(on); }

        /// <summary>Talks for the given seconds, then stops.</summary>
        public void Say(float seconds) { if (face == null) return; face.SetTalking(true); talkUntil = Time.time + seconds; }

        public bool SetExpression(string expression, float fadeSeconds = -1f) => face != null && face.SetExpression(expression, fadeSeconds);

        public void Blink() { if (face != null) face.Blink(); }

        // ---- Per frame ----

        void Update()
        {
            if (talkUntil >= 0f && Time.time >= talkUntil) { talkUntil = -1f; if (face != null) face.SetTalking(false); }
            if (oneShotHash != 0 && animator != null && !animator.IsInTransition(0))
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == oneShotHash && info.normalizedTime >= 0.97f)
                {
                    oneShotHash = 0;
                    if (animator.HasState(0, returnHash)) animator.CrossFadeInFixedTime(returnHash, fade, 0);
                }
            }
        }

        void LateUpdate() { Pose(Time.deltaTime); }

        /// <summary>Applies look-at and breathing on top of the animated pose (LateUpdate calls it; captures call it in edit mode).</summary>
        public void Pose(float dt)
        {
            if (head == null) { CacheBones(); if (head == null) return; }
            var root = transform;

            float wantYaw = 0f, wantPitch = 0f;
            if (looking)
            {
                Vector3 p = lookTarget != null ? lookTarget.position : lookPoint;
                Vector3 local = root.InverseTransformDirection(p - head.position);
                if (local.sqrMagnitude > 1e-4f)
                {
                    wantYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    wantPitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                    // Behind the shoulder: don't snap across; ease back to the front instead.
                    if (Mathf.Abs(wantYaw) > maxYaw + 40f) { wantYaw = 0f; wantPitch = 0f; }
                    wantYaw = Mathf.Clamp(wantYaw, -maxYaw, maxYaw) * lookWeight;
                    wantPitch = Mathf.Clamp(wantPitch, -maxPitch, maxPitch) * lookWeight;
                }
            }
            float k = 1f - Mathf.Exp(-lookSharpness * Mathf.Max(0f, dt));
            yaw = Mathf.Lerp(yaw, wantYaw, k);
            pitch = Mathf.Lerp(pitch, wantPitch, k);

            breathPhase += dt * breathsPerMinute / 60f * 6.2832f;
            if (breathPhase > 6.2832f) breathPhase -= 6.2832f;
            float breath = breathe * Mathf.Sin(breathPhase);

            Vector3 up = root.up, right = root.right;
            if (chest != null) chest.rotation = Turn(lookShares.x, up, right, -breath) * chest.rotation;
            if (neck != null) neck.rotation = Turn(lookShares.y, up, right, breath * 0.5f) * neck.rotation;
            head.rotation = Turn(neck == null ? lookShares.y + lookShares.z : lookShares.z, up, right, 0f) * head.rotation;
        }

        Quaternion Turn(float share, Vector3 up, Vector3 right, float extraPitch)
        {
            // Unity: a positive angle about +right pitches the face down, so looking up is a negative angle.
            return Quaternion.AngleAxis(yaw * share, up) * Quaternion.AngleAxis(-pitch * share + extraPitch, right);
        }
    }
}
