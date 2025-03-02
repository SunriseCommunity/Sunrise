using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Moderation;

[ChatCommand("unrestrict", requiredPrivileges: UserPrivilege.Admin)]
public class UnrestrictCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}unrestrict <user id>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = await database.Users.GetUser(userId);

        if (user == null)
        {
            ChatCommandRepository.SendMessage(session, "User not found.");
            return;
        }

        if (user.IsRestricted() == false)
        {
            ChatCommandRepository.SendMessage(session, "User is not restricted... yet.");
            return;
        }

        await database.Users.Moderation.UnrestrictPlayer(user.Id);

        ChatCommandRepository.SendMessage(session, $"User {user.Username} ({user.Id}) has been unrestricted.");
    }
}