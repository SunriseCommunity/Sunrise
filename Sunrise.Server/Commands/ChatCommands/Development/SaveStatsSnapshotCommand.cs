using Hangfire;
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
        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Save stats snapshot is not possible.");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session, "Saving stats snapshot has been started. Server is in maintenance mode.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => StartSaveStatsSnapshot(session.UserId));

        return Task.CompletedTask;
    }

    public async Task StartSaveStatsSnapshot(int userId)
    {
        await BackgroundTasks.SaveStatsSnapshot();

        ChatCommandRepository.TrySendMessage(userId, "Saving stats snapshot has been completed. Server is no longer in maintenance mode.");
        Configuration.OnMaintenance = false;
    }
}