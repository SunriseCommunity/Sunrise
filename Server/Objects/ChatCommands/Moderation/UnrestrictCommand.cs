using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands.Moderation;

[ChatCommand("unrestrict", requiredRank: PlayerRank.SuperMod)]
public class UnrestrictCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}unrestrict <user id>");
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

        if (user.IsRestricted == false)
        {
            CommandRepository.SendMessage(session, "User is not restricted... yet.");
            return;
        }

        user.IsRestricted = false;

        await database.UpdateUser(user);

        CommandRepository.SendMessage(session, $"User {user.Username} ({user.Id}) has been unrestricted.");
    }
}