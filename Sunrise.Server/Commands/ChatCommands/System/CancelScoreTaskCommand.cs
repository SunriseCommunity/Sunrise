using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.System;

[ChatCommand("cancelscoretask", requiredPrivileges: UserPrivilege.SuperUser)]
public class CancelScoreTaskCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1 || !int.TryParse(args[0], out var taskId))
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}cancelscoretask <entryId>");
            return;
        }

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var task = await database.ScoreProcessingTasks.GetTaskById(taskId);

        if (task == null)
        {
            ChatCommandRepository.SendMessage(session, $"Score task {taskId} does not exist.");
            return;
        }

        var cancelResult = await database.ScoreProcessingTasks.CancelTask(taskId);

        if (cancelResult.IsFailure)
        {
            ChatCommandRepository.SendMessage(session, cancelResult.Error);
            return;
        }

        await database.Events.ScoreProcessing.AddCancelledEvent(session.UserId, taskId, task?.ScoreId);

        ChatCommandRepository.SendMessage(session, $"Score task {taskId} was cancelled.");
    }
}