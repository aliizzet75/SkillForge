using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SkillForge.Api.Middleware;

public class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public JwtAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only process SignalR hub requests
        if (!context.Request.Path.StartsWithSegments("/hubs/game"))
        {
            await _next(context);
            return;
        }

        // Try to get token from query string (SignalR sends token this way)
        var token = context.Request.Query["access_token"].FirstOrDefault();

        // Fallback to Authorization header
        if (string.IsNullOrEmpty(token) && context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerValue = authHeader.ToString();
            if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = headerValue.Substring(7);
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        try
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var keyString = Environment.GetEnvironmentVariable("JWT_KEY") ?? jwtSettings["Key"];

            if (string.IsNullOrEmpty(keyString))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var tokenHandler = new JwtSecurityTokenHandler();

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"] ?? "SkillForge",
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"] ?? "SkillForgeUsers",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            var username = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // Assign validated principal so SignalR Context.User is populated
            context.User = principal;
            context.Items["UserId"] = userId;
            context.Items["Username"] = username ?? "Unknown";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JWT validation failed: {ex.Message}");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}

public static class JwtAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<JwtAuthenticationMiddleware>();
    }
}
