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

        Task.Run(() => DeleteUser(session, userId));

        return Task.CompletedTask;
    }

    private async Task DeleteUser(Session session, int userId)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(userId);

        if (user == null)
        {
            CommandRepository.SendMessage(session, $"User {userId} not found.");
            return;
        }

        var isDeleted = await database.UserService.DeleteUser(user.Id);

        if (!isDeleted)
        {
            CommandRepository.SendMessage(session, $"Failed to delete user {user.Username} ({userId}). Please check console for more information.");
            return;
        }

        CommandRepository.SendMessage(session, $"User {user.Username} ({userId}) has been deleted.");
    }
}