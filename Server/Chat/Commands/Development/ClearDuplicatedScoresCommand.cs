using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("clearduplicatedscores", requiredPrivileges: UserPrivileges.Developer)]
public class ClearDuplicatedScoresCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session,
            "Clearing duplicated scores is started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        _ = ClearDuplicatedScores(session);
        return Task.CompletedTask;
    }

    private async Task ClearDuplicatedScores(Session session)
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


            CommandRepository.SendMessage(session, $"Scores deleted: {scoresDeletedTotal}");
            CommandRepository.SendMessage(session, $"Total scores reviewed: {scoresReviewedTotal} / {allScores.Count}");
        }

        CommandRepository.SendMessage(session,
            $"Duplicated scores cleaning finished. {scoresDeletedTotal} scores deleted. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        Configuration.OnMaintenance = false;

        CommandRepository.SendMessage(session, "Server is no longer in maintenance mode.");
    }
}