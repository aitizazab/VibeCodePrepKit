using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>A fixed camera for the judges' stills (docs/SKYLINE-DESIGN.md 9.4): disabled, rendered by stage B to
    /// still-&lt;scene&gt;-&lt;shotName&gt;.png at 1920x1080 after the scene's recording.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SkylineStillCamera : MonoBehaviour
    {
        public string shotName = "shot";
    }
}
