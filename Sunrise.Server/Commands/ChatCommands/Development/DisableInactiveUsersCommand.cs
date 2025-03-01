using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("disableinactiveusers", requiredPrivileges: UserPrivilege.Developer)]
public class DisableInactiveUsersCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Starting checking for inactive users is not possible.");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session, "Checking for inactive users has been started. Server is in maintenance mode.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => DisableInactiveUsers(session.UserId));

        return Task.CompletedTask;
    }

    public async Task DisableInactiveUsers(int userId)
    {
        await BackgroundTasks.DisableInactiveUsers();

        ChatCommandRepository.TrySendMessage(userId, "Checking for inactive users has been completed. Server is no longer in maintenance mode.");
        Configuration.OnMaintenance = false;
    }
}