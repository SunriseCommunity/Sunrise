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
        BackgroundTasks.TryStartNewBackgroundJob<BackupDatabaseCommand>(
            () => StartDatabaseBackup(session.UserId, CancellationToken.None),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public Task StartDatabaseBackup(int userId, CancellationToken ct)
    {
        _ = BackgroundTasks.ExecuteBackgroundTask<BackupDatabaseCommand>(() =>
            {
                BackgroundTasks.BackupDatabase(ct);
                return Task.CompletedTask;
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));

        return Task.CompletedTask;
    }
}