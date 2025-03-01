using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("appendnewuserstats", requiredPrivileges: UserPrivilege.Developer)]
public class AppendNewUserStatsCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Starting new user stats appending is not possible.");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session,
            "Appending new user stats if needed has been started. Server is in maintenance mode.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => AppendMissingUserStats(session.UserId));

        return Task.CompletedTask;
    }

    public async Task AppendMissingUserStats(int userId)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var users = await database.Users.GetUsers(); // TODO: Optimise

        foreach (var mode in Enum.GetValues<GameMode>())
        {
            var startTime = DateTime.UtcNow;
            foreach (var user in users)
            {
                await database.Users.Stats.GetUserStats(user.Id, mode);
            }

            ChatCommandRepository.TrySendMessage(userId, $"Rechecking {mode} mode is finished. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");
        }

        Configuration.OnMaintenance = false;

        ChatCommandRepository.TrySendMessage(userId, "Appending new user stats if needed has been finished. Server is back online.");
    }
}