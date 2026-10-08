using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>A timed list of spawns. The next wave starts when this one is cleared, or after Max Duration.</summary>
    [CreateAssetMenu(menuName = "JamKit/Bullet Hell/Wave", fileName = "Wave")]
    public sealed class WaveDef : ScriptableObject
    {
        public List<WaveSpawn> spawns = new List<WaveSpawn>();
        [Tooltip("Seconds before the next wave comes even if enemies remain (0 = wait until cleared).")]
        public float maxDuration = 14f;
    }
}
