using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkillForge.Core.Data;

namespace SkillForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly SkillForgeDbContext _context;
    private readonly IMemoryCache _cache;
    private const int CacheExpirationMinutes = 5;

    public LeaderboardController(SkillForgeDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet("global")]
    public async Task<IActionResult> GetGlobalLeaderboard(
        [FromQuery] string skillType = "overall",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var cacheKey = $"leaderboard_global_{skillType}_{page}_{pageSize}";
        
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            return Ok(cachedResult);
        }

        var query = _context.UserSkills
            .Where(us => us.SkillType == skillType)
            .OrderByDescending(us => us.Percentile)
            .ThenByDescending(us => us.XP)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var users = await query
            .Select(us => new
            {
                rank = 0,
                userId = us.UserId,
                username = us.User.Username,
                avatar = us.User.Avatar,
                countryCode = us.User.CountryCode,
                level = us.Level,
                xp = us.XP,
                totalXp = us.User.TotalXp,
                percentile = us.Percentile,
                gamesPlayed = us.GamesPlayed,
                gamesWon = us.GamesWon
            })
            .ToListAsync();

        // Calculate ranks
        var startRank = (page - 1) * pageSize + 1;
        var rankedUsers = users.Select((u, i) => new
        {
            rank = startRank + i,
            u.userId,
            u.username,
            u.avatar,
            u.countryCode,
            u.level,
            u.xp,
            u.totalXp,
            u.percentile,
            u.gamesPlayed,
            u.gamesWon
        });

        var result = new
        {
            skillType,
            page,
            pageSize,
            users = rankedUsers
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(CacheExpirationMinutes));

        return Ok(result);
    }

    [HttpGet("country/{countryCode}")]
    public async Task<IActionResult> GetCountryLeaderboard(
        string countryCode,
        [FromQuery] string skillType = "overall",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var cacheKey = $"leaderboard_country_{countryCode.ToUpper()}_{skillType}_{page}_{pageSize}";
        
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            return Ok(cachedResult);
        }

        var query = _context.UserSkills
            .Where(us => us.SkillType == skillType)
            .Where(us => us.User.CountryCode == countryCode.ToUpper())
            .OrderByDescending(us => us.Percentile)
            .ThenByDescending(us => us.XP)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var users = await query
            .Select(us => new
            {
                rank = 0,
                userId = us.UserId,
                username = us.User.Username,
                avatar = us.User.Avatar,
                level = us.Level,
                xp = us.XP,
                totalXp = us.User.TotalXp,
                percentile = us.Percentile,
                gamesPlayed = us.GamesPlayed,
                gamesWon = us.GamesWon
            })
            .ToListAsync();

        // Calculate ranks
        var startRank = (page - 1) * pageSize + 1;
        var rankedUsers = users.Select((u, i) => new
        {
            rank = startRank + i,
            u.userId,
            u.username,
            u.avatar,
            u.level,
            u.xp,
            u.totalXp,
            u.percentile,
            u.gamesPlayed,
            u.gamesWon
        });

        var result = new
        {
            countryCode = countryCode.ToUpper(),
            skillType,
            page,
            pageSize,
            users = rankedUsers
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(CacheExpirationMinutes));

        return Ok(result);
    }

    [HttpGet("nearby/{userId:guid}")]
    public async Task<IActionResult> GetNearbyPlayers(Guid userId, [FromQuery] string skillType = "overall")
    {
        var userSkill = await _context.UserSkills
            .FirstOrDefaultAsync(us => us.UserId == userId && us.SkillType == skillType);

        if (userSkill == null)
        {
            return NotFound(new { error = "User skill not found" });
        }

        // Get 5 players above and 5 below
        var above = await _context.UserSkills
            .Where(us => us.SkillType == skillType)
            .Where(us => us.Percentile > userSkill.Percentile || (us.Percentile == userSkill.Percentile && us.XP > userSkill.XP))
            .OrderBy(us => us.Percentile)
            .ThenBy(us => us.XP)
            .Take(5)
            .Select(us => new
            {
                userId = us.UserId,
                username = us.User.Username,
                level = us.Level,
                xp = us.XP,
                percentile = us.Percentile,
                relativePosition = "above"
            })
            .ToListAsync();

        var below = await _context.UserSkills
            .Where(us => us.SkillType == skillType)
            .Where(us => us.Percentile < userSkill.Percentile || (us.Percentile == userSkill.Percentile && us.XP < userSkill.XP))
            .OrderByDescending(us => us.Percentile)
            .ThenByDescending(us => us.XP)
            .Take(5)
            .Select(us => new
            {
                userId = us.UserId,
                username = us.User.Username,
                level = us.Level,
                xp = us.XP,
                percentile = us.Percentile,
                relativePosition = "below"
            })
            .ToListAsync();

        return Ok(new
        {
            skillType,
            yourPercentile = userSkill.Percentile,
            yourLevel = userSkill.Level,
            above = above.AsEnumerable().Reverse().ToList(), // Show closest first
            below
        });
    }

    [HttpGet("skill/{skillType}")]
    public async Task<IActionResult> GetSkillLeaderboard(
        string skillType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Same as global but explicitly for a specific skill type
        var cacheKey = $"leaderboard_skill_{skillType}_{page}_{pageSize}";
        
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            return Ok(cachedResult);
        }

        var query = _context.UserSkills
            .Where(us => us.SkillType == skillType)
            .OrderByDescending(us => us.Percentile)
            .ThenByDescending(us => us.XP)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var users = await query
            .Select(us => new
            {
                rank = 0,
                userId = us.UserId,
                username = us.User.Username,
                avatar = us.User.Avatar,
                countryCode = us.User.CountryCode,
                level = us.Level,
                xp = us.XP,
                totalXp = us.User.TotalXp,
                percentile = us.Percentile,
                gamesPlayed = us.GamesPlayed,
                gamesWon = us.GamesWon
            })
            .ToListAsync();

        var startRank = (page - 1) * pageSize + 1;
        var rankedUsers = users.Select((u, i) => new
        {
            rank = startRank + i,
            u.userId,
            u.username,
            u.avatar,
            u.countryCode,
            u.level,
            u.xp,
            u.totalXp,
            u.percentile,
            u.gamesPlayed,
            u.gamesWon
        });

        var result = new
        {
            skillType,
            page,
            pageSize,
            users = rankedUsers
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(CacheExpirationMinutes));

        return Ok(result);
    }
}
