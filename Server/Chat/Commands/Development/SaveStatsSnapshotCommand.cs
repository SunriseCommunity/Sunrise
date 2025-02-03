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

        Task.Run(() => StartSaveStatsSnapshot(session));

        return Task.CompletedTask;
    }

    private async Task StartSaveStatsSnapshot(Session session)
    {
        await BackgroundTasks.SaveStatsSnapshot();

        CommandRepository.SendMessage(session, "Saving stats snapshot has been completed. Server is no longer in maintenance mode.");
        Configuration.OnMaintenance = false;
    }
}