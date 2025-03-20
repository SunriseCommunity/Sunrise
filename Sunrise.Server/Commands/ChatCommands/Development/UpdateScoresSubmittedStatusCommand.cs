using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("updatescoressubmittedstatus", requiredPrivileges: UserPrivilege.Developer)]
public class UpdateScoresSubmittedStatusCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        BackgroundTaskService.TryStartNewBackgroundJob<UpdateScoresSubmittedStatusCommand>(
            () =>
                UpdateScoresSubmittedStatus(session.UserId, CancellationToken.None),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public async Task UpdateScoresSubmittedStatus(int userId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<UpdateScoresSubmittedStatusCommand>(
            async () =>
            {
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
                                ct.ThrowIfCancellationRequested();
                                await database.Scores.UpdateScore(score);
                            }
                        }
                    }

                    ChatCommandRepository.TrySendMessage(userId, $"Updated {group.Count()} submitted statuses for scores {group.Key}");
                    ChatCommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal}");
                }
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}