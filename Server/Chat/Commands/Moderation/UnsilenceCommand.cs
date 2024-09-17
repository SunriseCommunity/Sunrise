using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Chat.Commands.Moderation;

[ChatCommand("unsilence", requiredPrivileges: UserPrivileges.Admin)]
public class UnsilenceCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}unsilence <user id>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            CommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var user = await database.GetUser(userId);

        if (user == null)
        {
            CommandRepository.SendMessage(session, "User not found.");
            return;
        }

        if (user.SilencedUntil < DateTime.UtcNow)
        {
            CommandRepository.SendMessage(session, "User is not silenced.");
            return;
        }

        user.SilencedUntil = DateTime.MinValue;

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var player = sessions.GetSession(userId: user.Id);

        player?.SendSilenceStatus();

        await database.UpdateUser(user);

        CommandRepository.SendMessage(session, $"User {user.Username} ({user.Id}) has been unsilenced.");
    }
}