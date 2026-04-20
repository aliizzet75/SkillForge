using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
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
    public CancellationTokenSource RoundCts { get; set; } = new();
    public int SubmittedAnswersCount => PlayerRoundAnswers.Count;
    private readonly object _lock = new();
    
    public bool TryAddAnswer(string playerId, (int timeMs, string[] answers) answer)
    {
        lock (_lock)
        {
            if (PlayerRoundAnswers.ContainsKey(playerId))
                return false;
            PlayerRoundAnswers[playerId] = answer;
            return true;
        }
    }
    
    public int GetAnswerCount()
    {
        lock (_lock)
        {
            return PlayerRoundAnswers.Count;
        }
    }
    
    public void ClearAnswers()
    {
        lock (_lock)
        {
            PlayerRoundAnswers.Clear();
        }
    }
}

public class GameHub : Hub<IGameClient>
{
    private static readonly ConcurrentDictionary<string, GameStateMachine> _playerStates = new();
    private static readonly ConcurrentDictionary<string, string> _playerRooms = new();
    private static readonly ConcurrentDictionary<string, byte> _waitingPlayers = new();
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
        var opponent = _waitingPlayers.Keys.FirstOrDefault(p => p != playerId);
        
        if (opponent != null)
        {
            // Match found!
            // Remove both players from waiting list
            _waitingPlayers.TryRemove(playerId, out _);
            _waitingPlayers.TryRemove(opponent, out _);

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
            _waitingPlayers.TryAdd(playerId, 0);
            await Clients.Caller.WaitingForOpponent();
            stateMachine.TransitionTo(GameState.WaitingForOpponent);
        }
    }

    private async Task StartRound(string roomId, int round)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        // Cancel any previous round operations
        gameState.RoundCts?.Cancel();
        gameState.RoundCts?.Dispose();
        gameState.RoundCts = new CancellationTokenSource();
        var ct = gameState.RoundCts.Token;

        gameState.CurrentRound = round;
        
        // Generate round data using MemoryColorsGame
        // Use round number as difficulty to increase challenge with each round
        var roundData = gameState.GameEngine.GenerateData(round, round);
        gameState.CurrentRoundData = roundData;

        // Notify players that round is starting
        await Clients.Group(roomId).RoundStarting(round, 3);
        
        try
        {
            // Show colors phase
            await Clients.Group(roomId).ShowColors(roundData, 2000); // Show for 2 seconds
            
            // After showing colors, start input phase
            await Task.Delay(2000, ct);
            
            if (ct.IsCancellationRequested) return;
            
            await Clients.Group(roomId).HideColors();
            await Clients.Group(roomId).RoundInputPhase();
            
            // Reset player answers for this round
            gameState.ClearAnswers();
        }
        catch (OperationCanceledException)
        {
            // Round was cancelled (e.g., player disconnected)
            return;
        }
    }

    public async Task SubmitAnswer(string[] colors, int timeMs)
    {
        var playerId = Context.ConnectionId;
        
        if (!_playerRooms.TryGetValue(playerId, out var roomId))
            return;
            
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        // Store player's answer (thread-safe)
        gameState.TryAddAnswer(playerId, (timeMs, colors));

        // Check if both players have submitted answers (thread-safe)
        var answerCount = gameState.GetAnswerCount();
        var playersInRoom = _playerRooms.Count(kvp => kvp.Value == roomId);
        
        if (answerCount >= 2 || playersInRoom == 1) // Solo mode
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

        // Determine winner based on total scores (handle ties properly)
        var playerIds = gameState.PlayerScores.Keys.ToList();
        string player1Id = "";
        string player2Id = "";
        
        // Find the actual player connection IDs
        foreach (var kvp in gameState.PlayerConnectionIds)
        {
            if (kvp.Value == "Player1")
                player1Id = kvp.Key;
            else if (kvp.Value == "Player2")
                player2Id = kvp.Key;
        }
        
        // Get scores for both players
        int player1Score = player1Id != "" && gameState.PlayerScores.ContainsKey(player1Id) ? gameState.PlayerScores[player1Id] : 0;
        int player2Score = player2Id != "" && gameState.PlayerScores.ContainsKey(player2Id) ? gameState.PlayerScores[player2Id] : 0;
        
        // Determine winner
        string winner = "";
        bool isTie = player1Score == player2Score;
        
        if (!isTie)
        {
            winner = player1Score > player2Score ? "Player1" : "Player2";
        }

        // Prepare final results with proper player identification
        var finalResults = new Dictionary<string, object>();
        
        // Add both players to results with their scores
        finalResults["Player1"] = new
        {
            TotalScore = player1Score,
            IsWinner = !isTie && winner == "Player1",
            IsTie = isTie
        };
        
        finalResults["Player2"] = new
        {
            TotalScore = player2Score,
            IsWinner = !isTie && winner == "Player2",
            IsTie = isTie
        };

        // Send match over event with clear winner information
        await Clients.Group(roomId).MatchOver(new
        {
            Results = finalResults,
            Winner = isTie ? "Tie" : winner,
            Player1Score = player1Score,
            Player2Score = player2Score,
            IsTie = isTie
        });

        // Cancel any ongoing round operations
        gameState.RoundCts?.Cancel();
        gameState.RoundCts?.Dispose();

        // Clean up game state
        _roomGameStates.TryRemove(roomId, out _);
        
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
        
        _waitingPlayers.TryRemove(playerId, out _);
        
        // Clean up state
        _playerStates.TryRemove(playerId, out _);
        
        // Remove from groups
        if (_playerRooms.TryGetValue(playerId, out var roomId))
        {
            await Groups.RemoveFromGroupAsync(playerId, roomId);
            _playerRooms.TryRemove(playerId, out _);
            
            // If this was the last player in the room, clean up game state
            if (!_playerRooms.Values.Contains(roomId))
            {
                if (_roomGameStates.TryGetValue(roomId, out var gameState))
                {
                    gameState.RoundCts?.Cancel();
                }
                _roomGameStates.TryRemove(roomId, out _);
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
            
            // Cancel ongoing round operations before cleanup
            if (_roomGameStates.TryGetValue(roomId, out var gameState))
            {
                gameState.RoundCts?.Cancel();
            }
        }

        // LeaveLobby handles the cleanup (no double remove here)
        await LeaveLobby();
        
        await base.OnDisconnectedAsync(exception);
    }
}