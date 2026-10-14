using System;

namespace JamKit.Skyline
{
    /// <summary>
    /// The floor's phase machine: Lobby -> LiftRide -> Floor -> Finale -> Done (<see cref="FloorPhase"/>). Each transition
    /// has one legal source phase; an out-of-order call changes nothing and returns false. A wreck in LiftRide, Floor or
    /// Finale returns to Lobby and raises <see cref="Entered"/>(Lobby), which the director uses to reset everything (the
    /// first Lobby is the starting phase and raises nothing, so Entered(Lobby) always means "restarted").
    /// </summary>
    public sealed class FloorFlow
    {
        /// <summary>Raised after the phase changed, with the new phase.</summary>
        public event Action<FloorPhase> Entered;

        public FloorPhase Phase { get; private set; } = FloorPhase.Lobby;

        /// <summary>The player stepped into the lift (Lobby to LiftRide).</summary>
        public bool SteppedIntoLift() => Go(FloorPhase.Lobby, FloorPhase.LiftRide);

        /// <summary>The lift doors opened onto the food court: the drop (LiftRide to Floor). The wave clock starts here.</summary>
        public bool DoorsOpened() => Go(FloorPhase.LiftRide, FloorPhase.Floor);

        /// <summary>The third asset was towed (Floor to Finale): waves stop, the west lift opens.</summary>
        public bool AllTowed() => Go(FloorPhase.Floor, FloorPhase.Finale);

        /// <summary>The Unit's feet reached the west lift (Finale to Done).</summary>
        public bool ReachedExit() => Go(FloorPhase.Finale, FloorPhase.Done);

        /// <summary>The Unit was wrecked (LiftRide, Floor or Finale back to Lobby). Ignored in Lobby and Done.</summary>
        public bool Wrecked()
        {
            if (Phase != FloorPhase.LiftRide && Phase != FloorPhase.Floor && Phase != FloorPhase.Finale) return false;
            Phase = FloorPhase.Lobby;
            Entered?.Invoke(Phase);
            return true;
        }

        bool Go(FloorPhase from, FloorPhase to)
        {
            if (Phase != from) return false;
            Phase = to;
            Entered?.Invoke(to);
            return true;
        }
    }
}
