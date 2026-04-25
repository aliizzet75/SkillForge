using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace SkillForge.Api.Middleware;

public class JwtHubFilter : IHubFilter
{
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var httpContext = context.Context.GetHttpContext();
        var userId = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            context.Context.Abort();
            return;
        }

        // Populate Context.Items once at connection time — thread-safe for concurrent hub method calls
        context.Context.Items["UserId"] = userId;
        context.Context.Items["Username"] = httpContext?.User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

        await next(context);
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (invocationContext.Context.Items["UserId"] is not string userId || string.IsNullOrEmpty(userId))
            throw new HubException("Unauthorized");

        return await next(invocationContext);
    }
}
