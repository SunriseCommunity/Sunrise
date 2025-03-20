using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("backupdatabase", requiredPrivileges: UserPrivilege.Developer)]
public class BackupDatabaseCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        BackgroundTaskService.TryStartNewBackgroundJob<BackupDatabaseCommand>(
            () => StartDatabaseBackup(session.UserId, CancellationToken.None),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public async Task StartDatabaseBackup(int userId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<BackupDatabaseCommand>(() => RecurringJobs.BackupDatabase(ct),
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}