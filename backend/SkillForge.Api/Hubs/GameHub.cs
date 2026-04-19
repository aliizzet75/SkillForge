using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using SkillForge.Core.StateMachine;
using SkillForge.Core.Models;
using SkillForge.Games;

namespace SkillForge.Api.Hubs;

public interface IGameClient
{
    Task PlayerJoined(string playerName, string avatar);
    Task PlayerLeft(string playerName);
    Task MatchFound(string opponentName, string opponentAvatar, int gameType, int round, int totalRounds);
    Task GameStarting(int gameType, object gameData);
    Task ShowColors(object colors, int durationMs);
    Task HideColors();
    Task RoundStarting(int round, int totalRounds);
    Task RoundInputPhase();
    Task OpponentFinished(string playerName);
    Task RoundResult(object result);
    Task MatchOver(object result);
    Task OpponentDisconnected();
    Task SoloModeActivated();
    Task WaitingForOpponent();
}

// Game state tracking for each room
public class RoomGameState
{
    public int CurrentRound { get; set; } = 1;
    public Dictionary<string, int> PlayerScores { get; set; } = new();
    public object CurrentRoundData { get; set; } = null!;
    public Dictionary<string, (int timeMs, string[] answers)> PlayerRoundAnswers { get; set; } = new();
    public MemoryColorsGame GameEngine { get; set; } = new();
    public Dictionary<string, string> PlayerConnectionIds { get; set; } = new();
}

public class GameHub : Hub<IGameClient>
{
    private static readonly Dictionary<string, GameStateMachine> _playerStates = new();
    private static readonly Dictionary<string, string> _playerRooms = new();
    private static readonly List<string> _waitingPlayers = new();
    private static readonly ConcurrentDictionary<string, RoomGameState> _roomGameStates = new(); // Track game state per room

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

            // Initialize room game state
            _roomGameStates[roomId] = new RoomGameState();
            _roomGameStates[roomId].PlayerConnectionIds[playerId] = "Player1";
            _roomGameStates[roomId].PlayerConnectionIds[opponent] = "Player2";

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

            // Start round 1
            await StartRound(roomId, 1);
        }
        else
        {
            // Add to waiting list
            _waitingPlayers.Add(playerId);
            await Clients.Caller.WaitingForOpponent();
            stateMachine.TransitionTo(GameState.WaitingForOpponent);
        }
    }

    private async Task StartRound(string roomId, int round)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        gameState.CurrentRound = round;
        
        // Generate round data using MemoryColorsGame
        var roundData = gameState.GameEngine.GenerateData(1, round); // difficulty = 1 for now
        gameState.CurrentRoundData = roundData;

        // Notify players that round is starting
        await Clients.Group(roomId).RoundStarting(round, 3);
        
        // Show colors phase
        await Clients.Group(roomId).ShowColors(roundData, 2000); // Show for 2 seconds
        
        // After showing colors, start input phase
        // TODO: move to background service to avoid holding SignalR context
        await Task.Delay(2000);
        await Clients.Group(roomId).HideColors();
        await Clients.Group(roomId).RoundInputPhase();
        
        // Reset player answers for this round
        gameState.PlayerRoundAnswers.Clear();
    }

    public async Task SubmitAnswer(string[] colors, int timeMs)
    {
        var playerId = Context.ConnectionId;
        
        if (!_playerRooms.TryGetValue(playerId, out var roomId))
            return;
            
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        // Store player's answer
        gameState.PlayerRoundAnswers[playerId] = (timeMs, colors);

        // Check if both players have submitted answers
        if (gameState.PlayerRoundAnswers.Count >= 2 || 
            (_playerRooms.Values.Count(id => id == roomId) == 1)) // Solo mode
        {
            await ProcessRoundResults(roomId);
        }
        else
        {
            // Notify opponent that player has finished
            await Clients.OthersInGroup(roomId).OpponentFinished("You");
        }
    }

    private async Task ProcessRoundResults(string roomId)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        var roundResults = new Dictionary<string, object>();

        // Validate answers and calculate scores for each player
        foreach (var kvp in gameState.PlayerRoundAnswers)
        {
            var playerId = kvp.Key;
            var (timeMs, answers) = kvp.Value;
            
            // Validate answer using MemoryColorsGame
            var validationResult = gameState.GameEngine.ValidateAnswer(answers, gameState.CurrentRoundData);
            var score = gameState.GameEngine.CalculateScore(timeMs, validationResult.CorrectCount, validationResult.IsPerfect);
            
            // Update player's total score
            if (!gameState.PlayerScores.ContainsKey(playerId))
                gameState.PlayerScores[playerId] = 0;
                
            gameState.PlayerScores[playerId] += score;
            
            roundResults[playerId] = new
            {
                Score = score,
                TotalScore = gameState.PlayerScores[playerId],
                CorrectCount = validationResult.CorrectCount,
                TotalCount = validationResult.TotalCount,
                IsPerfect = validationResult.IsPerfect,
                TimeMs = timeMs
            };
        }

        // Send round results to all players in room
        await Clients.Group(roomId).RoundResult(new
        {
            Round = gameState.CurrentRound,
            Results = roundResults
        });

        // Check if game is over (3 rounds completed)
        if (gameState.CurrentRound >= 3)
        {
            await EndMatch(roomId);
        }
        else
        {
            // Start next round after a delay
            await Task.Delay(3000);
            await StartRound(roomId, gameState.CurrentRound + 1);
        }
    }

    private async Task EndMatch(string roomId)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        // Determine winner based on total scores
        var playerIds = gameState.PlayerScores.Keys.ToList();
        string winnerId = "";
        int winnerScore = -1;
        
        foreach (var playerId in playerIds)
        {
            if (gameState.PlayerScores[playerId] > winnerScore)
            {
                winnerScore = gameState.PlayerScores[playerId];
                winnerId = playerId;
            }
        }

        // Prepare final results
        var finalResults = new Dictionary<string, object>();
        foreach (var playerId in playerIds)
        {
            finalResults[playerId] = new
            {
                TotalScore = gameState.PlayerScores[playerId],
                IsWinner = playerId == winnerId
            };
        }

        // Send match over event
        await Clients.Group(roomId).MatchOver(new
        {
            Results = finalResults,
            Winner = winnerId,
            WinnerScore = winnerScore
        });

        // Clean up game state
        _roomGameStates.Remove(roomId);
        
        // Update player states
        foreach (var playerId in playerIds)
        {
            if (_playerStates.TryGetValue(playerId, out var stateMachine))
            {
                if (stateMachine.CanTransitionTo(GameState.GameEnded))
                    stateMachine.TransitionTo(GameState.GameEnded);
            }
        }
    }

    public async Task GameComplete(int score, int timeMs)
    {
        // This method is kept for backward compatibility but the new SubmitAnswer method should be used
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
            
            // If this was the last player in the room, clean up game state
            if (!_playerRooms.ContainsValue(roomId))
            {
                _roomGameStates.Remove(roomId);
            }
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
            
            // Clean up room if needed
            _playerRooms.Remove(playerId);
            if (!_playerRooms.ContainsValue(roomId))
            {
                _roomGameStates.Remove(roomId);
            }
        }

        await LeaveLobby();
        
        await base.OnDisconnectedAsync(exception);
    }
}