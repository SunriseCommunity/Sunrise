using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Moderation;

[ChatCommand("restrict", requiredPrivileges: UserPrivilege.Admin)]
public class RestrictCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}restrict <user id> <reason>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        var restrictionReason = string.Join(" ", args[1..]);

        if (restrictionReason.Length is < 3 or > 256)
        {
            ChatCommandRepository.SendMessage(session, "Reason must be between 3 and 256 characters.");
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

        if (user.Privilege >= UserPrivilege.Admin)
        {
            ChatCommandRepository.SendMessage(session, "You cannot restrict this user due to their privilege level.");
            return;
        }

        await database.Users.Moderation.RestrictPlayer(user.Id, session.UserId, restrictionReason, TimeSpan.FromDays(365 * 10));

        var isRestricted = await database.Users.Moderation.IsUserRestricted(user.Id);

        ChatCommandRepository.SendMessage(session,
            isRestricted
                ? $"User {user.Username} ({user.Id}) has been restricted."
                : $"User {user.Username} ({user.Id}) hasn't been restricted due to an error. Contact a developer.");
    }
}