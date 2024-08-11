using Sunrise.Server.Data;
using Sunrise.Server.Repositories;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Helpers;

public static class AuthorizationHelper
{
    public static async Task<bool> IsAuthorized(HttpRequest request)
    {
        var username = request.Method == "POST" ? request.Form["us"] : request.Query["us"];
        var passhash = request.Method == "POST" ? request.Form["pass"] : request.Query["ha"];

        if (string.IsNullOrEmpty(username) && request.Method == "GET" || string.IsNullOrEmpty(passhash))
        {
            return false;
        }

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var user = await database.GetUser(username: username, token: passhash);

        if (user == null)
        {
            return false;
        }

        if (user.Passhash != passhash)
        {
            return false;
        }

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        var player = sessions.GetSessionBy(user.Id);

        if (player == null)
        {
            return false;
        }

        return true;
    }
}