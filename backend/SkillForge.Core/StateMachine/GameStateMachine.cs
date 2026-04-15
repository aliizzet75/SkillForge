namespace SkillForge.Core.StateMachine;

public enum GameState
{
    Idle,
    Matchmaking,
    WaitingForOpponent,
    GameStarting,
    RoundInProgress,
    RoundEnded,
    OpponentDisconnected,
    GamePaused,
    GameEnded,
    SoloModeConverted
}

public static class GameStateValidator
{
    private static readonly Dictionary<GameState, GameState[]> ValidTransitions = new()
    {
        [GameState.Idle] = new[] { GameState.Matchmaking },
        [GameState.Matchmaking] = new[] { GameState.WaitingForOpponent, GameState.SoloModeConverted },
        [GameState.WaitingForOpponent] = new[] { GameState.GameStarting, GameState.SoloModeConverted, GameState.Matchmaking },
        [GameState.GameStarting] = new[] { GameState.RoundInProgress, GameState.OpponentDisconnected },
        [GameState.RoundInProgress] = new[] { GameState.RoundEnded, GameState.OpponentDisconnected, GameState.GamePaused },
        [GameState.RoundEnded] = new[] { GameState.GameStarting, GameState.GameEnded, GameState.OpponentDisconnected },
        [GameState.OpponentDisconnected] = new[] { GameState.SoloModeConverted, GameState.GameEnded, GameState.GamePaused },
        [GameState.GamePaused] = new[] { GameState.RoundInProgress, GameState.GameEnded },
        [GameState.GameEnded] = new[] { GameState.Idle },
        [GameState.SoloModeConverted] = new[] { GameState.RoundInProgress, GameState.GameEnded }
    };

    public static bool CanTransition(GameState from, GameState to)
    {
        return ValidTransitions.TryGetValue(from, out var validStates) && validStates.Contains(to);
    }

    public static void ValidateTransition(GameState from, GameState to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid state transition: {from} -> {to}. " +
                $"Valid transitions from {from}: {string.Join(", ", ValidTransitions[from])}"
            );
        }
    }
}

public class GameStateMachine
{
    public GameState CurrentState { get; private set; } = GameState.Idle;
    
    public event Action<GameState, GameState>? OnStateChanged;
    
    public void TransitionTo(GameState newState)
    {
        GameStateValidator.ValidateTransition(CurrentState, newState);
        var oldState = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(oldState, newState);
    }
    
    public bool CanTransitionTo(GameState newState) => 
        GameStateValidator.CanTransition(CurrentState, newState);
}
