using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillForge.Core.StateMachine;
using SkillForge.Core.Models;
using SkillForge.Games;
using SkillForge.Core.Data;

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
    Task LobbySnapshot(IEnumerable<object> players);
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
    public Dictionary<string, int> PlayerPerfectRounds { get; set; } = new();
    public object CurrentRoundData { get; set; } = null!;
    public Dictionary<string, (int timeMs, string[] answers)> PlayerRoundAnswers { get; set; } = new();
    public MemoryColorsGame GameEngine { get; set; } = new();
    public Dictionary<string, string> PlayerConnectionIds { get; set; } = new();
    public CancellationTokenSource RoundCts { get; set; } = new();
    public int SubmittedAnswersCount => PlayerRoundAnswers.Count;
    public long RoundStartTimeMs { get; set; } = 0;
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

    public Dictionary<string, (int timeMs, string[] answers)> GetAnswerSnapshot()
    {
        lock (_lock)
        {
            return new Dictionary<string, (int timeMs, string[] answers)>(PlayerRoundAnswers);
        }
    }
}

public class GameHub : Hub<IGameClient>
{
    private static readonly ConcurrentDictionary<string, GameStateMachine> _playerStates = new();
    private static readonly ConcurrentDictionary<string, string> _playerRooms = new();
    private static readonly ConcurrentDictionary<string, byte> _waitingPlayers = new();
    private static readonly ConcurrentDictionary<string, RoomGameState> _roomGameStates = new();
    // Maps connectionId → userId so GetUserIdFromConnection can look up any connection, not just the caller
    private static readonly ConcurrentDictionary<string, string> _connectionUserIds = new();
    // Tracks players currently in the lobby: connectionId → (name, avatar)
    private static readonly ConcurrentDictionary<string, (string Name, string Avatar)> _lobbyPlayers = new();

    private readonly IServiceScopeFactory _scopeFactory;

    public GameHub(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task EnterLobby(string playerName, string avatar)
    {
        var playerId = Context.ConnectionId;

        _playerStates[playerId] = new GameStateMachine();
        _playerStates[playerId].OnStateChanged += (oldState, newState) =>
        {
            Console.WriteLine($"Player {playerName}: {oldState} -> {newState}");
        };

        // Send each existing lobby player to the new joiner
        foreach (var existing in _lobbyPlayers.Values)
            await Clients.Caller.PlayerJoined(existing.Name, existing.Avatar);

        _lobbyPlayers[playerId] = (playerName, avatar);
        await Groups.AddToGroupAsync(playerId, "lobby");
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

        var opponent = _waitingPlayers.Keys.FirstOrDefault(p => p != playerId);

        if (opponent != null)
        {
            _waitingPlayers.TryRemove(playerId, out _);
            _waitingPlayers.TryRemove(opponent, out _);

            var roomId = $"game_{Guid.NewGuid():N}";
            _playerRooms[playerId] = roomId;
            _playerRooms[opponent] = roomId;

            _roomGameStates[roomId] = new RoomGameState();
            _roomGameStates[roomId].PlayerConnectionIds[playerId] = "Player1";
            _roomGameStates[roomId].PlayerConnectionIds[opponent] = "Player2";

            await Groups.AddToGroupAsync(playerId, roomId);
            await Groups.AddToGroupAsync(opponent, roomId);

            await Clients.Client(playerId).PlayerAssigned("Player1");
            await Clients.Client(opponent).PlayerAssigned("Player2");

            await Clients.Client(playerId).MatchFound("Opponent", "🧙‍♂️", 1, 1, 3);
            await Clients.Client(opponent).MatchFound("Opponent", "🧙‍♀️", 1, 1, 3);

            stateMachine.TransitionTo(GameState.GameStarting);
            if (_playerStates.TryGetValue(opponent, out var opponentState))
                opponentState.TransitionTo(GameState.GameStarting);

            await StartRound(roomId, 1);
        }
        else
        {
            _waitingPlayers.TryAdd(playerId, 0);
            await Clients.Caller.WaitingForOpponent();
            stateMachine.TransitionTo(GameState.WaitingForOpponent);
        }
    }

    private async Task StartRound(string roomId, int round)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        gameState.RoundCts?.Cancel();
        gameState.RoundCts?.Dispose();
        gameState.RoundCts = new CancellationTokenSource();
        var ct = gameState.RoundCts.Token;

        gameState.CurrentRound = round;

        var roundData = gameState.GameEngine.GenerateData(round, round);
        gameState.CurrentRoundData = roundData;

        await Clients.Group(roomId).RoundStarting(round, 3);

        try
        {
            await Clients.Group(roomId).ShowColors(roundData, 2000);
            await Task.Delay(2000, ct);

            if (ct.IsCancellationRequested) return;

            await Clients.Group(roomId).HideColors();
            await Clients.Group(roomId).RoundInputPhase();

            gameState.RoundStartTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            gameState.ClearAnswers();
        }
        catch (OperationCanceledException)
        {
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

        var currentTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timeMs = (int)(currentTimeMs - gameState.RoundStartTimeMs);

        gameState.TryAddAnswer(playerId, (timeMs, colors));

        var answerCount = gameState.GetAnswerCount();
        var playersInRoom = _playerRooms.Count(kvp => kvp.Value == roomId);

        if (answerCount >= 2 || playersInRoom == 1)
        {
            if (gameState.TryStartRoundProcessing())
                await ProcessRoundResults(roomId);
        }
        else
        {
            await Clients.OthersInGroup(roomId).OpponentFinished("You");
        }
    }

    private async Task ProcessRoundResults(string roomId)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        var roundResults = new Dictionary<string, RoundPlayerResult>();
        var answersSnapshot = gameState.GetAnswerSnapshot();

        foreach (var kvp in answersSnapshot)
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

            // Accumulate per-player perfect round count
            if (validationResult.IsPerfect)
                gameState.PlayerPerfectRounds[playerId] = gameState.PlayerPerfectRounds.GetValueOrDefault(playerId) + 1;
        }

