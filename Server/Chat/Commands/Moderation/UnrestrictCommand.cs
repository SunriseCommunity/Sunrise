using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Chat.Commands.Moderation;

[ChatCommand("unrestrict", requiredPrivileges: UserPrivileges.Admin)]
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

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(userId);

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

        await database.UserService.Moderation.UnrestrictPlayer(user.Id);
        
        for (var i = 0; i < 4; i++)
        {
            var stat = await database.UserService.Stats.GetUserStats(user.Id, (GameMode)i);
            var pp = await Calculators.CalculateUserWeightedPerformance(user.Id, (GameMode)i);
            var acc = await Calculators.CalculateUserWeightedAccuracy(user.Id, (GameMode)i);

            stat.PerformancePoints = pp;
            stat.Accuracy = acc;

            await database.UserService.Stats.UpdateUserStats(stat);
        }

        CommandRepository.SendMessage(session, $"User {user.Username} ({user.Id}) has been unrestricted.");
    }
}