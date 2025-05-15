using Microsoft.EntityFrameworkCore;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

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

                var pageSize = 10;
                var scoresReviewedTotal = 0;
                
                for (var x = 1;; x++)
                {
                    var beatmapIds = await database.DbContext.Scores
                        .FilterValidScores()
                        .FilterPassedScoreableScores()
                        .Select(s => new
                        {
                            s.BeatmapId
                        })
                        .Distinct()
                        .OrderBy(x => x.BeatmapId)
                        .UseQueryOptions(new QueryOptions(new Pagination(x, pageSize)))
                        .ToListAsync(cancellationToken: ct);
                    
                    foreach (var beatmap in beatmapIds)
                    {
                        var scores = await database.DbContext.Scores
                            .FilterValidScores()
                            .FilterPassedScoreableScores()
                            .Where(s => s.BeatmapId == beatmap.BeatmapId)
                            .ToListAsync(cancellationToken: ct);
                        
                        var scoresGrouped = scores.GroupBy(s => new
                        {
                            s.BeatmapId,
                            s.GameMode,
                            s.Mods,
                            s.UserId,
                        });
                        
                        foreach (var group in scoresGrouped)
                        {
                            var scoresGroup = group.ToList();
                    
                            scoresReviewedTotal += group.Count();
                            
                            await UpdateUserBeatmapScoresSubmittedStatus(userId, database, scoresGroup, ct);
                        }
                    }
                    
                    ChatCommandRepository.TrySendMessage(userId, $"Total scores reviewed: {scoresReviewedTotal}");
                    if (beatmapIds.Count < pageSize) break;
                }
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
    
    public async Task UpdateUserBeatmapScoresSubmittedStatus(int sendProgressMessageToUserId, DatabaseService database, List<Score> scores, CancellationToken ct)
    {
        var bestScore = scores.Select(x => x).ToList().SortScoresByTheirScoreValue().FirstOrDefault();

        foreach (var score in scores)
        {
            var oldScoreStatus = score.SubmissionStatus;

            score.SubmissionStatus = score == bestScore ? SubmissionStatus.Best : score.IsPassed ? SubmissionStatus.Submitted : SubmissionStatus.Failed;
            ct.ThrowIfCancellationRequested();

            if (oldScoreStatus != score.SubmissionStatus)
            {
                await database.Scores.UpdateScore(score);
                ChatCommandRepository.TrySendMessage(sendProgressMessageToUserId, $"Updated score {score.Id} submission status from {oldScoreStatus} to {score.SubmissionStatus}");
            }
        }
    }
}