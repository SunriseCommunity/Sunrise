using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("backupdatabase", requiredPrivileges: UserPrivilege.Developer)]
public class BackupDatabaseCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (Configuration.OnMaintenance)
        {
            ChatCommandRepository.SendMessage(session, "Server is in maintenance mode. Starting database backup is not possible.");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session, "Database backup has been started. Server is in maintenance mode.");

        Configuration.OnMaintenance = true;

        BackgroundJob.Enqueue(() => StartDatabaseBackup(session.UserId));

        return Task.CompletedTask;
    }

    public void StartDatabaseBackup(int userId)
    {
        BackgroundTasks.BackupDatabase();

        ChatCommandRepository.TrySendMessage(userId, "Database backup has been completed. Server is no longer in maintenance mode.");
        Configuration.OnMaintenance = false;
    }
}