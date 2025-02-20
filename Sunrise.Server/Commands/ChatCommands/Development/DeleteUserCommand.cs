using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("deleteuser", requiredPrivileges: UserPrivilege.Developer)]
public class DeleteUserCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}deleteuser <id>; Example: {Configuration.BotPrefix}deleteuser 1");
            return Task.CompletedTask;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid user id.");
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
            ChatCommandRepository.TrySendMessage(userId, $"User {requestedUserId} not found.");
            return;
        }

        var isDeleted = await database.UserService.DeleteUser(user.Id);

        if (!isDeleted)
        {
            ChatCommandRepository.TrySendMessage(userId, $"Failed to delete user {user.Username} ({requestedUserId}). Please check console for more information.");
            return;
        }

        ChatCommandRepository.TrySendMessage(userId, $"User {user.Username} ({requestedUserId}) has been deleted.");
    }
}