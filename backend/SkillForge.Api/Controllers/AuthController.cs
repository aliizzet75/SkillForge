using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillForge.Core.Data;
using SkillForge.Core.Models;
using SkillForge.Core.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SkillForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SkillForgeDbContext _context;
    private readonly IJwtService _jwtService;

    public AuthController(SkillForgeDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest(new { error = "Username already exists" });
        }

        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(new { error = "Email already exists" });
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            CountryCode = request.CountryCode,
            Timezone = request.Timezone,
            Avatar = request.Avatar ?? "🧙‍♀️"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Initialize skills
        var skills = new[] { "memory", "speed", "precision", "overall" };
        foreach (var skillType in skills)
        {
            _context.UserSkills.Add(new UserSkill
            {
                UserId = user.Id,
                SkillType = skillType,
                Level = 1,
                XP = 0,
                Percentile = 50.00m
            });
        }
        await _context.SaveChangesAsync();

        var token = _jwtService.GenerateJwtToken(user);
        return Ok(new
        {
            token,
            user = new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                avatar = user.Avatar,
                currentLevel = user.CurrentLevel,
                totalXp = user.TotalXp
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Skills)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash!))
        {
            return Unauthorized(new { error = "Invalid username or password" });
        }

        user.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = _jwtService.GenerateJwtToken(user);
        
        return Ok(new
        {
            token,
            user = new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                avatar = user.Avatar ?? "🧙‍♀️",
                countryCode = user.CountryCode,
                currentLevel = user.CurrentLevel,
                totalXp = user.TotalXp,
                skills = user.Skills.Select(s => new
                {
                    type = s.SkillType,
                    level = s.Level,
                    xp = s.XP,
                    percentile = s.Percentile
                })
            }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var guid)) return Unauthorized();

        var user = await _context.Users.FindAsync(guid);
        if (user == null) return NotFound();

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            avatar = user.Avatar ?? "🧙‍♀️",
            currentLevel = user.CurrentLevel,
            totalXp = user.TotalXp
        });
    }

    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private bool VerifyPassword(string password, string hash)
    {
        // Migration: supports both BCrypt (new) and SHA256 (legacy) hashes
        if (hash.StartsWith("$2"))
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        else
        {
            // Legacy SHA256 fallback for existing users
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes) == hash;
        }
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? Timezone { get; set; }
    public string? Avatar { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
