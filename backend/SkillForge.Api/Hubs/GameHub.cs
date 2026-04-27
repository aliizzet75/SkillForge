using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SkillForge.Core.StateMachine;
using SkillForge.Core.Models;
using SkillForge.Games;
using SkillForge.Core.Data;
using StackExchange.Redis;

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
    Task ChallengeReceived(string fromPlayerName, string fromAvatar);
    Task ChallengeAccepted(string byPlayerName);
    Task ChallengeDeclined(string byPlayerName);
    Task MatchmakingTimeout();
}

public class RoomGameState
{
    public int CurrentRound { get; set; } = 1;
    public Dictionary<string, int> PlayerScores { get; set; } = new();
    public Dictionary<string, int> PlayerPerfectRounds { get; set; } = new();
    public object CurrentRoundData { get; set; } = null!;
    public Dictionary<string, (int timeMs, string[] answers)> PlayerRoundAnswers { get; set; } = new();
    public IGamePlugin GameEngine { get; set; } = new MemoryColorsGame();
    public int GameTypeId { get; set; } = 1;
    public Dictionary<string, string> PlayerConnectionIds { get; set; } = new();
    public CancellationTokenSource RoundCts { get; set; } = new();
    public long RoundStartTimeMs { get; set; } = 0;
    private readonly object _lock = new();
    private bool _roundBeingProcessed = false;

    public bool TryAddAnswer(string playerId, (int timeMs, string[] answers) answer)
    {
        lock (_lock)
        {
            if (PlayerRoundAnswers.ContainsKey(playerId)) return false;
            PlayerRoundAnswers[playerId] = answer;
            return true;
        }
    }

