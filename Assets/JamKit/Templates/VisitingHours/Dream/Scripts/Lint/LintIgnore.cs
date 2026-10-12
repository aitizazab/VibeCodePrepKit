using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The geometry lint ("JamKit/QA/Geometry Lint") skips this GameObject and everything under it: its renderers
    /// and colliders take part in no check (they still block the lint's rays, as they block the player). Say why in
    /// <see cref="reason"/>; the reason is listed in the lint report so an ignore never hides a problem silently.
    /// </summary>
    public sealed class LintIgnore : MonoBehaviour
    {
        [Tooltip("Why the lint should skip this (listed in the report).")]
        public string reason = "";
    }
}
