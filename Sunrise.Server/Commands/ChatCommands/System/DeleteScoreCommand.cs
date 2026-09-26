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

[ChatCommand("deletescore", requiredPrivileges: UserPrivilege.SuperUser)]
public class DeleteScoreCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1 || !int.TryParse(args[0], out var scoreId))
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}deletescore <scoreId>");
            return Task.CompletedTask;
        }

        BackgroundTaskService.TryStartNewBackgroundJob<DeleteScoreCommand>(
            () => DeleteScore(session.UserId, scoreId, CancellationToken.None),
            message => ChatCommandRepository.TrySendMessage(session.UserId, message));

        return Task.CompletedTask;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task DeleteScore(int userId, int scoreId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<DeleteScoreCommand>(
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

                var task = new ScoreProcessingTask
                {
                    TaskType = ScoreTaskType.Delete,
                    ScoreId = score.Id,
                    Priority = (int)ScoreProcessingPriority.Normal,
                    CreatedAt = DateTime.UtcNow
                };

                var queued = await database.ScoreProcessingTasks.TryAddQueueEntry(task, ct);

                if (!queued)
                {
                    ChatCommandRepository.TrySendMessage(userId, $"Score {scoreId} already has an active queued task.");
                    return;
                }

                await database.Events.ScoreProcessing.AddActionRequestedEvent(userId, score.Id, task.Id, ScoreTaskType.Delete, task.Priority, ct);

                ChatCommandRepository.TrySendMessage(userId, $"Score {scoreId} was queued for deletion.");
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}