    public int GetAnswerCount()
    {
        lock (_lock) { return PlayerRoundAnswers.Count; }
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
    // Redis key constants
    private const string LobbyPlayersKey = "sf:lobby:players";  // Hash: connId → JSON{name,avatar}
    private const string LobbyNamesKey   = "sf:lobby:names";    // Hash: playerName → connId
    private const string MmQueueKey      = "sf:mm:queue";       // SortedSet: connId → join timestamp
    private const string MmLevelsKey     = "sf:mm:levels";      // Hash: connId → skill level
    private const string ChallengesKey   = "sf:challenges";     // Hash: targetConnId → challengerConnId
    private const string PlayerRoomsKey  = "sf:rooms";          // Hash: connId → roomId
    private const string ConnUsersKey    = "sf:users";          // Hash: connId → userId
    private const string MmGameTypesKey  = "sf:mm:gametypes";   // Hash: connId → gameType
    private const string ChallengeGameTypesKey = "sf:challenges:gametypes"; // Hash: targetConnId → gameType

    // Atomically find + remove a skill-matched opponent from the matchmaking queue
    private static readonly LuaScript FindOpponentScript = LuaScript.Prepare(@"
local members = redis.call('ZRANGE', @queue, 0, -1)
local my_id = @player_id
local my_level = tonumber(@player_level)
local range = tonumber(@level_range)
for _, member in ipairs(members) do
    if member ~= my_id then
        local lvl = redis.call('HGET', @levels, member)
        if lvl and math.abs(tonumber(lvl) - my_level) <= range then
            redis.call('ZREM', @queue, member)
            redis.call('HDEL', @levels, member)
            return member
        end
    end
end
return false
");

    // Non-serializable in-memory state — bound to active connections/rooms
    private static readonly ConcurrentDictionary<string, GameStateMachine> _playerStates = new();
    private static readonly ConcurrentDictionary<string, RoomGameState> _roomGameStates = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _matchmakingTimers = new();

    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private IDatabase Db => _redis.GetDatabase();

    public GameHub(IConnectionMultiplexer redis, IServiceScopeFactory scopeFactory)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
    }

    // ─── Lobby ──────────────────────────────────────────────────────────────

    public async Task EnterLobby(string playerName, string avatar)
    {
        var connId = Context.ConnectionId;

        _playerStates[connId] = new GameStateMachine();
        _playerStates[connId].OnStateChanged += (o, n) =>
            Console.WriteLine($"Player {playerName}: {o} -> {n}");

        // Send existing lobby players to new joiner
        var existing = await Db.HashGetAllAsync(LobbyPlayersKey);
        foreach (var entry in existing)
        {
            var info = JsonSerializer.Deserialize<LobbyPlayer>(entry.Value.ToString());
            if (info != null) await Clients.Caller.PlayerJoined(info.Name, info.Avatar);
        }

        var json = JsonSerializer.Serialize(new LobbyPlayer(playerName, avatar));
        await Db.HashSetAsync(LobbyPlayersKey, connId, json);
        await Db.HashSetAsync(LobbyNamesKey, playerName, connId);

        await Groups.AddToGroupAsync(connId, "lobby");
        await Clients.Group("lobby").PlayerJoined(playerName, avatar);
    }

    // ─── Matchmaking ────────────────────────────────────────────────────────

    public async Task PlayRandom(int gameType = 1)
    {
        var connId = Context.ConnectionId;

        if (!_playerStates.TryGetValue(connId, out var sm) || !sm.CanTransitionTo(GameState.Matchmaking))
            return;

        sm.TransitionTo(GameState.Matchmaking);

        var level = await GetPlayerLevel(connId);
        await Db.HashSetAsync(MmGameTypesKey, connId, gameType.ToString());

        var result = await Db.ScriptEvaluateAsync(FindOpponentScript, new
        {
            queue        = (RedisKey)MmQueueKey,
            levels       = (RedisKey)MmLevelsKey,
            player_id    = (RedisValue)connId,
            player_level = (RedisValue)level,
            level_range  = (RedisValue)2
        });

        if (!result.IsNull && result.ToString() != "0")
        {
            var opponentId = result.ToString();

            if (_matchmakingTimers.TryRemove(opponentId, out var opCts))
                opCts.Cancel();

            await Db.HashDeleteAsync(MmGameTypesKey, opponentId);
            await StartMatch(connId, opponentId, gameType);
        }
        else
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await Db.SortedSetAddAsync(MmQueueKey, connId, ts);
            await Db.HashSetAsync(MmLevelsKey, connId, level.ToString());

            await Clients.Caller.WaitingForOpponent();
            sm.TransitionTo(GameState.WaitingForOpponent);

            var cts = new CancellationTokenSource();
            _matchmakingTimers[connId] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(30_000, cts.Token);
                    var removed = await Db.SortedSetRemoveAsync(MmQueueKey, connId);
                    if (removed)
                    {
                        await Db.HashDeleteAsync(MmLevelsKey, connId);
                        _matchmakingTimers.TryRemove(connId, out _);
                        await Clients.Client(connId).MatchmakingTimeout();
                    }
                }
                catch (OperationCanceledException) { }
            });
        }
    }

    public async Task CancelMatchmaking()
    {
        var connId = Context.ConnectionId;

        if (_matchmakingTimers.TryRemove(connId, out var cts)) cts.Cancel();
        await Db.SortedSetRemoveAsync(MmQueueKey, connId);
        await Db.HashDeleteAsync(MmLevelsKey, connId);
        await Db.HashDeleteAsync(MmGameTypesKey, connId);
    }

    // ─── Challenge ──────────────────────────────────────────────────────────

    public async Task ChallengePlayer(string targetName, int gameType = 1)
    {
        var challengerId = Context.ConnectionId;

        var targetConnIdRaw = await Db.HashGetAsync(LobbyNamesKey, targetName);
        if (targetConnIdRaw.IsNullOrEmpty || targetConnIdRaw.ToString() == challengerId) return;

        var targetConnId = targetConnIdRaw.ToString();
        await Db.HashSetAsync(ChallengesKey, targetConnId, challengerId);
        await Db.HashSetAsync(ChallengeGameTypesKey, targetConnId, gameType.ToString());

        string challengerName = "Unknown";
        string challengerAvatar = "🧙‍♀️";
        var raw = await Db.HashGetAsync(LobbyPlayersKey, challengerId);
        if (!raw.IsNullOrEmpty)
        {
            var info = JsonSerializer.Deserialize<LobbyPlayer>(raw.ToString());
            if (info != null) { challengerName = info.Name; challengerAvatar = info.Avatar; }
        }

        await Clients.Client(targetConnId).ChallengeReceived(challengerName, challengerAvatar);
    }

    public async Task AcceptChallenge()
    {
        var acceptingId = Context.ConnectionId;

        var challengerIdRaw = await Db.HashGetAsync(ChallengesKey, acceptingId);
        if (challengerIdRaw.IsNullOrEmpty) return;
        var challengerId = challengerIdRaw.ToString();
        await Db.HashDeleteAsync(ChallengesKey, acceptingId);

        var gameTypeRaw = await Db.HashGetAsync(ChallengeGameTypesKey, acceptingId);
        var gameType = gameTypeRaw.IsNullOrEmpty || !int.TryParse(gameTypeRaw.ToString(), out var gt) ? 1 : gt;
        await Db.HashDeleteAsync(ChallengeGameTypesKey, acceptingId);

        foreach (var id in new[] { challengerId, acceptingId })
        {
            if (_matchmakingTimers.TryRemove(id, out var cts)) cts.Cancel();
            await Db.SortedSetRemoveAsync(MmQueueKey, id);
            await Db.HashDeleteAsync(MmLevelsKey, id);
            _playerStates.GetOrAdd(id, _ => new GameStateMachine());
            if (_playerStates.TryGetValue(id, out var sm) && sm.CanTransitionTo(GameState.Matchmaking))
                sm.TransitionTo(GameState.Matchmaking);
        }

        string acceptingName = "Opponent";
        var raw = await Db.HashGetAsync(LobbyPlayersKey, acceptingId);
        if (!raw.IsNullOrEmpty)
        {
            var info = JsonSerializer.Deserialize<LobbyPlayer>(raw.ToString());
            if (info != null) acceptingName = info.Name;
        }

        await Clients.Client(challengerId).ChallengeAccepted(acceptingName);
        await StartMatch(challengerId, acceptingId, gameType);
    }

    public async Task DeclineChallenge()
    {
        var decliningId = Context.ConnectionId;

        var challengerIdRaw = await Db.HashGetAsync(ChallengesKey, decliningId);
        if (challengerIdRaw.IsNullOrEmpty) return;
        var challengerId = challengerIdRaw.ToString();
        await Db.HashDeleteAsync(ChallengesKey, decliningId);

        string declinerName = "Opponent";
        var raw = await Db.HashGetAsync(LobbyPlayersKey, decliningId);
        if (!raw.IsNullOrEmpty)
        {
            var info = JsonSerializer.Deserialize<LobbyPlayer>(raw.ToString());
            if (info != null) declinerName = info.Name;
        }

        await Clients.Client(challengerId).ChallengeDeclined(declinerName);
    }

    // ─── Match ──────────────────────────────────────────────────────────────

    private static IGamePlugin CreateGamePlugin(int gameType) => gameType switch
    {
        2 => new SpeedReactionGame(),
        _ => new MemoryColorsGame()
    };

    private async Task StartMatch(string player1Id, string player2Id, int gameType = 1)
    {
        string p1Name = "Player1", p1Avatar = "🧙‍♀️";
        string p2Name = "Player2", p2Avatar = "🧙‍♂️";

        var r1 = await Db.HashGetAsync(LobbyPlayersKey, player1Id);
        if (!r1.IsNullOrEmpty)
        {
            var i = JsonSerializer.Deserialize<LobbyPlayer>(r1.ToString());
            if (i != null) { p1Name = i.Name; p1Avatar = i.Avatar; }
        }

        var r2 = await Db.HashGetAsync(LobbyPlayersKey, player2Id);
        if (!r2.IsNullOrEmpty)
        {
            var i = JsonSerializer.Deserialize<LobbyPlayer>(r2.ToString());
            if (i != null) { p2Name = i.Name; p2Avatar = i.Avatar; }
        }

        var roomId = $"game_{Guid.NewGuid():N}";
        await Db.HashSetAsync(PlayerRoomsKey, player1Id, roomId);
        await Db.HashSetAsync(PlayerRoomsKey, player2Id, roomId);

        var gs = new RoomGameState { GameEngine = CreateGamePlugin(gameType), GameTypeId = gameType };
        gs.PlayerConnectionIds[player1Id] = "Player1";
        gs.PlayerConnectionIds[player2Id] = "Player2";
        _roomGameStates[roomId] = gs;

        await Groups.AddToGroupAsync(player1Id, roomId);
        await Groups.AddToGroupAsync(player2Id, roomId);

        await Clients.Client(player1Id).PlayerAssigned("Player1");
        await Clients.Client(player2Id).PlayerAssigned("Player2");

        await Clients.Client(player1Id).MatchFound(p2Name, p2Avatar, gameType, 1, 3);
        await Clients.Client(player2Id).MatchFound(p1Name, p1Avatar, gameType, 1, 3);

        foreach (var pid in new[] { player1Id, player2Id })
        {
            if (_playerStates.TryGetValue(pid, out var sm) && sm.CanTransitionTo(GameState.GameStarting))
                sm.TransitionTo(GameState.GameStarting);
        }

        await StartRound(roomId, 1);
    }

    private async Task StartRound(string roomId, int round)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gs)) return;

        gs.RoundCts?.Cancel();
        gs.RoundCts?.Dispose();
        gs.RoundCts = new CancellationTokenSource();
        var ct = gs.RoundCts.Token;
        gs.CurrentRound = round;

        var roundData = gs.GameEngine.GenerateData(round, round);
        gs.CurrentRoundData = roundData;

        await Clients.Group(roomId).RoundStarting(round, 3);

        try
        {
            await Clients.Group(roomId).ShowColors(roundData, 2000);
            await Task.Delay(2000, ct);
            if (ct.IsCancellationRequested) return;

            await Clients.Group(roomId).HideColors();
            await Clients.Group(roomId).RoundInputPhase();
            gs.RoundStartTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            gs.ClearAnswers();
        }
        catch (OperationCanceledException) { }
    }

    public async Task SubmitAnswer(string[] colors)
    {
        var connId = Context.ConnectionId;

        var roomIdRaw = await Db.HashGetAsync(PlayerRoomsKey, connId);
        if (roomIdRaw.IsNullOrEmpty) return;
        var roomId = roomIdRaw.ToString();

        if (!_roomGameStates.TryGetValue(roomId, out var gs)) return;

        var timeMs = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - gs.RoundStartTimeMs);
        gs.TryAddAnswer(connId, (timeMs, colors));

        var answerCount   = gs.GetAnswerCount();
        var playersInRoom = gs.PlayerConnectionIds.Count;

        if (answerCount >= 2 || playersInRoom == 1)
        {
            if (gs.TryStartRoundProcessing())
                await ProcessRoundResults(roomId);
        }
        else
        {
            await Clients.OthersInGroup(roomId).OpponentFinished("You");
        }
    }

    private async Task ProcessRoundResults(string roomId)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gs)) return;

        var roundResults    = new Dictionary<string, object>();
        var scores          = new Dictionary<string, int>();
        var answersSnapshot = gs.GetAnswerSnapshot();

        foreach (var (playerId, (timeMs, answers)) in answersSnapshot)
        {
            var validation = gs.GameEngine.ValidateAnswer(answers, gs.CurrentRoundData);
            var score      = gs.GameEngine.CalculateScore(timeMs, validation.CorrectCount, validation.IsPerfect);

            gs.PlayerScores[playerId] = gs.PlayerScores.GetValueOrDefault(playerId) + score;
            if (validation.IsPerfect)
                gs.PlayerPerfectRounds[playerId] = gs.PlayerPerfectRounds.GetValueOrDefault(playerId) + 1;

            scores[playerId] = score;
            roundResults[playerId] = new
            {
                Score       = score,
                TotalScore  = gs.PlayerScores[playerId],
                validation.CorrectCount,
                validation.TotalCount,
                validation.IsPerfect,
                TimeMs      = timeMs,
                PlayerAlias = gs.PlayerConnectionIds.TryGetValue(playerId, out var label) ? label : playerId
            };
        }

        foreach (var playerId in answersSnapshot.Keys)
        {
            var opponentId    = answersSnapshot.Keys.FirstOrDefault(k => k != playerId);
            var myScore       = scores.GetValueOrDefault(playerId);
            var opponentScore = opponentId != null ? scores.GetValueOrDefault(opponentId) : 0;

            await Clients.Client(playerId).RoundResult(new
            {
                Round         = gs.CurrentRound,
                YourScore     = myScore,
                OpponentScore = opponentScore,
                Results       = roundResults
            });
        }

        if (gs.CurrentRound >= 3)
            await EndMatch(roomId);
        else
        {
            await Task.Delay(3000);
            await StartRound(roomId, gs.CurrentRound + 1);
        }
    }

    private async Task EndMatch(string roomId)
    {
        if (!_roomGameStates.TryGetValue(roomId, out var gs)) return;

        string p1Id = "", p2Id = "";
        foreach (var (k, v) in gs.PlayerConnectionIds)
        {
            if (v == "Player1") p1Id = k;
            else if (v == "Player2") p2Id = k;
        }

        int p1Score  = gs.PlayerScores.GetValueOrDefault(p1Id);
        int p2Score  = gs.PlayerScores.GetValueOrDefault(p2Id);
        bool isTie   = p1Score == p2Score;
        string winner = isTie ? "" : (p1Score > p2Score ? "Player1" : "Player2");

        int p1Xp = 24 + (gs.PlayerPerfectRounds.GetValueOrDefault(p1Id) * 5) + (!isTie && winner == "Player1" ? 10 : 0);
        int p2Xp = 24 + (gs.PlayerPerfectRounds.GetValueOrDefault(p2Id) * 5) + (!isTie && winner == "Player2" ? 10 : 0);

        await Clients.Group(roomId).MatchOver(new
        {
            Results = new Dictionary<string, object>
            {
                ["Player1"] = new { TotalScore = p1Score, IsWinner = !isTie && winner == "Player1", IsTie = isTie, XpEarned = p1Xp },
                ["Player2"] = new { TotalScore = p2Score, IsWinner = !isTie && winner == "Player2", IsTie = isTie, XpEarned = p2Xp }
            },
            Winner          = isTie ? "Tie" : winner,
            Player1Score    = p1Score,
            Player2Score    = p2Score,
            Player1XpEarned = p1Xp,
            Player2XpEarned = p2Xp,
            IsTie           = isTie
        });

        gs.RoundCts?.Cancel();
        gs.RoundCts?.Dispose();
        _roomGameStates.TryRemove(roomId, out _);

        foreach (var pid in gs.PlayerConnectionIds.Keys)
        {
            if (_playerStates.TryGetValue(pid, out var sm) && sm.CanTransitionTo(GameState.GameEnded))
                sm.TransitionTo(GameState.GameEnded);
        }

        await SaveMatchResultsToDatabase(p1Id, p2Id, p1Score, p2Score, winner, isTie, p1Xp, p2Xp, gs.GameTypeId);
    }

    // ─── Lobby leave ────────────────────────────────────────────────────────

    public async Task LeaveLobby()
    {
        var connId = Context.ConnectionId;

        if (_matchmakingTimers.TryRemove(connId, out var cts)) cts.Cancel();
        await Db.SortedSetRemoveAsync(MmQueueKey, connId);
        await Db.HashDeleteAsync(MmLevelsKey, connId);
        await Db.HashDeleteAsync(MmGameTypesKey, connId);
        _playerStates.TryRemove(connId, out _);

        var roomIdRaw = await Db.HashGetAsync(PlayerRoomsKey, connId);
        if (!roomIdRaw.IsNullOrEmpty)
        {
            var room = roomIdRaw.ToString();
            await Groups.RemoveFromGroupAsync(connId, room);
            await Db.HashDeleteAsync(PlayerRoomsKey, connId);

            if (_roomGameStates.TryGetValue(room, out var roomGs) &&
                !roomGs.PlayerConnectionIds.Keys.Any(k => k != connId))
            {
                roomGs.RoundCts?.Cancel();
                _roomGameStates.TryRemove(room, out _);
            }
        }

        var playerJsonRaw = await Db.HashGetAsync(LobbyPlayersKey, connId);
        if (!playerJsonRaw.IsNullOrEmpty)
        {
            var info = JsonSerializer.Deserialize<LobbyPlayer>(playerJsonRaw.ToString());
            if (info != null)
            {
                await Db.HashDeleteAsync(LobbyNamesKey, info.Name);
                await Clients.Group("lobby").PlayerLeft(info.Name);
            }
        }

        await Db.HashDeleteAsync(LobbyPlayersKey, connId);
        await Db.HashDeleteAsync(ConnUsersKey, connId);
        await Groups.RemoveFromGroupAsync(connId, "lobby");
    }

    // ─── Connection lifecycle ────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            Context.Items["UserId"] = userId;
            await Db.HashSetAsync(ConnUsersKey, Context.ConnectionId, userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connId = Context.ConnectionId;

        if (_matchmakingTimers.TryRemove(connId, out var cts)) cts.Cancel();
        await Db.SortedSetRemoveAsync(MmQueueKey, connId);
        await Db.HashDeleteAsync(MmLevelsKey, connId);
        await Db.HashDeleteAsync(MmGameTypesKey, connId);
        await Db.HashDeleteAsync(ChallengesKey, connId);
        await Db.HashDeleteAsync(ChallengeGameTypesKey, connId);

        var roomIdRaw = await Db.HashGetAsync(PlayerRoomsKey, connId);
        if (!roomIdRaw.IsNullOrEmpty)
        {
            var roomId = roomIdRaw.ToString();
            if (_roomGameStates.TryGetValue(roomId, out var gs))
            {
                gs.RoundCts?.Cancel();

                var remainingId = gs.PlayerConnectionIds.Keys.FirstOrDefault(k => k != connId);
                if (remainingId != null)
                {
                    gs.PlayerConnectionIds.TryGetValue(remainingId, out var remainingLabel);
                    int remainingScore    = gs.PlayerScores.GetValueOrDefault(remainingId);
                    int disconnectedScore = gs.PlayerScores.GetValueOrDefault(connId);
                    bool p1IsRemaining    = remainingLabel == "Player1";
                    int p1Score = p1IsRemaining ? remainingScore : disconnectedScore;
                    int p2Score = p1IsRemaining ? disconnectedScore : remainingScore;
                    int p1Xp   = p1IsRemaining ? 34 : 0;
                    int p2Xp   = p1IsRemaining ? 0 : 34;

                    await Clients.Client(remainingId).MatchOver(new
                    {
                        Results = new Dictionary<string, object>
                        {
                            ["Player1"] = new { TotalScore = p1Score, IsWinner = p1IsRemaining, IsTie = false, XpEarned = p1Xp },
                            ["Player2"] = new { TotalScore = p2Score, IsWinner = !p1IsRemaining, IsTie = false, XpEarned = p2Xp }
                        },
                        Winner          = remainingLabel,
                        Player1Score    = p1Score,
                        Player2Score    = p2Score,
                        Player1XpEarned = p1Xp,
                        Player2XpEarned = p2Xp,
                        IsTie           = false
                    });

                    var winnerUserIdRaw = await Db.HashGetAsync(ConnUsersKey, remainingId);
                    if (!winnerUserIdRaw.IsNullOrEmpty)
                        await SavePlayerMatchResult(winnerUserIdRaw.ToString(), 1, remainingScore, true, 34);
                }

                _roomGameStates.TryRemove(roomId, out _);
            }
        }

        await LeaveLobby();
        await base.OnDisconnectedAsync(exception);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<int> GetPlayerLevel(string connectionId)
    {
        var userIdRaw = await Db.HashGetAsync(ConnUsersKey, connectionId);
        if (userIdRaw.IsNullOrEmpty || !Guid.TryParse(userIdRaw.ToString(), out var userGuid))
            return 1;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkillForgeDbContext>();
        var user = await db.Users.FindAsync(userGuid);
        return user?.CurrentLevel ?? 1;
    }

    private async Task SaveMatchResultsToDatabase(string p1Id, string p2Id, int p1Score, int p2Score, string winner, bool isTie, int p1Xp, int p2Xp, int gameType = 1)
    {
        var p1UserIdRaw = await Db.HashGetAsync(ConnUsersKey, p1Id);
        var p2UserIdRaw = await Db.HashGetAsync(ConnUsersKey, p2Id);
        var p1UserId = p1UserIdRaw.IsNullOrEmpty ? "" : p1UserIdRaw.ToString();
        var p2UserId = p2UserIdRaw.IsNullOrEmpty ? "" : p2UserIdRaw.ToString();

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (!string.IsNullOrEmpty(p1UserId))
                    await SavePlayerMatchResult(p1UserId, gameType, p1Score, winner == "Player1" && !isTie, p1Xp);
                if (!string.IsNullOrEmpty(p2UserId))
                    await SavePlayerMatchResult(p2UserId, gameType, p2Score, winner == "Player2" && !isTie, p2Xp);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Retry {attempt}/{maxRetries}] DB save error: {ex.Message}");
                if (attempt < maxRetries) await Task.Delay(500 * attempt);
                else Console.WriteLine("All retries exhausted.");
            }
        }
    }

    private async Task SavePlayerMatchResult(string userId, int gameType, int score, bool won, int xpEarned)
    {
        if (!Guid.TryParse(userId, out var userGuid)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkillForgeDbContext>();

        db.MatchHistories.Add(new MatchHistory
        {
            UserId   = userGuid,
            GameType = gameType,
            Score    = score,
            Won      = won,
            XPEarned = xpEarned,
            PlayedAt = DateTime.UtcNow
        });

        var user = await db.Users.FindAsync(userGuid);
        if (user != null)
        {
            user.TotalXp     += xpEarned;
            user.UpdatedAt    = DateTime.UtcNow;
            user.CurrentLevel = (user.TotalXp / 100) + 1;
        }

        // Update UserSkill for skill-specific type and "overall"
        var skillType = gameType switch { 1 => "memory", 2 => "speed", _ => "overall" };
        foreach (var st in new[] { skillType, "overall" }.Distinct())
        {
            var skill = await db.UserSkills.FirstOrDefaultAsync(s => s.UserId == userGuid && s.SkillType == st);
            if (skill == null)
            {
                skill = new UserSkill { UserId = userGuid, SkillType = st };
                db.UserSkills.Add(skill);
            }
            skill.XP          += xpEarned;
            skill.Level        = (skill.XP / 100) + 1;
            skill.GamesPlayed += 1;
            if (won) skill.GamesWon += 1;
            skill.LastUpdated  = DateTime.UtcNow;

            // Percentile: excludes current user so denominator is correct for new and existing users.
            var otherPlayers = await db.UserSkills.CountAsync(s => s.SkillType == st && s.UserId != userGuid);
            var lowerPlayers = await db.UserSkills.CountAsync(s => s.SkillType == st && s.UserId != userGuid && s.XP < skill.XP);
            skill.Percentile  = otherPlayers > 0 ? Math.Round((decimal)lowerPlayers / otherPlayers * 100, 2) : 50m;

            db.SkillSnapshots.Add(new SkillSnapshot
            {
                UserId     = userGuid,
                SkillType  = st,
                XP         = skill.XP,
                Level      = skill.Level,
                Percentile = skill.Percentile,
                RecordedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        // Refresh leaderboard view asynchronously — fire and forget, non-blocking.
        // Uses a fresh scope so the captured db (from the outer using scope) is not accessed after disposal.
        _ = Task.Run(async () =>
        {
            try
            {
                using var refreshScope = _scopeFactory.CreateScope();
                var refreshDb = refreshScope.ServiceProvider.GetRequiredService<SkillForgeDbContext>();
                await refreshDb.Database.ExecuteSqlRawAsync(
                    "REFRESH MATERIALIZED VIEW CONCURRENTLY mv_leaderboard");
            }
            catch { /* view refresh is best-effort */ }
        });
    }
}

// Local DTO for Redis serialization
file sealed record LobbyPlayer(string Name, string Avatar);
