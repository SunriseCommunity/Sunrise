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

[ChatCommand("recalculatescore", requiredPrivileges: UserPrivilege.SuperUser)]
public class RecalculateScoreCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1 || !int.TryParse(args[0], out var scoreId))
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}reprocessscore <scoreId>");
            return Task.CompletedTask;
        }

        BackgroundTaskService.TryStartNewBackgroundJob<RecalculateScoreCommand>(
            () => ReprocessScore(session.UserId, scoreId, CancellationToken.None),
            message => ChatCommandRepository.TrySendMessage(session.UserId, message));

        return Task.CompletedTask;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ReprocessScore(int userId, int scoreId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<RecalculateScoreCommand>(
            async () =>
            {
                using var scope = ServicesProviderHolder.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                var score = await database.Scores.GetUnvalidatedScore(scoreId, ct: ct);

                if (score == null)
                {
                    ChatCommandRepository.TrySendMessage(userId, $"Score {scoreId} was not found.");
                    return;
                }

                var queued = await database.ScoreTaskQueue.TryAddQueueEntry(new ScoreTaskQueue
                    {
                        TaskType = ScoreTaskType.Recalculation,
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

                ChatCommandRepository.TrySendMessage(userId, $"Score {scoreId} was queued for recalculation.");
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}