        foreach (var playerId in answersSnapshot.Keys)
        {
            var opponentId = answersSnapshot.Keys.FirstOrDefault(k => k != playerId);
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

        if (gameState.CurrentRound >= 3)
        {
            await EndMatch(roomId);
        }
        else
        {
            await Task.Delay(3000);
            await StartRound(roomId, gameState.CurrentRound + 1);
        }
    }

    private async Task EndMatch(string roomId)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gameState))
            return;

        var playerIds = gameState.PlayerScores.Keys.ToList();
        string player1Id = "";
        string player2Id = "";

        foreach (var kvp in gameState.PlayerConnectionIds)
        {
            if (kvp.Value == "Player1")
                player1Id = kvp.Key;
            else if (kvp.Value == "Player2")
                player2Id = kvp.Key;
        }

        int player1Score = player1Id != "" && gameState.PlayerScores.ContainsKey(player1Id) ? gameState.PlayerScores[player1Id] : 0;
        int player2Score = player2Id != "" && gameState.PlayerScores.ContainsKey(player2Id) ? gameState.PlayerScores[player2Id] : 0;

        string winner = "";
        bool isTie = player1Score == player2Score;

        if (!isTie)
            winner = player1Score > player2Score ? "Player1" : "Player2";

        // Calculate XP before sending MatchOver so it can be shown in the UI
        var player1PerfectRounds = gameState.PlayerPerfectRounds.GetValueOrDefault(player1Id);
        var player2PerfectRounds = gameState.PlayerPerfectRounds.GetValueOrDefault(player2Id);
        int player1Xp = 24 + (player1PerfectRounds * 5);
        int player2Xp = 24 + (player2PerfectRounds * 5);
        if (!isTie)
        {
            if (winner == "Player1") player1Xp += 10;
            else player2Xp += 10;
        }

        var finalResults = new Dictionary<string, object>
        {
            ["Player1"] = new { TotalScore = player1Score, IsWinner = !isTie && winner == "Player1", IsTie = isTie, XpEarned = player1Xp },
            ["Player2"] = new { TotalScore = player2Score, IsWinner = !isTie && winner == "Player2", IsTie = isTie, XpEarned = player2Xp }
        };

        await Clients.Group(roomId).MatchOver(new
        {
            Results = finalResults,
            Winner = isTie ? "Tie" : winner,
            Player1Score = player1Score,
            Player2Score = player2Score,
            Player1XpEarned = player1Xp,
            Player2XpEarned = player2Xp,
            IsTie = isTie
        });

        // Cleanup game state BEFORE saving to DB so a DB failure never leaks state
        gameState.RoundCts?.Cancel();
        gameState.RoundCts?.Dispose();
        _roomGameStates.TryRemove(roomId, out _);

        foreach (var pid in playerIds)
        {
            if (_playerStates.TryGetValue(pid, out var sm) && sm.CanTransitionTo(GameState.GameEnded))
                sm.TransitionTo(GameState.GameEnded);
        }

        // Persist XP after cleanup so game state is always consistent
        await SaveMatchResultsToDatabase(player1Id, player2Id, player1Score, player2Score, winner, isTie, player1Xp, player2Xp);
    }

    private async Task SaveMatchResultsToDatabase(string player1Id, string player2Id, int player1Score, int player2Score, string winner, bool isTie, int player1Xp, int player2Xp)
    {
        var player1UserId = GetUserIdFromConnection(player1Id);
        var player2UserId = GetUserIdFromConnection(player2Id);

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (!string.IsNullOrEmpty(player1UserId))
                    await SavePlayerMatchResult(player1UserId, 1, player1Score, winner == "Player1" && !isTie, player1Xp);

                if (!string.IsNullOrEmpty(player2UserId))
                    await SavePlayerMatchResult(player2UserId, 1, player2Score, winner == "Player2" && !isTie, player2Xp);

                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Retry {attempt}/{maxRetries}] Error saving match results: {ex.Message}");
                if (attempt < maxRetries)
                    await Task.Delay(500 * attempt);
                else
                    Console.WriteLine("All retry attempts exhausted. XP not saved.");
            }
        }
    }

    // Looks up userId by connectionId from the static map populated at connection time
    private string? GetUserIdFromConnection(string connectionId)
    {
        _connectionUserIds.TryGetValue(connectionId, out var userId);
        return userId;
    }

    private async Task SavePlayerMatchResult(string userId, int gameType, int score, bool won, int xpEarned)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            Console.WriteLine($"Invalid user ID format: {userId}");
            return;
        }

        // Create a fresh scope per call — avoids injecting scoped DbContext into the long-lived hub
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SkillForgeDbContext>();

        var matchHistory = new MatchHistory
        {
            UserId = userGuid,
            GameType = gameType,
            Score = score,
            Won = won,
            XPEarned = xpEarned,
            PlayedAt = DateTime.UtcNow
        };

        dbContext.MatchHistories.Add(matchHistory);

        var user = await dbContext.Users.FindAsync(userGuid);
        if (user != null)
        {
            user.TotalXp += xpEarned;
            user.UpdatedAt = DateTime.UtcNow;
            user.CurrentLevel = (user.TotalXp / 100) + 1;
        }

        await dbContext.SaveChangesAsync();
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
            await Clients.OthersInGroup(roomId).OpponentFinished("You");

            int opponentScore = 0;
            if (_roomGameStates.TryGetValue(roomId, out var gameState))
            {
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
                    opponentScore = gameState.PlayerScores[opponentId];
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
        _playerStates.TryRemove(playerId, out _);

        if (_playerRooms.TryGetValue(playerId, out var roomId))
        {
            await Groups.RemoveFromGroupAsync(playerId, roomId);
            _playerRooms.TryRemove(playerId, out _);

            if (!_playerRooms.Values.Contains(roomId))
            {
                if (_roomGameStates.TryGetValue(roomId, out var gameState))
                    gameState.RoundCts?.Cancel();
                _roomGameStates.TryRemove(roomId, out _);
            }
        }

        if (_lobbyPlayers.TryRemove(playerId, out var player))
            await Clients.Group("lobby").PlayerLeft(player.Name);

        await Groups.RemoveFromGroupAsync(playerId, "lobby");
    }

    public override async Task OnConnectedAsync()
    {
        // JWT was already validated by JwtAuthenticationMiddleware; read claims from Context.User
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            Context.Items["UserId"] = userId;
            _connectionUserIds[Context.ConnectionId] = userId;
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = Context.ConnectionId;

        _connectionUserIds.TryRemove(playerId, out _);

        if (_playerRooms.TryGetValue(playerId, out var roomId))
        {
            await Clients.OthersInGroup(roomId).OpponentDisconnected();

            if (_roomGameStates.TryGetValue(roomId, out var gameState))
                gameState.RoundCts?.Cancel();
        }

        await LeaveLobby();

        await base.OnDisconnectedAsync(exception);
    }
}
