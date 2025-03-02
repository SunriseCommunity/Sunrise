using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Extensions.Users;

public static class UserExtensions
{
    public static string GetUserInGameChatString(this User user)
    {
        return $"[https://{Configuration.Domain}/user/{user.Id} {user.Username}]";
    }

}