using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillForge.Core.Data;
using SkillForge.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SkillForge.Api.Controllers;

public class UpdateAvatarRequest
{
    [Required, MinLength(1)]
    public string Avatar { get; set; } = "🧙‍♀️";
}

file static class AllowedAvatars
{
    internal static readonly HashSet<string> Values = ["🧙‍♀️", "🧙‍♂️", "🦸‍♀️", "🦸‍♂️", "👩‍🔬", "👨‍🔬", "🧚‍♀️", "🧚‍♂️", "👩‍🚀", "👨‍🚀"];
}

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

    [Authorize]
    [HttpGet("{id:guid}/insights")]
    public async Task<IActionResult> GetInsights(Guid id)
    {
        var callerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (callerId == null || !Guid.TryParse(callerId, out var callerGuid) || callerGuid != id)
            return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "User not found" });

        var now = DateTime.UtcNow;
        var oneWeekAgo = now.AddDays(-7);
        var twoWeeksAgo = now.AddDays(-14);

        var insights = new List<object>();

        foreach (var skillType in new[] { "overall", "memory" })
        {
            var thisWeek = await _context.SkillSnapshots
                .Where(ss => ss.UserId == id && ss.SkillType == skillType && ss.RecordedAt >= oneWeekAgo)
                .OrderBy(ss => ss.RecordedAt)
                .ToListAsync();

            var lastWeek = await _context.SkillSnapshots
                .Where(ss => ss.UserId == id && ss.SkillType == skillType && ss.RecordedAt >= twoWeeksAgo && ss.RecordedAt < oneWeekAgo)
                .OrderBy(ss => ss.RecordedAt)
                .ToListAsync();

            if (thisWeek.Count == 0) continue;

            var xpGainedThisWeek = thisWeek.Count > 1 ? thisWeek.Last().XP - thisWeek.First().XP : 0;
            var xpGainedLastWeek = lastWeek.Count > 1 ? lastWeek.Last().XP - lastWeek.First().XP : 0;

            var percentileStart = thisWeek.First().Percentile;
            var percentileEnd = thisWeek.Last().Percentile;
            var percentileChange = percentileEnd - percentileStart;

            string? message = null;
            if (xpGainedLastWeek > 0 && xpGainedThisWeek > 0)
            {
                var changePercent = (int)Math.Round((double)(xpGainedThisWeek - xpGainedLastWeek) / xpGainedLastWeek * 100);
                var skillLabel = skillType == "memory" ? "Memory" : "Gesamt";
                if (Math.Abs(changePercent) >= 5)
                    message = changePercent > 0
                        ? $"Dein {skillLabel}-Skill hat sich diese Woche um {changePercent}% verbessert"
                        : $"Dein {skillLabel}-Skill war diese Woche {Math.Abs(changePercent)}% schwächer als letzte Woche";
            }
            else if (xpGainedThisWeek > 0)
            {
                var skillLabel = skillType == "memory" ? "Memory" : "Gesamt";
                message = $"+{xpGainedThisWeek} XP diese Woche in {skillLabel}";
            }

            if (message != null)
                insights.Add(new { skillType, message, xpGained = xpGainedThisWeek, percentileChange });
        }

        // Streak: count consecutive days with at least one snapshot
        var recentDays = await _context.SkillSnapshots
            .Where(ss => ss.UserId == id && ss.RecordedAt >= now.AddDays(-30))
            .Select(ss => ss.RecordedAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

        int streak = 0;
        var checkDay = now.Date;
        foreach (var day in recentDays)
        {
            if (day == checkDay || day == checkDay.AddDays(-1))
            {
                streak++;
                checkDay = day;
            }
            else break;
        }

        return Ok(new { userId = id, insights, streakDays = streak });
    }

    [Authorize]
    [HttpPatch("{id:guid}/avatar")]
    public async Task<IActionResult> UpdateAvatar(Guid id, [FromBody] UpdateAvatarRequest request)
    {
        var callerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (callerId == null || !Guid.TryParse(callerId, out var callerGuid) || callerGuid != id)
            return Forbid();

        if (!AllowedAvatars.Values.Contains(request.Avatar))
            return BadRequest(new { error = "Invalid avatar" });

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "User not found" });

        user.Avatar = request.Avatar;
        await _context.SaveChangesAsync();
        return Ok(new { avatar = user.Avatar });
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
