using Hangfire;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("appendnewuserstats", requiredPrivileges: UserPrivileges.Developer)]
public class AppendNewUserStatsCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Starting new user stats appending is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session,
            "Appending new user stats if needed has been started. Server is in maintenance mode.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => AppendMissingUserStats(session.User.Id));

        return Task.CompletedTask;
    }

    public async Task AppendMissingUserStats(int userId)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        var startTime = DateTime.UtcNow;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var users = await database.UserService.GetAllUsers();

        foreach (var mode in Enum.GetValues<GameMode>())
        {
            foreach (var user in users)
            {
                var stats = await database.UserService.Stats.GetUserStats(user.Id, mode);

                if (stats != null)
                {
                    continue;
                }

                var newStats = new UserStats
                {
                    UserId = user.Id,
                    GameMode = mode
                };

                await database.UserService.Stats.InsertUserStats(newStats);

                CommandRepository.TrySendMessage(userId,
                    $"User {user.Id} stats for mode {mode} has been created.");
            }

            CommandRepository.TrySendMessage(userId, $"Rechecking {mode} mode is finished. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");
        }

        Configuration.OnMaintenance = false;

        CommandRepository.TrySendMessage(userId, "Appending new user stats if needed has been finished. Server is back online.");
    }
}