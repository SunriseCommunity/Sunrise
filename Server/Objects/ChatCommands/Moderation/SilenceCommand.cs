using HOPEless.Bancho;
using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands.Moderation;

[ChatCommand("silence", PlayerRank.SuperMod)]
public class SilenceCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 4)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}silence <user id> <amount> <unit (s/m/h/d)> <reason>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            CommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var user = await database.GetUser(userId);

        if (user == null)
        {
            CommandRepository.SendMessage(session, "User not found.");
            return;
        }

        if (user.Privilege >= PlayerRank.SuperMod)
        {
            CommandRepository.SendMessage(session, "You cannot silence this user due to their privilege level.");
            return;
        }

        if (user.SilencedUntil > DateTime.UtcNow)
        {
            CommandRepository.SendMessage(session, $"User is already silenced. He will be unsilenced at {user.SilencedUntil}. UTC+0 ");
            return;
        }

        if (!int.TryParse(args[1], out var amount))
        {
            CommandRepository.SendMessage(session, "Invalid amount.");
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
                CommandRepository.SendMessage(session, "Invalid time unit.");
                return;
        }

        if (time > 7 * 86400)
        {
            CommandRepository.SendMessage(session, "Silence time cannot exceed 7 days.");
            return;
        }

        var reason = string.Join(" ", args[3..]);

        user.SilencedUntil = DateTime.UtcNow.AddSeconds(time);

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        var player = sessions.GetSession(user.Id);

        player?.SendSilenceStatus(time, reason);

        sessions.WriteToAllSessions(PacketType.ServerUserSilenced, user.Id);

        await database.UpdateUser(user);

        CommandRepository.SendMessage(session, $"User {user.Username} ({user.Id}) has been silenced until {user.SilencedUntil:yyyy-MM-dd HH:mm:ss}. UTC+0 | Reason: {reason}");
    }
}