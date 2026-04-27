using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillForge.Core.Data;
using SkillForge.Core.Models;

namespace SkillForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly SkillForgeDbContext _context;

    public UsersController(SkillForgeDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Skills)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            countryCode = user.CountryCode,
            createdAt = user.CreatedAt,
            lastSeenAt = user.LastSeenAt,
            skills = user.Skills.Select(s => new
            {
                type = s.SkillType,
                level = s.Level,
                xp = s.XP,
                percentile = s.Percentile,
                gamesPlayed = s.GamesPlayed,
                gamesWon = s.GamesWon
            })
        });
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetUserHistory(Guid id, [FromQuery] int days = 30)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        var sessions = await _context.GameSessions
            .Where(gs => gs.Player1Id == id || gs.Player2Id == id)
            .Where(gs => gs.EndedAt != null && gs.EndedAt > DateTime.UtcNow.AddDays(-days))
            .OrderByDescending(gs => gs.EndedAt)
            .Take(50)
            .ToListAsync();

        return Ok(sessions.Select(s => new
        {
            id = s.Id,
            gameType = s.GameType,
            mode = s.Mode,
            status = s.Status,
            startedAt = s.StartedAt,
            endedAt = s.EndedAt,
            myScore = s.Player1Id == id ? s.Player1Score : s.Player2Score,
            opponentScore = s.Player1Id == id ? s.Player2Score : s.Player1Score,
            won = s.WinnerId == id
        }));
    }

    [HttpGet("{id:guid}/skill-history")]
    public async Task<IActionResult> GetSkillHistory(Guid id, [FromQuery] string skillType = "overall", [FromQuery] int days = 30)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "User not found" });

        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));

        var snapshots = await _context.SkillSnapshots
            .Where(ss => ss.UserId == id && ss.SkillType == skillType && ss.RecordedAt >= since)
            .OrderBy(ss => ss.RecordedAt)
            .Select(ss => new
            {
                recordedAt = ss.RecordedAt,
                xp = ss.XP,
                level = ss.Level,
                percentile = ss.Percentile
            })
            .ToListAsync();

        return Ok(new { userId = id, skillType, days, snapshots });
    }

    [HttpGet("online")]
    public async Task<IActionResult> GetOnlineUsers([FromQuery] int limit = 20)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        
        var users = await _context.Users
            .Where(u => u.LastSeenAt != null && u.LastSeenAt > cutoff)
            .OrderByDescending(u => u.LastSeenAt)
            .Take(limit)
            .Select(u => new
            {
                id = u.Id,
                username = u.Username,
                countryCode = u.CountryCode,
                lastSeenAt = u.LastSeenAt
            })
            .ToListAsync();

        return Ok(users);
    }
}
