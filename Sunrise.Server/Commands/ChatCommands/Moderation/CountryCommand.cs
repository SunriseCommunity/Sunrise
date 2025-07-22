using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Moderation;

[ChatCommand("country", requiredPrivileges: UserPrivilege.Admin)]
public class CountryCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}country <user id> <\"new country\">. Check Alpha-2 codes of all countries here: https://www.iban.com/country-codes.\nExample: !country 1010 US");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        if (!Enum.TryParse<CountryCode>(args[1], out var newCountry))
        {
            ChatCommandRepository.SendMessage(session, "Invalid country code.");
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

        if (user.Privilege >= UserPrivilege.Admin)
        {
            ChatCommandRepository.SendMessage(session, "You cannot change their country due to their privilege level.");
            return;
        }
        
        await database.Users.UpdateUserCountry(user, user.Country, newCountry, session.UserId);
        ChatCommandRepository.SendMessage(session, "Users country has been updated.");
    }
}