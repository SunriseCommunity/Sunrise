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

        var cancelResult = await database.ScoreTaskQueue.CancelTask(taskId);
        if (cancelResult.IsFailure)
        {
            ChatCommandRepository.SendMessage(session, cancelResult.Error);
            return;
        }

        ChatCommandRepository.SendMessage(session, $"Score task {taskId} was cancelled.");
    }
}
