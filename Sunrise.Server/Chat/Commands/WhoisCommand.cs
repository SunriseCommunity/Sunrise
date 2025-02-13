using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands;

[ChatCommand("whois")]
public class WhoisCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}whois <username>");
            return;
        }

        var username = args[0];
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(username: username);

        if (user == null)
        {
            CommandRepository.SendMessage(session, "User not found.");
            return;
        }

        CommandRepository.SendMessage(session, $"User {user.Username} ({user.Id}) has {user.Privilege} privileges.");
    }
}