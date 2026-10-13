using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>Marks a renderer that is meant to have no collider (glass panels, signs, the scrap cache's crate): the
    /// arena lint (SkylineLint rule 1) leaves it, and everything under it, alone.</summary>
    [DisallowMultipleComponent]
    public sealed class SkylineNoCollider : MonoBehaviour
    {
    }
}
