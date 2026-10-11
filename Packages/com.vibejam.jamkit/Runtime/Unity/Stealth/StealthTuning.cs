using UnityEngine;

namespace JamKit.Stealth
{
    /// <summary>
    /// A stealth tuning asset: every number of the stealth kit (core <see cref="StealthDef"/>, explained in
    /// docs/STEALTH.md). Seekers can share one; edits in Play mode apply at once. Assets > Create > JamKit > Stealth Tuning.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Stealth Tuning", fileName = "StealthTuning")]
    public sealed class StealthTuning : ScriptableObject
    {
        public StealthDef def = new StealthDef();
    }
}
