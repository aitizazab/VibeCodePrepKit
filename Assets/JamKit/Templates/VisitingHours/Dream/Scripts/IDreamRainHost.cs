using JamKit.Movement;

namespace JamKit.DreamEscape
{
    /// <summary>A scene run that has a front door into the rain (<see cref="DreamUmbrella"/> reads it): the dream escape's
    /// end, and the Night 2 house's front door.</summary>
    public interface IDreamRainHost
    {
        MovementController Player { get; }
        /// <summary>The player's distance along the route.</summary>
        float S { get; }
        bool AutopilotOn { get; }
        bool Respawning { get; }
        /// <summary>The door, the umbrella and the rain path (null until the run has built its course).</summary>
        DreamRainSpot RainSpot { get; }
    }
}
