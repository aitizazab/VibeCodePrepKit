using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// Put on a collider to change how movement treats it: walls are runnable unless this says no
    /// (or the controller's "Only Marked Walls" is on and this says yes).
    /// </summary>
    public sealed class MovementSurface : MonoBehaviour
    {
        [Tooltip("Can be wallrun on.")]
        public bool runnable = true;
    }
}
