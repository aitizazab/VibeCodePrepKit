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

        [Tooltip("Renderers switched off while this shot renders (the lift lobby's shutter, so the still can look into the lobby).")]
        public Renderer[] hideDuringShot;
        bool[] _wasEnabled;

        /// <summary>Called by stage B's capture just before it renders this still: hides <see cref="hideDuringShot"/> and shows
        /// the shot's display bot (<see cref="SkylineDisplayBot"/>), if it has one. Everything is put back by <see cref="EndShot"/>
        /// in the same editor tick, so no frame of the game sees the change.</summary>
        public void BeginShot()
        {
            if (hideDuringShot != null)
            {
                _wasEnabled = new bool[hideDuringShot.Length];
                for (int i = 0; i < hideDuringShot.Length; i++)
                {
                    if (hideDuringShot[i] == null) continue;
                    _wasEnabled[i] = hideDuringShot[i].enabled;
                    hideDuringShot[i].enabled = false;
                }
            }
            var display = GetComponent<SkylineDisplayBot>();
            if (display != null) display.Show();
        }

        /// <summary>Undoes <see cref="BeginShot"/>.</summary>
        public void EndShot()
        {
            var display = GetComponent<SkylineDisplayBot>();
            if (display != null) display.Hide();
            if (hideDuringShot != null && _wasEnabled != null)
                for (int i = 0; i < hideDuringShot.Length && i < _wasEnabled.Length; i++)
                    if (hideDuringShot[i] != null) hideDuringShot[i].enabled = _wasEnabled[i];
            _wasEnabled = null;
        }

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
