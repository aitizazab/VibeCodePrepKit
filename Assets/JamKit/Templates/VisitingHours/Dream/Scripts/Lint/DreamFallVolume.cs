using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Marks a trigger collider as the place where a fall is caught (a pit's floor, the void under a level). The
    /// geometry lint ("JamKit/QA/Geometry Lint", check no-fall-volume) counts an edge whose drop ends in one of these
    /// as handled. Put it on the GameObject that holds the trigger collider. A trigger named with "Fall volume" or
    /// "Kill" counts too. The marker does nothing at runtime by itself: the scene's game decides what a fall costs.
    /// </summary>
    public sealed class DreamFallVolume : MonoBehaviour
    {
        [Tooltip("What happens when you fall in here (for the reader; the lint only needs the marker).")]
        public string note = "";
    }
}
