using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillForge.Core.Data;
using SkillForge.Core.Models;
using System.Text;

namespace SkillForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SkillForgeDbContext _context;

    public AuthController(SkillForgeDbContext context)
    {
        _context = context;
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
            Timezone = request.Timezone
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

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            message = "User registered successfully"
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

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            countryCode = user.CountryCode,
            skills = user.Skills.Select(s => new
            {
                type = s.SkillType,
                level = s.Level,
                xp = s.XP,
                percentile = s.Percentile
            })
        });
    }

    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? Timezone { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
