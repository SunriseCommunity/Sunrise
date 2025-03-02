using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Commands.ChatCommands.Moderation;

[ChatCommand("silence", requiredPrivileges: UserPrivilege.Admin)]
public class SilenceCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 4)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}silence <user id> <amount> <unit (s/m/h/d)> <reason>");
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

        if (user.Privilege.HasFlag(UserPrivilege.Admin))
        {
            ChatCommandRepository.SendMessage(session, "You cannot silence this user due to their privilege level.");
            return;
        }

        if (user.SilencedUntil > DateTime.UtcNow)
        {
            ChatCommandRepository.SendMessage(session,
                $"User is already silenced. He will be unsilenced at {user.SilencedUntil}. UTC+0 ");
            return;
        }

        if (!int.TryParse(args[1], out var amount))
        {
            ChatCommandRepository.SendMessage(session, "Invalid amount.");
            return;
        }

        int time;

        switch (args[2].ToLower())
        {
            case "s":
                time = amount;
                break;
            case "m":
                time = amount * 60;
                break;
            case "h":
                time = amount * 3600;
                break;
            case "d":
                time = amount * 86400;
                break;
            default:
                ChatCommandRepository.SendMessage(session, "Invalid time unit.");
                return;
        }

        if (time > 7 * 86400)
        {
            ChatCommandRepository.SendMessage(session, "Silence time cannot exceed 7 days.");
            return;
        }

        var reason = string.Join(" ", args[3..]);

        user.SilencedUntil = DateTime.UtcNow.AddSeconds(time);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var player = sessions.GetSession(userId: user.Id);

        player?.SendSilenceStatus(time, reason);

        sessions.WriteToAllSessions(PacketType.ServerUserSilenced, user.Id);

        await database.Users.UpdateUser(user);

        ChatCommandRepository.SendMessage(session,
            $"User {user.Username} ({user.Id}) has been silenced until {user.SilencedUntil:yyyy-MM-dd HH:mm:ss}. UTC+0 | Reason: {reason}");
    }
}