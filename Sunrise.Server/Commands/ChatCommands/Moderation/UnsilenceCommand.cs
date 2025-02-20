using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Commands.ChatCommands.Moderation;

[ChatCommand("unsilence", requiredPrivileges: UserPrivilege.Admin)]
public class UnsilenceCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}unsilence <user id>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(userId);

        if (user == null)
        {
            ChatCommandRepository.SendMessage(session, "User not found.");
            return;
        }

        if (user.SilencedUntil < DateTime.UtcNow)
        {
            ChatCommandRepository.SendMessage(session, "User is not silenced.");
            return;
        }

        user.SilencedUntil = DateTime.MinValue;

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var player = sessions.GetSession(userId: user.Id);

        player?.SendSilenceStatus();

        await database.UserService.UpdateUser(user);

        ChatCommandRepository.SendMessage(session, $"User {user.Username} ({user.Id}) has been unsilenced.");
    }
}