using Hangfire;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("deleteuser", requiredPrivileges: UserPrivileges.Developer)]
public class DeleteUserCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            CommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}deleteuser <id>; Example: {Configuration.BotPrefix}deleteuser 1");
            return Task.CompletedTask;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            CommandRepository.SendMessage(session, "Invalid user id.");
            return Task.CompletedTask;
        }

        BackgroundJob.Enqueue(() => DeleteUser(session.User.Id, userId));

        return Task.CompletedTask;
    }

    public async Task DeleteUser(int userId, int requestedUserId)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(requestedUserId);

        if (user == null)
        {
            CommandRepository.TrySendMessage(userId, $"User {requestedUserId} not found.");
            return;
        }

        var isDeleted = await database.UserService.DeleteUser(user.Id);

        if (!isDeleted)
        {
            CommandRepository.TrySendMessage(userId, $"Failed to delete user {user.Username} ({requestedUserId}). Please check console for more information.");
            return;
        }

        CommandRepository.TrySendMessage(userId, $"User {user.Username} ({requestedUserId}) has been deleted.");
    }
}