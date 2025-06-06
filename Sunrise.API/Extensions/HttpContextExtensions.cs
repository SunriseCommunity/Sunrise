using Microsoft.AspNetCore.Http;
using Sunrise.API.Services;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.API.Extensions;

public static class HttpContextExtensions
{
    public static BaseSession GetCurrentSession(this HttpContext context)
    {
        var user = GetCurrentUser(context);
        if (user == null || user.IsUserSunriseBot())
            return AuthService.GenerateIpSession(context.Request);

        var session = new BaseSession(user);
        return session;
    }

    public static User? GetCurrentUser(this HttpContext context)
    {
        var user = context.Items["CurrentUser"];

        if (user == null)
            return null;

        return user as User;
    }
}