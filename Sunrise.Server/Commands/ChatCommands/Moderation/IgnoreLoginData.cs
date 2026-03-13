using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Moderation;

[ChatCommand("ignorelogindata", requiredPrivileges: UserPrivilege.Admin)]
public class IgnoreLoginData : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}ignorelogindata <user id> <true/false>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        if (!bool.TryParse(args[1], out var isIgnored))
        {
            ChatCommandRepository.SendMessage(session, "Invalid value. Use true or false.");
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

        var result = await database.Events.Users.SetRegisterEventIgnoredFromIpCheck(user.Id, isIgnored);

        if (result.IsFailure)
        {
            ChatCommandRepository.SendMessage(session, $"Failed to update login data ignore status: {result.Error}");
            return;
        }

        ChatCommandRepository.SendMessage(session, $"Successfully {(isIgnored ? "enabled" : "disabled")} IP check ignore for user {user.Username} (Id: {user.Id}).");
    }
}
