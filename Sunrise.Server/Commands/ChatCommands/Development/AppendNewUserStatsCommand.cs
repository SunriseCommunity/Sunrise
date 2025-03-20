using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("appendnewuserstats", requiredPrivileges: UserPrivilege.Developer)]
public class AppendNewUserStatsCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        BackgroundTaskService.TryStartNewBackgroundJob<AppendNewUserStatsCommand>(
            () => AppendMissingUserStats(session.UserId, CancellationToken.None),
            message => ChatCommandRepository.SendMessage(session, message),
            true);

        return Task.CompletedTask;
    }

    public async Task AppendMissingUserStats(int userId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<AppendNewUserStatsCommand>(
            async () =>
            {
                using var scope = ServicesProviderHolder.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                var users = await database.Users.GetUsers();

                foreach (var mode in Enum.GetValues<GameMode>())
                {
                    var startTime = DateTime.UtcNow;

                    foreach (var user in users)
                    {
                        ct.ThrowIfCancellationRequested();
                        await database.Users.Stats.GetUserStats(user.Id, mode);
                    }

                    ChatCommandRepository.TrySendMessage(userId, $"Rechecking {mode} mode is finished. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");
                }
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}