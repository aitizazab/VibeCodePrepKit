using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The corridor's front door: a pair of leaves on their hinges (the left jamb and the right). The game swings them open
    /// when she bursts through (<see cref="Open"/>, 0..1) and shuts them for a new run (<see cref="Close"/>).
    /// </summary>
    public sealed class EscapeDoubleDoor : MonoBehaviour
    {
        public Transform left, right;
        /// <summary>How far each leaf swings (degrees).</summary>
        public float swing = 105f;

        public void Open(float k)
        {
            float a = swing * Mathf.Clamp01(k);
            if (left != null) left.localRotation = Quaternion.Euler(0f, -a, 0f);
            if (right != null) right.localRotation = Quaternion.Euler(0f, a, 0f);
        }

        public void Close() => Open(0f);
    }
}
