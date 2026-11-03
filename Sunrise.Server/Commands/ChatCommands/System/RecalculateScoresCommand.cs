using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
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
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}recalculatescores <modeEnum | all> <startFromId> [isStartMaintenance]; Example: {Configuration.BotPrefix}recalculatescores 0 10 for osu std starting from score 10. The command only queues recalculations, so maintenance mode is optional.");
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

        var isStartMaintenance = false;

        if (args.Length >= 3 && !bool.TryParse(args[2], out isStartMaintenance))
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

                const int pageSize = 500;
                var scoresReviewedTotal = 0;
                var scoresSkippedTotal = 0;

                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    using var scope = ServicesProviderHolder.CreateScope();
                    var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                    var scoreIds = await database.Scores.GetScoreIdsForBulkProcessing(mode: mode,
                        startFromId: startFromId, limit: pageSize, filterValidScores: true, ct: ct);
                    if (scoreIds.Count == 0) break;

                    var queued = await database.ScoreProcessingTasks.EnqueueScoreTasks(scoreIds, ScoreTaskType.Recalculation, ScoreProcessingPriority.Low, ct);
                    scoresReviewedTotal += scoreIds.Count;
                    scoresSkippedTotal += scoreIds.Count - queued;
                    ChatCommandRepository.TrySendMessage(userId, $"Scores reviewed: {scoresReviewedTotal}. Queued: {scoresReviewedTotal - scoresSkippedTotal}. Skipped active: {scoresSkippedTotal}");

                    if (scoreIds.Count < pageSize || scoreIds[^1] == int.MaxValue) break;
                    startFromId = scoreIds[^1] + 1;
                }

                ChatCommandRepository.TrySendMessage(userId, $"Finished queueing recalculations. Reviewed: {scoresReviewedTotal}. Queued: {scoresReviewedTotal - scoresSkippedTotal}. Skipped active: {scoresSkippedTotal}");
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}
