using Microsoft.AspNetCore.SignalR;
using SkillForge.Core.StateMachine;
using SkillForge.Core.Models;

namespace SkillForge.Api.Hubs;

public interface IGameClient
{
    Task PlayerJoined(string playerName, string avatar);
    Task PlayerLeft(string playerName);
    Task MatchFound(string opponentName, string opponentAvatar, int gameType, int round, int totalRounds);
    Task GameStarting(int gameType, object gameData);
    Task OpponentFinished(string playerName);
    Task RoundResult(object result);
    Task MatchOver(object result);
    Task OpponentDisconnected();
    Task SoloModeActivated();
    Task WaitingForOpponent();
}

public class GameHub : Hub<IGameClient>
{
    private static readonly Dictionary<string, GameStateMachine> _playerStates = new();
    private static readonly Dictionary<string, string> _playerRooms = new();
    private static readonly List<string> _waitingPlayers = new();

    public async Task EnterLobby(string playerName, string avatar)
    {
        var playerId = Context.ConnectionId;
        
        // Initialize player state
        _playerStates[playerId] = new GameStateMachine();
        _playerStates[playerId].OnStateChanged += (oldState, newState) =>
        {
            Console.WriteLine($"Player {playerName}: {oldState} -> {newState}");
        };

        // Add to general lobby group
        await Groups.AddToGroupAsync(playerId, "lobby");
        
        // Notify others in lobby
        await Clients.Group("lobby").PlayerJoined(playerName, avatar);
    }

    public async Task PlayRandom()
    {
        var playerId = Context.ConnectionId;
        
        if (!_playerStates.TryGetValue(playerId, out var stateMachine))
            return;

        if (!stateMachine.CanTransitionTo(GameState.Matchmaking))
            return;

        stateMachine.TransitionTo(GameState.Matchmaking);

        // Check for waiting players
        var opponent = _waitingPlayers.FirstOrDefault(p => p != playerId);
        
        if (opponent != null)
        {
            // Match found!
            _waitingPlayers.Remove(opponent);
            _waitingPlayers.Remove(playerId);

            var roomId = $"game_{Guid.NewGuid():N}";
            _playerRooms[playerId] = roomId;
            _playerRooms[opponent] = roomId;

            await Groups.AddToGroupAsync(playerId, roomId);
            await Groups.AddToGroupAsync(opponent, roomId);

            // Notify both players
            await Clients.Client(playerId).MatchFound("Opponent", "🧙‍♂️", 1, 1, 3);
            await Clients.Client(opponent).MatchFound("Opponent", "🧙‍♀️", 1, 1, 3);

            // Start game for both
            stateMachine.TransitionTo(GameState.GameStarting);
            if (_playerStates.TryGetValue(opponent, out var opponentState))
            {
                opponentState.TransitionTo(GameState.GameStarting);
            }

            // Send game data (simulated for now)
            var gameData = new[] { "🔴", "🟢", "🔵" };
            await Clients.Group(roomId).GameStarting(1, gameData);

            // Transition to round in progress
            stateMachine.TransitionTo(GameState.RoundInProgress);
            if (_playerStates.TryGetValue(opponent, out var oppState))
            {
                oppState.TransitionTo(GameState.RoundInProgress);
            }
        }
        else
        {
            // Add to waiting list
            _waitingPlayers.Add(playerId);
            await Clients.Caller.WaitingForOpponent();
            stateMachine.TransitionTo(GameState.WaitingForOpponent);
        }
    }

    public async Task GameComplete(int score, int timeMs)
    {
        var playerId = Context.ConnectionId;
        
        if (!_playerStates.TryGetValue(playerId, out var stateMachine))
            return;

        if (!stateMachine.CanTransitionTo(GameState.RoundEnded))
            return;

        stateMachine.TransitionTo(GameState.RoundEnded);

        if (_playerRooms.TryGetValue(playerId, out var roomId))
        {
            // Notify opponent
            await Clients.OthersInGroup(roomId).OpponentFinished("You");
            
            // Send round result
            await Clients.Group(roomId).RoundResult(new
            {
                YourScore = score,
                OpponentScore = 0, // Would be calculated from actual game
                Winner = playerId
            });
        }
    }

    public async Task LeaveLobby()
    {
        var playerId = Context.ConnectionId;
        
        // Remove from waiting list if present
        _waitingPlayers.Remove(playerId);
        
        // Clean up state
        _playerStates.Remove(playerId);
        
        // Remove from groups
        if (_playerRooms.TryGetValue(playerId, out var roomId))
        {
            await Groups.RemoveFromGroupAsync(playerId, roomId);
            _playerRooms.Remove(playerId);
        }
        
        await Groups.RemoveFromGroupAsync(playerId, "lobby");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = Context.ConnectionId;
        
        // Handle disconnection
        if (_playerRooms.TryGetValue(playerId, out var roomId))
        {
            await Clients.OthersInGroup(roomId).OpponentDisconnected();
        }

        await LeaveLobby();
        
        await base.OnDisconnectedAsync(exception);
    }
}
