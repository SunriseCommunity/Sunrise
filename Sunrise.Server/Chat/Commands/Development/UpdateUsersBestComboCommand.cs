using Hangfire;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("updateusersbestcombo", requiredPrivileges: UserPrivileges.Developer)]
public class UpdateUsersBestComboCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session,
            "Updating users best combo is started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => UpdateUsersBestCombo(session.User.Id));

        return Task.CompletedTask;
    }

    public async Task UpdateUsersBestCombo(int userId)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        var startTime = DateTime.UtcNow;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        foreach (var mode in Enum.GetValues<GameMode>())
        {
            var allScores = await database.ScoreService.GetBestScoresByGameMode(mode);
            var groupedScores = allScores.Where(x => x.IsScoreable).GroupBy(x => x.UserId);

            foreach (var group in groupedScores)
            {
                var bestCombo = group.Max(x => x.MaxCombo);

                var user = await database.UserService.GetUser(group.Key);

                if (user == null)
                {
                    CommandRepository.TrySendMessage(userId, $"User {group.Key} not found. Skipping.");
                    continue;
                }

                var userStats = await database.UserService.Stats.GetUserStats(user.Id, mode);

                if (userStats == null)
                {
                    CommandRepository.TrySendMessage(userId, $"User {user.Id} stats not found. Skipping.");
                    continue;
                }

                var previousBestCombo = userStats.MaxCombo;

                userStats.MaxCombo = bestCombo;

                await database.UserService.Stats.UpdateUserStats(userStats);

                CommandRepository.TrySendMessage(userId, $"Updated {user.Username} ({user.Id}) best combo to {bestCombo} (previous: {previousBestCombo}) for mode {mode}");
            }

            CommandRepository.TrySendMessage(userId,
                $"Updated users best combo for mode {mode}. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");
        }

        Configuration.OnMaintenance = false;

        CommandRepository.TrySendMessage(userId, "Updating users best combo is finished. Server is back online.");
    }
}