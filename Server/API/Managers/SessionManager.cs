using Sunrise.Server.API.Services;
using Sunrise.Server.Objects;

namespace Sunrise.Server.API.Managers;

public static class SessionManager
{
    public static async Task<BaseSession?> GetSessionFromRequest(this HttpRequest request)
    {
        var token = request.Cookies["session"];

        var user = await AuthService.GetUserFromToken(token ?? "");
        if (user == null)
            return null;

        var session = new BaseSession(user);
        return session;
    }
}