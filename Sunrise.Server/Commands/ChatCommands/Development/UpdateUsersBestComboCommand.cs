using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("updateusersbestcombo", requiredPrivileges: UserPrivilege.Developer)]
public class UpdateUsersBestComboCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session,
            "Updating users best combo is started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => UpdateUsersBestCombo(session.UserId));

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

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        foreach (var mode in Enum.GetValues<GameMode>())
        {

            var pageSize = 50;

            for (var page = 1;; page++)
            {
                var scores = await database.Scores.GetBestScoresByGameMode(mode, new QueryOptions(new Pagination(page, pageSize)));
                var groupedScores = scores.Where(x => x.IsScoreable).GroupBy(x => x.UserId);

                foreach (var group in groupedScores)
                {
                    var bestCombo = group.Max(x => x.MaxCombo);

                    var user = await database.Users.GetUser(group.Key);

                    if (user == null)
                    {
                        ChatCommandRepository.TrySendMessage(userId, $"User {group.Key} not found. Skipping.");
                        continue;
                    }

                    var userStats = await database.Users.Stats.GetUserStats(user.Id, mode);

                    if (userStats == null)
                    {
                        ChatCommandRepository.TrySendMessage(userId, $"User {user.Id} stats not found. Skipping.");
                        continue;
                    }

                    var previousBestCombo = userStats.MaxCombo;

                    userStats.MaxCombo = bestCombo;

                    await database.Users.Stats.UpdateUserStats(userStats, user);

                    ChatCommandRepository.TrySendMessage(userId, $"Updated {user.Username} ({user.Id}) best combo to {bestCombo} (previous: {previousBestCombo}) for mode {mode}");
                }

                ChatCommandRepository.TrySendMessage(userId,
                    $"Updated users best combo for mode {mode}. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

                if (scores.Count < pageSize) break;
            }
        }

        Configuration.OnMaintenance = false;

        ChatCommandRepository.TrySendMessage(userId, "Updating users best combo is finished. Server is back online.");
    }
}