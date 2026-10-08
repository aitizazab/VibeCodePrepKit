using System;

namespace JamKit.Flow
{
    public enum GameState { Boot, Title, Playing, Paused, Dialogue, GameOver, Victory, Credits }

    /// <summary>
    /// The top-level screens of every JamKit game, on a whitelisted <see cref="StateMachine{TState}"/>
    /// so a stray "pause during the game-over screen" is refused instead of soft-locking the game.
    /// <code>
    /// Boot     -> Title
    /// Title    -> Playing, Credits
    /// Playing  -> Paused, Dialogue, GameOver, Victory
    /// Dialogue -> Playing, Paused, GameOver, Victory   (a story choice can end the game)
    /// Paused   -> Playing, Dialogue (Resume goes back where it came from), Title (quit)
    /// GameOver -> Playing (retry), Title
    /// Victory  -> Playing (new run), Credits, Title
    /// Credits  -> Title
    /// </code>
    /// Pause is allowed from Playing and Dialogue only: those are the states a player can be stuck in
    /// mid-action. Run setup belongs on <see cref="RunStarted"/>, not OnEnter(Playing), because Playing
    /// is also re-entered on every resume and a restart from Playing doesn't change state at all.
    /// </summary>
    public sealed class GameFlow
    {
        public StateMachine<GameState> Machine { get; }

        public GameState Current => Machine.Current;

        /// <summary>Raised with (from, to) after every state change.</summary>
        public event Action<GameState, GameState> Changed;

        /// <summary>A fresh run begins (start, retry, restart): reset the level, player and RunState here.</summary>
        public event Action RunStarted;

        GameState _resumeTo = GameState.Playing;

        public GameFlow(GameState initial = GameState.Boot)
        {
            Machine = new StateMachine<GameState>(initial)
                .Allow(GameState.Boot, GameState.Title)
                .Allow(GameState.Title, GameState.Playing, GameState.Credits)
                .Allow(GameState.Playing, GameState.Paused, GameState.Dialogue, GameState.GameOver, GameState.Victory)
                .Allow(GameState.Dialogue, GameState.Playing, GameState.Paused, GameState.GameOver, GameState.Victory)
                .Allow(GameState.Paused, GameState.Playing, GameState.Dialogue, GameState.Title)
                .Allow(GameState.GameOver, GameState.Playing, GameState.Title)
                .Allow(GameState.Victory, GameState.Playing, GameState.Credits, GameState.Title)
                .Allow(GameState.Credits, GameState.Title);
            Machine.Changed += (from, to) => Changed?.Invoke(from, to);
        }

        /// <summary>Gameplay clocks stop: Paused, and Dialogue (bullets and enemies wait while people talk).</summary>
        public bool IsTimeFrozen => Current == GameState.Paused || Current == GameState.Dialogue;

        /// <summary>A run is under way (including paused and mid-dialogue).</summary>
        public bool InRun => Current == GameState.Playing || Current == GameState.Paused || Current == GameState.Dialogue;

        public bool CanGo(GameState to) => Machine.CanGo(to);

        public bool TryGo(GameState to) => Machine.TryGo(to);

        public void Tick(float dt) => Machine.Tick(dt);

        public bool ShowTitle() => Machine.TryGo(GameState.Title);

        /// <summary>From Title, GameOver or Victory into a fresh run.</summary>
        public bool StartRun()
        {
            if (Current == GameState.Playing || !Machine.TryGo(GameState.Playing)) return false;
            RunStarted?.Invoke();
            return true;
        }

        /// <summary>Begin the run over from Playing, Paused, GameOver or Victory (the pause menu's "Restart").</summary>
        public bool Restart()
        {
            if (Current == GameState.Playing) { RunStarted?.Invoke(); return true; }
            if (Current == GameState.Dialogue || !Machine.TryGo(GameState.Playing)) return false;
            RunStarted?.Invoke();
            return true;
        }

        public bool Pause()
        {
            var from = Current;
            if (from != GameState.Playing && from != GameState.Dialogue) return false;
            if (!Machine.TryGo(GameState.Paused)) return false;
            _resumeTo = from;
            return true;
        }

        /// <summary>Back to whatever was paused: Playing or Dialogue.</summary>
        public bool Resume() => Current == GameState.Paused && Machine.TryGo(_resumeTo);

        public bool TogglePause() => Current == GameState.Paused ? Resume() : Pause();

        public bool OpenDialogue() => Current == GameState.Playing && Machine.TryGo(GameState.Dialogue);

        public bool CloseDialogue() => Current == GameState.Dialogue && Machine.TryGo(GameState.Playing);

        public bool Win() => Machine.TryGo(GameState.Victory);

        public bool Lose() => Machine.TryGo(GameState.GameOver);

        public bool ShowCredits() => Machine.TryGo(GameState.Credits);

        public bool QuitToTitle() => Current != GameState.Title && Machine.TryGo(GameState.Title);
    }
}
