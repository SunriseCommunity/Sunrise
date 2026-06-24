using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Services;
using BeatmapStatus = Sunrise.Shared.Enums.Beatmaps.BeatmapStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Shared.Jobs;

public class BulkScoreProcessingJob(IServiceScopeFactory scopeFactory)
{
    private const int PageSize = 100;

    public async Task EnqueueByFilter(
        int? executorId,
        ScoreTaskType action,
        int userId,
        GameMode? mode,
        Mods? mods,
        SubmissionStatus? submissionStatus,
        BeatmapStatus? beatmapStatus,
        DateTime? submittedFrom,
        DateTime? submittedTo,
        CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<BulkScoreProcessingJob>(async () =>
        {
            var matched = 0;
            var queued = 0;
            var skipped = 0;
            int? lastScoreId = null;

            while (true)
            {
                using var scope = scopeFactory.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                var pageScores = await database.Scores.GetScoresForBulkProcessing(
                    mode,
                    userId,
                    mods,
                    submissionStatus,
                    beatmapStatus,
                    submittedFrom,
                    submittedTo,
                    lastScoreId != null ? lastScoreId + 1 : null,
                    PageSize,
                    ct);

                if (pageScores.Count == 0)
                    break;

                var scoreIds = pageScores.Select(score => score.Id).ToList();
                var queuedTasks = await database.ScoreProcessingTasks.BulkAddScoreTasks(scoreIds, action, ScoreProcessingPriority.Low, ct);
                lastScoreId = scoreIds[^1];

                matched += pageScores.Count;
                queued += queuedTasks.Count;
                skipped += pageScores.Count - queuedTasks.Count;

                ct.ThrowIfCancellationRequested();

                if (pageScores.Count < PageSize)
                    break;
            }

            using var summaryScope = scopeFactory.CreateScope();
            var summaryDatabase = summaryScope.ServiceProvider.GetRequiredService<DatabaseService>();

            await summaryDatabase.Events.ScoreProcessing.AddBulkRequestedEvent(
                executorId,
                action,
                new
                {
                    UserId = userId,
                    Mode = mode,
                    Mods = mods,
                    SubmissionStatus = submissionStatus,
                    BeatmapStatus = beatmapStatus,
                    From = submittedFrom,
                    To = submittedTo
                },
                matched,
                queued,
                skipped,
                ct);
        });
    }
}