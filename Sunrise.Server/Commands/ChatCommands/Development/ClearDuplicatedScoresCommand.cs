using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("clearduplicatedscores", requiredPrivileges: UserPrivilege.Developer)]
public class ClearDuplicatedScoresCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session,
            "Clearing duplicated scores is started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => ClearDuplicatedScores(session.User.Id));

        return Task.CompletedTask;
    }

    public async Task ClearDuplicatedScores(int userId)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        var startTime = DateTime.UtcNow;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var allScores = await database.ScoreService.GetAllScores();
        var groupedScores = allScores.GroupBy(x => x.ScoreHash);

        var scoresReviewedTotal = 0;
        var scoresDeletedTotal = 0;

        foreach (var group in groupedScores)
        {
            var scoresLength = group.Count();

            scoresReviewedTotal += scoresLength;

            var isNeedsCleaning = scoresLength > 1;
            if (!isNeedsCleaning) continue;

            var scores = group.OrderBy(x => x.Id).ToList();

            for (var i = 1; i < scores.Count; i++)
            {
                var score = scores[i];
                await database.ScoreService.MarkAsDeleted(score);
                scoresDeletedTotal++;
            }


            ChatCommandRepository.TrySendMessage(userId, $"Scores deleted: {scoresDeletedTotal}");
            ChatCommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal} / {allScores.Count}");
        }

        ChatCommandRepository.TrySendMessage(userId,
            $"Duplicated scores cleaning finished. {scoresDeletedTotal} scores deleted. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        Configuration.OnMaintenance = false;

        ChatCommandRepository.TrySendMessage(userId, "Server is no longer in maintenance mode.");
    }
}