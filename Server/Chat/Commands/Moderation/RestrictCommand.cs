using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Moderation;

[ChatCommand("restrict", requiredPrivileges: UserPrivileges.Admin)]
public class RestrictCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}restrict <user id> <reason>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            CommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        if (args[1].Length is < 3 or > 256)
        {
            CommandRepository.SendMessage(session, "Reason must be between 3 and 256 characters.");
            return;
        }

        var reason = string.Join(" ", args[1..]);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(userId);

        if (user == null)
        {
            CommandRepository.SendMessage(session, "User not found.");
            return;
        }

        if (user.Privilege >= UserPrivileges.Admin)
        {
            CommandRepository.SendMessage(session, "You cannot restrict this user due to their privilege level.");
            return;
        }

        await database.UserService.Moderation.RestrictPlayer(user.Id, session.User.Id, reason);

        var isRestricted = await database.UserService.Moderation.IsRestricted(user.Id);

        CommandRepository.SendMessage(session,
            isRestricted
                ? $"User {user.Username} ({user.Id}) has been restricted."
                : $"User {user.Username} ({user.Id}) hasn't been restricted due to an error. Contact a developer.");
    }
}