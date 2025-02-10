using Sunrise.Server.API.Services;
using Sunrise.Server.Objects;

namespace Sunrise.Server.API.Managers;

public static class SessionManager
{
    public static async Task<BaseSession?> GetSessionFromRequest(this HttpRequest request)
    {
        var header = request.Headers.Authorization;

        if (header.Count == 0)
            return null;

        if (header[0]?.StartsWith("Bearer ") == false)
            return null;

        var token = header[0]?.Split(" ")[1];
        if (string.IsNullOrEmpty(token))
            return null;

        var user = await AuthService.GetUserFromToken(token);
        if (user == null)
            return null;

        if (user.IsRestricted())
            return null;

        var session = new BaseSession(user);
        return session;
    }
}