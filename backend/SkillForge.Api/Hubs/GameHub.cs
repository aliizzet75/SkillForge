using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SkillForge.Core.StateMachine;
using SkillForge.Core.Models;
using SkillForge.Games;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillForge.Core.Data;
using Microsoft.EntityFrameworkCore;

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
    Task PlayerAssigned(string label);
}

public class RoundPlayerResult
{
    public int Score { get; init; }
    public int TotalScore { get; init; }
    public int CorrectCount { get; init; }
    public int TotalCount { get; init; }
    public bool IsPerfect { get; init; }
    public int TimeMs { get; init; }
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
    public long RoundStartTimeMs { get; set; } = 0; // Timestamp when input phase starts
    private readonly object _lock = new();
    private bool _roundBeingProcessed = false;

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
            _roundBeingProcessed = false;
        }
    }

    public bool TryStartRoundProcessing()
    {
        lock (_lock)
        {
            if (_roundBeingProcessed) return false;
            _roundBeingProcessed = true;
            return true;
        }
    }
}

public class GameHub : Hub<IGameClient>
{
    private static readonly ConcurrentDictionary<string, GameStateMachine> _playerStates = new();
    private static readonly ConcurrentDictionary<string, string> _playerRooms = new();
    private static readonly ConcurrentDictionary<string, byte> _waitingPlayers = new();
    private static readonly ConcurrentDictionary<string, RoomGameState> _roomGameStates = new(); // Track game state per room

    private readonly SkillForgeDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GameHub(SkillForgeDbContext dbContext, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }

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

            // Notify both players of their assigned labels
            await Clients.Client(playerId).PlayerAssigned("Player1");
            await Clients.Client(opponent).PlayerAssigned("Player2");
            
            // Notify both players about match
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
            
