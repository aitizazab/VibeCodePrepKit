using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Makes everything under this GameObject one "object" for the geometry lint's butting-in check: parts of one
    /// built thing (a house whose wall boxes overlap at the corners, a prop made of several primitives) may overlap
    /// each other without a finding. Overlaps with anything outside the group are still reported. A GameObject whose
    /// name ends with "[group]" counts the same, and so does a prefab instance's outermost root.
    /// </summary>
    public sealed class LintGroup : MonoBehaviour
    {
        [Tooltip("Why its parts may overlap (for the reader).")]
        public string note = "";
    }
}
