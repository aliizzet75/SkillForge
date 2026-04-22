namespace SkillForge.Api.Services;

/// <summary>
/// Provides access to the current user context for SignalR hubs
/// </summary>
public interface IUserContextAccessor
{
    string? GetUserId();
    string? GetUsername();
}

/// <summary>
/// Implementation that extracts user info from HttpContext
/// </summary>
public class HttpUserContextAccessor : IUserContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpUserContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetUserId()
    {
        if (_httpContextAccessor.HttpContext?.Items?.TryGetValue("UserId", out var userId) == true)
        {
            return userId?.ToString();
        }
        return null;
    }

    public string? GetUsername()
    {
        if (_httpContextAccessor.HttpContext?.Items?.TryGetValue("Username", out var username) == true)
        {
            return username?.ToString();
        }
        return null;
    }
}
