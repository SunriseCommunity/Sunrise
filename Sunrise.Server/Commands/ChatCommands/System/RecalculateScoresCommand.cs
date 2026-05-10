using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.System;

[ChatCommand("recalculatescores", requiredPrivileges: UserPrivilege.SuperUser)]
public class RecalculateScoresCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 3)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}recalculatescores <modeEnum | all> <startFromId> <isStartMaintenance>; Example: {Configuration.BotPrefix}recalculatescores 0 10 true for osu std starting from score 10 with maintenance mode on.");
            return Task.CompletedTask;
        }

        GameMode? mode = Enum.TryParse(args[0], out GameMode parsedMode) ? parsedMode : null;

        if (mode == null && args[0] != "all")
        {
            ChatCommandRepository.SendMessage(session, "Invalid mode.");
            return Task.CompletedTask;
        }

        if (!int.TryParse(args[1], out var startFromId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid startFromId.");
            return Task.CompletedTask;
        }

        if (!bool.TryParse(args[2], out var isStartMaintenance))
        {
            ChatCommandRepository.SendMessage(session, "Invalid isStartMaintenance value.");
            return Task.CompletedTask;
        }

        BackgroundTaskService.TryStartNewBackgroundJob<RecalculateScoresCommand>(
            () => RecalculateScores(session.UserId, CancellationToken.None, startFromId, mode),
            message => ChatCommandRepository.TrySendMessage(session.UserId, message),
            isStartMaintenance);

        return Task.CompletedTask;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task RecalculateScores(int userId, CancellationToken ct, int startFromId, GameMode? mode = null)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<RecalculateScoresCommand>(
            async () =>
            {

                var pageSize = 100;
                var scoresReviewedTotal = 0;
                var scoresSkippedTotal = 0;

                for (var x = 1;; x++)
                {
                    using var scope = ServicesProviderHolder.CreateScope();
                    var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                    var (pageScores, _) = await database.Scores.GetScores(mode,
                        new QueryOptions(new Pagination(x, pageSize))
                        {
                            IgnoreCountQueryIfExists = true
                        },
                        startFromId);

                    foreach (var score in pageScores)
                    {
                        var queued = await database.ScoreTaskQueue.TryAddQueueEntry(new ScoreTaskQueue
                        {
                            TaskType = ScoreTaskType.Recalculation,
                            ScoreId = score.Id,
                            Priority = (int)ScoreProcessingPriority.Low,
                            CreatedAt = DateTime.UtcNow
                        }, ct);

                        ct.ThrowIfCancellationRequested();
                        scoresReviewedTotal++;

                        if (!queued)
                        {
                            scoresSkippedTotal++;
                            continue;
                        }

                        if (scoresReviewedTotal % 100 == 0)
                            ChatCommandRepository.TrySendMessage(userId, $"Scores reviewed: {scoresReviewedTotal}. Queued: {scoresReviewedTotal - scoresSkippedTotal}. Skipped active: {scoresSkippedTotal}");
                    }

                    if (pageScores.Count < pageSize) break;
                }

                ChatCommandRepository.TrySendMessage(userId, $"Recalculation finished. Reviewed: {scoresReviewedTotal}. Queued: {scoresReviewedTotal - scoresSkippedTotal}. Skipped active: {scoresSkippedTotal}");
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}