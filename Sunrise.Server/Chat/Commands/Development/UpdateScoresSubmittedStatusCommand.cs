using Hangfire;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Extensions;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("updatescoressubmittedstatus", requiredPrivileges: UserPrivileges.Developer)]
public class UpdateScoresSubmittedStatusCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session,
            "Updating beatmap status on scores is started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => UpdateScoresSubmittedStatus(session.User.Id));

        return Task.CompletedTask;
    }

    public async Task UpdateScoresSubmittedStatus(int userId)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            userSession.SendBanchoMaintenance();
        }

        var startTime = DateTime.UtcNow;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var allScores = await database.ScoreService.GetAllScores();
        var groupedScores = allScores.GroupBy(x => x.BeatmapId);

        var scoresReviewedTotal = 0;

        foreach (var group in groupedScores)
        {

            scoresReviewedTotal += group.Count();

            var isNeedsUpdate = group.Any(s => s.SubmissionStatus == SubmissionStatus.Unknown);
            if (!isNeedsUpdate) continue;

            var usersScores = group.ToList().GroupScoresByUserId();

            foreach (var userScores in usersScores)
            {
                var scoresByMods = userScores.ToList().GroupBy(x => x.Mods);

                foreach (var scores in scoresByMods)
                {
                    var bestScore = scores.Where(x => x.IsPassed).MaxBy(x => x.TotalScore);

                    foreach (var score in scores)
                    {
                        score.SubmissionStatus = score == bestScore ? SubmissionStatus.Best : score.IsPassed ? SubmissionStatus.Submitted : SubmissionStatus.Failed;
                        await database.ScoreService.UpdateScore(score);
                    }
                }
            }

            CommandRepository.TrySendMessage(userId, $"Updated {group.Count()} submitted statuses for scores {group.Key}");
            CommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal}");
        }

        CommandRepository.TrySendMessage(userId,
            $"Updating submitted statuses on scores is finished. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        Configuration.OnMaintenance = false;

        CommandRepository.TrySendMessage(userId, "Recalculation finished. Server is back online.");
    }
}