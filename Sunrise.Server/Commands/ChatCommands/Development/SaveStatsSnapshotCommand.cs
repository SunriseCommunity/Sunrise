using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("savestatssnapshot", requiredPrivileges: UserPrivilege.Developer)]
public class SaveStatsSnapshotCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        BackgroundTasks.TryStartNewBackgroundJob<SaveStatsSnapshotCommand>(
            () => StartSaveStatsSnapshot(session.UserId, CancellationToken.None),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public async Task StartSaveStatsSnapshot(int userId, CancellationToken ct)
    {
        await BackgroundTasks.ExecuteBackgroundTask<SaveStatsSnapshotCommand>(
            async () => { await BackgroundTasks.SaveStatsSnapshot(ct); },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}