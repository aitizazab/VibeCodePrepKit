using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JamKit.SkylineRepo
{
    /// <summary>A fixed camera for the judges' stills (docs/SKYLINE-DESIGN.md 9.4): disabled, rendered by stage B to
    /// still-&lt;scene&gt;-&lt;shotName&gt;.png at 1920x1080 after the scene's recording.
    /// The Camera beside it is kept disabled at all times (Reset, Awake, OnEnable, and every frame; not OnValidate, where toggling components draws SendMessage warnings), so a
    /// still camera can never take over the Game view that the Recorder records; and it is kept a Base camera, since an
    /// Overlay camera can't render on its own.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SkylineStillCamera : MonoBehaviour
    {
        public string shotName = "shot";

        /// <summary>Disables the Camera and makes it a Base camera; returns it.</summary>
        public Camera Enforce()
        {
            var cam = GetComponent<Camera>();
            if (cam == null) return null;
            if (cam.enabled) cam.enabled = false;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null && data.renderType != CameraRenderType.Base) data.renderType = CameraRenderType.Base;
            return cam;
        }

        void Reset() => Enforce();
        void Awake() => Enforce();
        void OnEnable() => Enforce();
        void LateUpdate() => Enforce();
    }
}
