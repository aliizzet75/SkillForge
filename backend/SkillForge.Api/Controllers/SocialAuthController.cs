using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SkillForge.Core.Data;
using SkillForge.Core.Models;

namespace SkillForge.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class SocialAuthController : ControllerBase
    {
        private readonly SkillForgeDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SocialAuthController> _logger;

        public SocialAuthController(
            SkillForgeDbContext context,
            IConfiguration configuration,
            ILogger<SocialAuthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Initiate Google OAuth login
        /// </summary>
        [HttpGet("google")]
        public IActionResult GoogleLogin()
        {
            var redirectUrl = Url.Action(nameof(GoogleCallback), "SocialAuth", null, Request.Scheme);
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Google OAuth callback
        /// </summary>
        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            try
            {
                var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
                
                if (!authenticateResult.Succeeded)
                {
                    _logger.LogWarning("Google authentication failed");
                    return Redirect($"{_configuration["Frontend:Url"]}/login?error=google_auth_failed");
                }

                var claims = authenticateResult.Principal?.Identities?.FirstOrDefault()?.Claims;
                var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
                var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
                var googleId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(googleId))
                {
                    return Redirect($"{_configuration["Frontend:Url"]}/login?error=invalid_google_data");
                }

                // Find or create user
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.SocialProvider == "google" && u.SocialProviderId == googleId);

                if (user == null)
                {
                    // Check if email exists with password login
                    user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                    
                    if (user == null)
                    {
                        // Create new user
                        user = new User
                        {
                            Username = name ?? email.Split('@')[0],
                            Email = email,
                            DisplayName = name,
                            SocialProvider = "google",
                            SocialProviderId = googleId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true,
                            TotalXp = 0,
                            CurrentLevel = 1
                        };
                        _context.Users.Add(user);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // Link existing user to Google
                        user.SocialProvider = "google";
                        user.SocialProviderId = googleId;
                        user.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                // Generate JWT
                var token = GenerateJwtToken(user);

                // Redirect to frontend with token
                return Redirect($"{_configuration["Frontend:Url"]}/auth/callback?token={token}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Google callback");
                return Redirect($"{_configuration["Frontend:Url"]}/login?error=server_error");
            }
        }

        /// <summary>
        /// Initiate Facebook OAuth login
        /// </summary>
        [HttpGet("facebook")]
        public IActionResult FacebookLogin()
        {
            var redirectUrl = Url.Action(nameof(FacebookCallback), "SocialAuth", null, Request.Scheme);
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, FacebookDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Facebook OAuth callback
        /// </summary>
        [HttpGet("facebook-callback")]
        public async Task<IActionResult> FacebookCallback()
        {
            try
            {
                var authenticateResult = await HttpContext.AuthenticateAsync(FacebookDefaults.AuthenticationScheme);
                
                if (!authenticateResult.Succeeded)
                {
                    _logger.LogWarning("Facebook authentication failed");
                    return Redirect($"{_configuration["Frontend:Url"]}/login?error=facebook_auth_failed");
                }

                var claims = authenticateResult.Principal?.Identities?.FirstOrDefault()?.Claims;
                var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
                var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
                var facebookId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(facebookId))
                {
                    return Redirect($"{_configuration["Frontend:Url"]}/login?error=invalid_facebook_data");
                }

                // Find or create user
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.SocialProvider == "facebook" && u.SocialProviderId == facebookId);

                if (user == null)
                {
                    // Check if email exists
                    if (!string.IsNullOrEmpty(email))
                    {
                        user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                    }
                    
                    if (user == null)
                    {
                        // Create new user
                        user = new User
                        {
                            Username = name ?? $"fb_user_{facebookId.Substring(0, 8)}",
                            Email = email,
                            DisplayName = name,
                            SocialProvider = "facebook",
                            SocialProviderId = facebookId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true,
                            TotalXp = 0,
                            CurrentLevel = 1
                        };
                        _context.Users.Add(user);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // Link existing user to Facebook
                        user.SocialProvider = "facebook";
                        user.SocialProviderId = facebookId;
                        user.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                // Generate JWT
                var token = GenerateJwtToken(user);

                // Redirect to frontend with token
                return Redirect($"{_configuration["Frontend:Url"]}/auth/callback?token={token}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Facebook callback");
                return Redirect($"{_configuration["Frontend:Url"]}/login?error=server_error");
            }
        }

        /// <summary>
        /// Guest login - no authentication required
        /// </summary>
        [HttpPost("guest")]
        public async Task<ActionResult> GuestLogin()
        {
            try
            {
                var guestId = Guid.NewGuid().ToString("N")[..8];
                var user = new User
                {
                    Username = $"Guest_{guestId}",
                    DisplayName = $"Guest {guestId}",
                    SocialProvider = "guest",
                    SocialProviderId = guestId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true,
                    TotalXp = 0,
                    CurrentLevel = 1
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var token = GenerateJwtToken(user);

                return Ok(new
                {
                    Token = token,
                    User = new
                    {
                        Id = user.Id,
                        Username = user.Username,
                        DisplayName = user.DisplayName,
                        TotalXp = user.TotalXp,
                        CurrentLevel = user.CurrentLevel
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in guest login");
                return StatusCode(500, new { Error = "Failed to create guest account" });
            }
        }

        /// <summary>
        /// Get current authenticated user
        /// </summary>
        [HttpGet("me")]
        public async Task<ActionResult> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Avatar = user.Avatar,
                TotalXp = user.TotalXp,
                CurrentLevel = user.CurrentLevel,
                CountryCode = user.CountryCode
            });
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("SocialProvider", user.SocialProvider ?? ""),
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(24),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
