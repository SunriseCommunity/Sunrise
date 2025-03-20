using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("savestatssnapshot", requiredPrivileges: UserPrivilege.Developer)]
public class SaveStatsSnapshotCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        BackgroundTaskService.TryStartNewBackgroundJob<SaveStatsSnapshotCommand>(
            () => StartSaveStatsSnapshot(session.UserId, CancellationToken.None),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public async Task StartSaveStatsSnapshot(int userId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<SaveStatsSnapshotCommand>(
            async () => { await RecurringJobs.SaveUsersStatsSnapshots(ct); },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}