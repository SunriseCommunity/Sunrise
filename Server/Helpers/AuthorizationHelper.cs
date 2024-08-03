using Sunrise.Server.Utils;

namespace Sunrise.Server.Helpers;

public class AuthorizationHelper(ServicesProvider services)
{
    public async Task<bool> IsAuthorized(HttpRequest request)
    {
        var username = request.Method == "POST" ? request.Form["us"] : request.Query["us"];
        var passhash = request.Method == "POST" ? request.Form["pass"] : request.Query["ha"];

        if (string.IsNullOrEmpty(username) && request.Method == "GET" || string.IsNullOrEmpty(passhash))
        {
            return false;
        }

        var user = await services.Database.GetUser(username: username, token: passhash);

        if (user == null)
        {
            return false;
        }

        if (user.Passhash != passhash)
        {
            return false;
        }

        var player = services.Sessions.GetSessionByUserId(user.Id);

        if (player == null)
        {
            return false;
        }

        return true;
    }
}