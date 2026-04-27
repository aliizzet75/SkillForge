using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillForge.Core.Data;

namespace SkillForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly SkillForgeDbContext _context;

    public LeaderboardController(SkillForgeDbContext context)
    {
        _context = context;
    }

    [HttpGet("global")]
    public async Task<IActionResult> GetGlobalLeaderboard(
        [FromQuery] string skillType = "overall",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = _context.UserSkills
            .Where(us => us.SkillType == skillType)
            .OrderByDescending(us => us.XP)
            .ThenByDescending(us => us.GamesPlayed);

        var totalCount = await baseQuery.CountAsync();

        var users = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(us => new
            {
                userId = us.UserId,
                username = us.User.Username,
                countryCode = us.User.CountryCode,
                level = us.Level,
                xp = us.XP,
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
            u.countryCode,
            u.level,
            u.xp,
            u.percentile,
            u.gamesPlayed,
            u.gamesWon
        });

        return Ok(new
        {
            skillType,
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            users = rankedUsers
        });
    }

    [HttpGet("country/{countryCode}")]
    public async Task<IActionResult> GetCountryLeaderboard(
        string countryCode,
        [FromQuery] string skillType = "overall",
        [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);
        var normalizedCountry = countryCode.ToUpper();

        var users = await _context.UserSkills
            .Where(us => us.SkillType == skillType)
            .Where(us => us.User.CountryCode == normalizedCountry)
            .OrderByDescending(us => us.XP)
            .ThenByDescending(us => us.GamesPlayed)
            .Take(limit)
            .Select(us => new
            {
                userId = us.UserId,
                username = us.User.Username,
                level = us.Level,
                xp = us.XP,
                percentile = us.Percentile,
                gamesPlayed = us.GamesPlayed,
                gamesWon = us.GamesWon
            })
            .ToListAsync();

        var rankedUsers = users.Select((u, i) => new
        {
            rank = i + 1,
            u.userId,
            u.username,
            u.level,
            u.xp,
            u.percentile,
            u.gamesPlayed,
            u.gamesWon
        });

        return Ok(new
        {
            countryCode = normalizedCountry,
            skillType,
            totalCount = rankedUsers.Count(),
            users = rankedUsers
        });
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
}
