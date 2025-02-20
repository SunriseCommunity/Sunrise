using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Performance;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("recalculate", requiredPrivileges: UserPrivilege.Developer)]
public class RecalculateCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        // Note: Currently unstable, because if there is not beatmap files in database, it will spam Observatory with calls.
        ChatCommandRepository.SendMessage(session, "This command is currently disabled. Please try again later.");
        return Task.CompletedTask;

        ChatCommandRepository.SendMessage(session,
            "Recalculation started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => RecalculateUserStats(session.User.Id));

        return Task.CompletedTask;
    }

    public async Task RecalculateUserStats(int userId)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        foreach (var mode in Enum.GetValues<GameMode>())
        {
            var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

            var stats = await database.UserService.Stats.GetAllUserStats(mode, LeaderboardSortType.Pp);

            if (stats.Count == 0)
            {
                ChatCommandRepository.TrySendMessage(userId, $"No stats found for mode {mode}. Skipping.");
                continue;
            }

            var startTime = DateTime.UtcNow;

            foreach (var stat in stats)
            {
                var pp = await Calculators.CalculateUserWeightedPerformance(stat.UserId, mode);
                var acc = await Calculators.CalculateUserWeightedAccuracy(stat.UserId, mode);

                stat.PerformancePoints = pp;
                stat.Accuracy = acc;

                await database.UserService.Stats.UpdateUserStats(stat);
            }

            ChatCommandRepository.TrySendMessage(userId,
                $"Recalculated stats for mode {mode}. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        }

        Configuration.OnMaintenance = false;

        ChatCommandRepository.TrySendMessage(userId, "Recalculation finished. Server is back online.");
    }
}