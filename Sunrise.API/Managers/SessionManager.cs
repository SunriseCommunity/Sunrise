using Microsoft.AspNetCore.Http;
using Sunrise.API.Services;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.API.Managers;

public class SessionManager(AuthService authService)
{
    public async Task<BaseSession?> GetSessionFromRequest(HttpRequest request, CancellationToken ct = default)
    {
        var header = request.Headers.Authorization;

        if (header.Count == 0)
            return null;

        if (header[0]?.StartsWith("Bearer ") == false)
            return null;

        var token = header[0]?.Split(" ")[1];
        if (string.IsNullOrEmpty(token))
            return null;

        var user = await authService.GetUserFromToken(token, ct);
        if (user == null || user.IsUserSunriseBot())
            return null;

        var session = new BaseSession(user);
        return session;
    }
}