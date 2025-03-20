using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("disableinactiveusers", requiredPrivileges: UserPrivilege.Developer)]
public class DisableInactiveUsersCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        BackgroundTaskService.TryStartNewBackgroundJob<DisableInactiveUsersCommand>(
            () => DisableInactiveUsers(session.UserId, CancellationToken.None),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public async Task DisableInactiveUsers(int userId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<DisableInactiveUsersCommand>(
            async () => { await RecurringJobs.DisableInactiveUsers(ct); },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}