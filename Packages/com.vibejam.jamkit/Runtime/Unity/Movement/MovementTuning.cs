using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// A movement tuning asset: every number of the Titanfall-style movement (core <see cref="MoveDef"/>,
    /// explained in docs/MOVEMENT.md). Several scenes can share one; edits in Play mode apply at once.
    /// Create one with Assets > Create > JamKit > Movement Tuning.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Movement Tuning", fileName = "MovementTuning")]
    public sealed class MovementTuning : ScriptableObject
    {
        public MoveDef def = new MoveDef();
    }
}