            // Set round start time on server (prevents client-side cheating)
            gameState.RoundStartTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // Reset player answers for this round
            gameState.ClearAnswers();
        }
        catch (OperationCanceledException)
        {
            // Round was cancelled (e.g., player disconnected)
            return;
        }
    }

    public async Task SubmitAnswer(string[] colors)
    {
        var playerId = Context.ConnectionId;
        
        if (!_playerRooms.TryGetValue(playerId, out var roomId))
            return;
            
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        // Calculate response time server-side (prevents client-side cheating)
        var currentTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timeMs = (int)(currentTimeMs - gameState.RoundStartTimeMs);

        // Store player's answer (thread-safe)
        gameState.TryAddAnswer(playerId, (timeMs, colors));

        // Check if both players have submitted answers (thread-safe)
        var answerCount = gameState.GetAnswerCount();
        var playersInRoom = _playerRooms.Count(kvp => kvp.Value == roomId);
        
        if (answerCount >= 2 || playersInRoom == 1) // Solo mode
        {
            if (gameState.TryStartRoundProcessing())
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

        var roundResults = new Dictionary<string, RoundPlayerResult>();

        foreach (var kvp in gameState.PlayerRoundAnswers)
        {
            var playerId = kvp.Key;
            var (timeMs, answers) = kvp.Value;

            var validationResult = gameState.GameEngine.ValidateAnswer(answers, gameState.CurrentRoundData);
            var score = gameState.GameEngine.CalculateScore(timeMs, validationResult.CorrectCount, validationResult.IsPerfect);

            if (!gameState.PlayerScores.ContainsKey(playerId))
                gameState.PlayerScores[playerId] = 0;
            gameState.PlayerScores[playerId] += score;

            roundResults[playerId] = new RoundPlayerResult
            {
                Score = score,
                TotalScore = gameState.PlayerScores[playerId],
                CorrectCount = validationResult.CorrectCount,
                TotalCount = validationResult.TotalCount,
                IsPerfect = validationResult.IsPerfect,
                TimeMs = timeMs
            };
        }

        foreach (var playerId in gameState.PlayerRoundAnswers.Keys)
        {
            var opponentId = gameState.PlayerRoundAnswers.Keys.FirstOrDefault(k => k != playerId);
            var opponentScore = opponentId != null && roundResults.TryGetValue(opponentId, out var opp) ? opp.Score : 0;
            var myScore = roundResults.TryGetValue(playerId, out var my) ? my.Score : 0;

            await Clients.Client(playerId).RoundResult(new
            {
                Round = gameState.CurrentRound,
                YourScore = myScore,
                OpponentScore = opponentScore,
                Results = roundResults
            });
        }

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

        // Save XP to database for both players
        await SaveMatchResultsToDatabase(player1Id, player2Id, player1Score, player2Score, winner, isTie, gameState);

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

    private async Task SaveMatchResultsToDatabase(string player1Id, string player2Id, int player1Score, int player2Score, string winner, bool isTie, RoomGameState gameState)
    {
        try
        {
            // Get user IDs from HttpContext if authenticated
            var player1UserId = await GetUserIdFromConnection(player1Id);
            var player2UserId = await GetUserIdFromConnection(player2Id);

            // Calculate XP: Base 24 + Win bonus 10 + Perfect round bonus 5 per perfect round
            var player1PerfectRounds = gameState.PlayerRoundAnswers.Values.Count(a => a.answers.Length > 0);
            var player2PerfectRounds = gameState.PlayerRoundAnswers.Values.Count(a => a.answers.Length > 0);

            int player1Xp = 24;
            int player2Xp = 24;

            if (!isTie)
            {
                if (winner == "Player1")
                    player1Xp += 10;
                else
                    player2Xp += 10;
            }

            // Save Player 1 results
            if (!string.IsNullOrEmpty(player1UserId))
            {
                await SavePlayerMatchResult(player1UserId, 1, player1Score, winner == "Player1" && !isTie, player1Xp);
            }

            // Save Player 2 results
            if (!string.IsNullOrEmpty(player2UserId))
            {
                await SavePlayerMatchResult(player2UserId, 1, player2Score, winner == "Player2" && !isTie, player2Xp);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving match results: {ex.Message}");
        }
    }

    private async Task<string?> GetUserIdFromConnection(string connectionId)
    {
        // Try to get user ID from Context.Items set by JWT middleware
        // For now, return null as we need access to HttpContext which isn't directly available here
        // This will be enhanced when we have proper user session management
        return null;
    }

    private async Task SavePlayerMatchResult(string userId, int gameType, int score, bool won, int xpEarned)
    {
        try
        {
            var userGuid = Guid.Parse(userId);

            // Add match history entry
            var matchHistory = new MatchHistory
            {
                UserId = userGuid,
                GameType = gameType,
                Score = score,
                Won = won,
                XPEarned = xpEarned,
                PlayedAt = DateTime.UtcNow
            };

            _dbContext.MatchHistories.Add(matchHistory);

            // Update user total XP
            var user = await _dbContext.Users.FindAsync(userGuid);
            if (user != null)
            {
                user.TotalXp += xpEarned;
                user.UpdatedAt = DateTime.UtcNow;
                
                // Level up calculation (simple: every 100 XP = level up)
                user.CurrentLevel = (user.TotalXp / 100) + 1;
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving player match result: {ex.Message}");
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
            
            // Send round result with proper opponent score lookup
            int opponentScore = 0;
            if (_roomGameStates.TryGetValue(roomId, out var gameState))
            {
                // Find opponent and get their score
                string opponentId = "";
                foreach (var kvp in gameState.PlayerConnectionIds)
                {
                    if (kvp.Key != playerId)
                    {
                        opponentId = kvp.Key;
                        break;
                    }
                }
                
                if (!string.IsNullOrEmpty(opponentId) && gameState.PlayerScores.ContainsKey(opponentId))
                {
                    opponentScore = gameState.PlayerScores[opponentId];
                }
            }
            
            await Clients.Caller.RoundResult(new
            {
                YourScore = score,
                OpponentScore = opponentScore,
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