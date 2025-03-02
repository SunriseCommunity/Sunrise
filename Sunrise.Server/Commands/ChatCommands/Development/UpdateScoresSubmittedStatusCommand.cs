using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("updatescoressubmittedstatus", requiredPrivileges: UserPrivilege.Developer)]
public class UpdateScoresSubmittedStatusCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Starting recalculation is not possible.");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session,
            "Updating beatmap status on scores is started. Server will enter maintenance mode until it's done.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => UpdateScoresSubmittedStatus(session.UserId));

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

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var allScores = await database.Scores.GetScores();
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
                        await database.Scores.UpdateScore(score);
                    }
                }
            }

            ChatCommandRepository.TrySendMessage(userId, $"Updated {group.Count()} submitted statuses for scores {group.Key}");
            ChatCommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal}");
        }

        ChatCommandRepository.TrySendMessage(userId,
            $"Updating submitted statuses on scores is finished. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");

        Configuration.OnMaintenance = false;

        ChatCommandRepository.TrySendMessage(userId, "Recalculation finished. Server is back online.");
    }
}