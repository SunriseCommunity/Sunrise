using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("flushcache", requiredPrivileges: UserPrivilege.Developer)]
public class FlushCacheCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var isSoftFlush = true;

        if (args is { Length: > 0 })
        {
            switch (args[0])
            {
                case "true":
                    break;
                case "false":
                    ChatCommandRepository.SendMessage(session,
                        "WARNING: Are you SURE that you want to flush ALL the cache? It will force rebuilding all user stats leaderboards (can take some time).\n" +
                        $"Use '{Configuration.BotPrefix}flushcache yes_i_understand_what_im_doing' to proceed.");
                    return Task.CompletedTask;
                case "yes_i_understand_what_im_doing":
                    isSoftFlush = false;
                    break;
                default:
                    ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}flushcache <isFlushGeneralData?>;\nExample: {Configuration.BotPrefix}flushcache true - flash only general data, such as keys and db queries, but sorted sets will not be flushed");
                    return Task.CompletedTask;
            }
        }

        BackgroundTaskService.TryStartNewBackgroundJob<FlushCacheCommand>(
            () =>
                FlushAndUpdateRedisCache(session.UserId, isSoftFlush),
            message => ChatCommandRepository.SendMessage(session, message),
            !isSoftFlush);

        return Task.CompletedTask;
    }

    public async Task FlushAndUpdateRedisCache(int userId, bool isSoftFlush)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<FlushCacheCommand>(
            async () =>
            {
                using var scope = ServicesProviderHolder.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                await database.FlushAndUpdateRedisCache(isSoftFlush);
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}