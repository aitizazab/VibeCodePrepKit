using UnityEngine;

namespace JamKit.DeepBlockLab
{
    /// <summary>A prop that belongs to one of the deep block's per-night switches (S1-S8, or 8 = the washing-line
    /// sheets): it's there only when that switch is open (<see cref="presentWhenOpen"/>) or only when it's shut.
    /// <see cref="DeepBlockGame"/> turns them on and off by night.</summary>
    public sealed class BlockSwitchProp : MonoBehaviour
    {
        public int switchIndex;
        public bool presentWhenOpen = true;

        public void Apply(bool[] open)
        {
            bool on = switchIndex < 0 || open == null || switchIndex >= open.Length || open[switchIndex] == presentWhenOpen;
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }
    }
}
