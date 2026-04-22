using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace SkillForge.Api.Middleware;

public class GameHubAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public GameHubAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if this is a SignalR negotiation or connection request
        if (context.Request.Path.StartsWithSegments("/hubs/game"))
        {
            // Try to get token from query string
            var token = context.Request.Query["access_token"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(token))
            {
                // Try to get token from Authorization header
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    token = authHeader.Substring("Bearer ".Length).Trim();
                }
            }

            if (!string.IsNullOrEmpty(token))
            {
                // Store token in HttpContext for later use
                context.Items["AccessToken"] = token;
            }
        }

        await _next(context);
    }
}