using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.System;

[ChatCommand("requeuefailedscores", requiredPrivileges: UserPrivilege.SuperUser)]
public class RequeueFailedScoresCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        int? taskId = null;

        if (args != null && args.Length >= 1)
        {
            if (!int.TryParse(args[0], out var parsedTaskId))
            {
                ChatCommandRepository.SendMessage(session,
                    $"Usage: {Configuration.BotPrefix}requeuefailedscores [taskId] — omit filter to requeue all failed tasks.");
                return;
            }

            taskId = parsedTaskId;
        }

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var requeuedCount = taskId.HasValue
            ? await database.ScoreTaskQueue.TryRequeueFailedTask(taskId.Value) ? 1 : 0
            : await database.ScoreTaskQueue.TryRequeueFailedTasks();

        ChatCommandRepository.SendMessage(session, $"Requeued {requeuedCount} failed score-processing {(requeuedCount == 1 ? "task" : "tasks")}.");
    }
}