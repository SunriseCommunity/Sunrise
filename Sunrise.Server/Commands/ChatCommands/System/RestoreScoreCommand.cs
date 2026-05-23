using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.System;

[ChatCommand("restorescore", requiredPrivileges: UserPrivilege.SuperUser)]
public class RestoreScoreCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1 || !int.TryParse(args[0], out var scoreId))
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}restorescore <scoreId>");
            return Task.CompletedTask;
        }

        BackgroundTaskService.TryStartNewBackgroundJob<RestoreScoreCommand>(
            () => RestoreScore(session.UserId, scoreId, CancellationToken.None),
            message => ChatCommandRepository.TrySendMessage(session.UserId, message));

        return Task.CompletedTask;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task RestoreScore(int userId, int scoreId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<RestoreScoreCommand>(
            async () =>
            {
                using var scope = ServicesProviderHolder.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                var score = await database.Scores.GetScore(scoreId, filterValidScores: false, ct: ct);

                if (score == null)
                {
                    ChatCommandRepository.TrySendMessage(userId, $"Score {scoreId} was not found.");
                    return;
                }

                var queued = await database.ScoreTaskQueue.TryAddQueueEntry(new ScoreTaskQueue
                    {
                        TaskType = ScoreTaskType.Restore,
                        ScoreId = score.Id,
                        Priority = (int)ScoreProcessingPriority.Normal,
                        CreatedAt = DateTime.UtcNow
                    },
                    ct);

                if (!queued)
                {
                    ChatCommandRepository.TrySendMessage(userId, $"Score {scoreId} already has an active queued task.");
                    return;
                }

                ChatCommandRepository.TrySendMessage(userId, $"Score {scoreId} was queued for restore.");
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}