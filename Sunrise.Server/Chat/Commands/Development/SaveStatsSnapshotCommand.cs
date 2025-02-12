using Hangfire;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("savestatssnapshot", requiredPrivileges: UserPrivileges.Developer)]
public class SaveStatsSnapshotCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Save stats snapshot is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session, "Saving stats snapshot has been started. Server is in maintenance mode.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => StartSaveStatsSnapshot(session.User.Id));

        return Task.CompletedTask;
    }

    public async Task StartSaveStatsSnapshot(int userId)
    {
        await BackgroundTasks.SaveStatsSnapshot();

        CommandRepository.TrySendMessage(userId, "Saving stats snapshot has been completed. Server is no longer in maintenance mode.");
        Configuration.OnMaintenance = false;
    }
}