using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

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

        BackgroundJob.Enqueue(() => DeleteUser(session.UserId, userId));

        return Task.CompletedTask;
    }

    public async Task DeleteUser(int userId, int requestedUserId)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = await database.Users.GetUser(requestedUserId);

        if (user == null)
        {
            ChatCommandRepository.TrySendMessage(userId, $"User {requestedUserId} not found.");
            return;
        }

        var deletedUserResult = await database.Users.DeleteUser(user.Id);

        if (deletedUserResult.IsFailure)
        {
            ChatCommandRepository.TrySendMessage(userId, $"Failed to delete user {user.Username} ({requestedUserId}). Please check console for more information.");
            return;
        }

        ChatCommandRepository.TrySendMessage(userId, $"User {user.Username} ({requestedUserId}) has been deleted.");
    }
}