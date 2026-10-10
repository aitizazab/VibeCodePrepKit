using System;
using UnityEngine;

namespace JamKit.Tests.Play
{
    /// <summary>A test probe that runs after every speed visual's LateUpdate (docs/SPEED-FEEL.md), so a frame's boom,
    /// foot plant and footprint are seen together. In its own file so Unity honours its execution order.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SpeedFeelProbe : MonoBehaviour
    {
        public Action tick;
        void LateUpdate() => tick?.Invoke();
    }
}
