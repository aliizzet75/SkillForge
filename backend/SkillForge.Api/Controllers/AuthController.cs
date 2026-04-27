using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SkillForge.Core.Data;
using SkillForge.Core.Models;
using SkillForge.Core.Services;
using System.ComponentModel.DataAnnotations;
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
    private readonly IConfiguration _configuration;

    public AuthController(SkillForgeDbContext context, IJwtService jwtService, IConfiguration configuration)
    {
        _context = context;
        _jwtService = jwtService;
        _configuration = configuration;
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
            Avatar = (request.Avatar != null && AllowedAvatars.Values.Contains(request.Avatar))
                ? request.Avatar
                : "🧙‍♀️"
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
        var input = request.Username;
        var user = await _context.Users
            .Include(u => u.Skills)
            .FirstOrDefaultAsync(u => u.Username == input || u.Email == input);

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

        var user = await _context.Users
            .Include(u => u.Skills)
            .FirstOrDefaultAsync(u => u.Id == guid);
        if (user == null) return NotFound();

        return Ok(new
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
        });
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user != null)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-").Replace("/", "_").Replace("=", "");
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            // TODO: send reset email with link to /reset-password?token={token}
        }
        return Ok(new { message = "If that email is registered, you will receive a password reset link shortly." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (request.NewPassword.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == request.Token &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
            return BadRequest(new { error = "Invalid or expired reset token." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password updated successfully." });
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

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
