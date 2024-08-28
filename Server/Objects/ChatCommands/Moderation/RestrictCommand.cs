using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands.Moderation;

[ChatCommand("restrict", requiredRank: PlayerRank.SuperMod)]
public class RestrictCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}restrict <user id>");
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

        if (user.Privilege >= PlayerRank.SuperMod)
        {
            CommandRepository.SendMessage(session, "You cannot restrict this user due to their privilege level.");
            return;
        }

        user.IsRestricted = true;

        await database.UpdateUser(user);

        CommandRepository.SendMessage(session, $"User {user.Username} ({user.Id}) has been restricted.");

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var player = sessions.GetSession(userId: user.Id);

        player?.SendRestriction();
    }
}