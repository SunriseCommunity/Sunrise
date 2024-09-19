using osu.Shared;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("recalculate", requiredPrivileges: UserPrivileges.Developer)]
public class RecalculateCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        CommandRepository.SendMessage(session, "Recalculation started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        foreach (var mode in Enum.GetValues<GameMode>())
        {
            var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

            var stats = await database.GetAllUserStats(mode);

            if (stats == null)
            {
                CommandRepository.SendMessage(session, $"No stats found for mode {mode}. Skipping.");
                continue;
            }

            var startTime = DateTime.UtcNow;

            foreach (var stat in stats)
            {
                var pp = await Calculators.CalculateUserWeightedPerformance(stat.UserId, mode);
                var acc = await Calculators.CalculateUserWeightedAccuracy(stat.UserId, mode);

                stat.PerformancePoints = (short)pp;
                stat.Accuracy = acc;

                await database.UpdateUserStats(stat);
            }

            CommandRepository.SendMessage(session, $"Recalculated stats for mode {mode}. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");
        }

        Configuration.OnMaintenance = false;

        CommandRepository.SendMessage(session, "Recalculation finished. Server is back online.");
    }
}