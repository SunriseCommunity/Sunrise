using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("disableinactiveusers", requiredPrivileges: UserPrivileges.Developer)]
public class DisableInactiveUsersCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Starting checking for inactive users is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session, "Checking for inactive users has been started. Server is in maintenance mode.");

        Configuration.OnMaintenance = true;

        Task.Run(() => DisableInactiveUsers(session));

        return Task.CompletedTask;
    }

    private async Task DisableInactiveUsers(Session session)
    {
        await Task.Run(BackgroundTasks.DisableInactiveUsers);

        CommandRepository.SendMessage(session, "Checking for inactive users has been completed. Server is no longer in maintenance mode.");
        Configuration.OnMaintenance = false;
    }
}