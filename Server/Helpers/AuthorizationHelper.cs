using Sunrise.Server.Data;
using Sunrise.Server.Repositories;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Helpers;

public static class AuthorizationHelper
{
    public static async Task<bool> IsAuthorized(HttpRequest request)
    {
        if (request.Method == "POST")
        {
            throw new Exception("Invalid request: POST method is not allowed for this endpoint.");
        }

        var username = !string.IsNullOrEmpty(request.Query["us"]) ? request.Query["us"] : request.Query["u"];
        var passhash = !string.IsNullOrEmpty(request.Query["ha"]) ? request.Query["ha"] : request.Query["h"];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passhash))
        {
            return false;
        }

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var user = await database.GetUser(username: username);

        if (user == null)
        {
            return false;
        }

        if (user.Passhash != passhash || user.IsRestricted)
        {
            return false;
        }

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        var player = sessions.GetSession(user.Id);

        if (player == null)
        {
            return false;
        }

        return true;
    }
}