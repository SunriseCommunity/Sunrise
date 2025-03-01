using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("whois")]
public class WhoisCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}whois <username>");
            return;
        }

        var username = string.Join(" ", args[0..]);
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = await database.Users.GetUser(username: username);

        if (user == null)
        {
            ChatCommandRepository.SendMessage(session, "User not found.");
            return;
        }

        ChatCommandRepository.SendMessage(session, $"User {user.Username} ({user.Id}) has {user.Privilege} privileges.");
    }
}