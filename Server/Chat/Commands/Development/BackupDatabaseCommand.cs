using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("backupdatabase", requiredPrivileges: UserPrivileges.Developer)]
public class BackupDatabaseCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            CommandRepository.SendMessage(session, "Server is in maintenance mode. Starting database backup is not possible.");
            return Task.CompletedTask;
        }

        CommandRepository.SendMessage(session, "Database backup has been started. Server is in maintenance mode.");

        Configuration.OnMaintenance = true;

        _ = StartDatabaseBackup(session);

        return Task.CompletedTask;
    }

    private async Task StartDatabaseBackup(Session session)
    {
        await Task.Run(BackgroundTasks.BackupDatabase);

        CommandRepository.SendMessage(session, "Database backup has been completed. Server is no longer in maintenance mode.");
        Configuration.OnMaintenance = false;
    }
}