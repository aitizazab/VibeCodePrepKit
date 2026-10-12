using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>What a body rig needs each frame besides the pose (all world space unless noted).</summary>
    public struct MovementBodyFrame
    {
        /// <summary>The first-person camera (arm poses are in its space).</summary>
        public Transform camera;
        /// <summary>The body's yaw frame (leg poses are in its space, from the soles).</summary>
        public Quaternion bodyRotation;
        /// <summary>The soles, interpolated (MovementController.RenderPosition).</summary>
        public Vector3 feet;
        /// <summary>Where the eyes go: the controller's head (before the camera's bob and dip).</summary>
        public Vector3 eyes;
        /// <summary>The view's pitch (degrees, down +).</summary>
        public float pitch;
        /// <summary>True while the debug orbit camera shows the body from outside (show the head).</summary>
        public bool thirdPerson;
        /// <summary>The peek: degrees the spine bends sideways toward the lean (+ right). The eyes are already where the lean
        /// put them, so the hips take what the bend doesn't.</summary>
        public float lean;
    }

    /// <summary>
    /// The seam between <see cref="MovementBody"/> (the feel: where the hands, feet and torso go) and a
    /// real body. Put a subclass on the body model and set it as the MovementBody's rig; while it's
    /// <see cref="Ready"/>, the placeholder capsules hide and <see cref="Apply"/> poses the model each
    /// frame. A template wires its own character (Visiting Hours wires Nadia's; see
    /// <see cref="MixamoBodyRig"/> for any Mixamo-boned model).
    /// </summary>
    public abstract class MovementBodyRig : MonoBehaviour
    {
        /// <summary>True when the model is bound and can be posed (false: the capsules stay).</summary>
        public abstract bool Ready { get; }

        /// <summary>Poses the body: arms from <paramref name="pose"/> (camera space), legs (body space), torso, dip, breath.</summary>
        public abstract void Apply(JamKit.Movement.BodyFeelPose pose, in MovementBodyFrame frame);

        /// <summary>Where a hand actually is (world; 0 left, 1 right), to check the pose was reached.</summary>
        public abstract Vector3 HandPosition(int side);

        /// <summary>Where a foot (ankle) actually is (world).</summary>
        public abstract Vector3 FootPosition(int side);

        /// <summary>The head's centre (world): what the first-person camera must not see.</summary>
        public abstract Vector3 HeadPosition { get; }

        /// <summary>The radius of the sphere the visible body is cut away inside in first person (m; 0: nothing is cut away).</summary>
        public abstract float CutoutRadius { get; }

        /// <summary>Where an arm starts (world; 0 left, 1 right) and how far its hand can reach from there (m), so a
        /// contact spot can be kept within reach. False: unknown (the body uses its own estimate).</summary>
        public virtual bool ArmReach(int side, out Vector3 shoulder, out float reach)
        {
            shoulder = Vector3.zero;
            reach = 0f;
            return false;
        }
    }
}
