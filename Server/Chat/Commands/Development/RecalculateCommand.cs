using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("recalculate", requiredPrivileges: UserPrivileges.Developer)]
public class RecalculateCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session,
            "Recalculation started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        _ = RecalculateUserStats(session);
        return Task.CompletedTask;
    }

    private async Task RecalculateUserStats(Session session)
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
                CommandRepository.SendMessage(session, $"No stats found for mode {mode}. Skipping.");
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

            CommandRepository.SendMessage(session,
                $"Recalculated stats for mode {mode}. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        }

        Configuration.OnMaintenance = false;

        CommandRepository.SendMessage(session, "Recalculation finished. Server is back online.");
    }
}