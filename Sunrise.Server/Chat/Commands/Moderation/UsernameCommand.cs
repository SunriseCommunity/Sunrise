using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Extensions;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Moderation;

[ChatCommand("username", requiredPrivileges: UserPrivileges.Admin)]
public class UsernameCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}username <user id> <\"new username\" or filter>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            CommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        if (args[1].Length is < 2 or > 32)
        {
            CommandRepository.SendMessage(session, "Username must be between 2 and 32 characters.");
            return;
        }

        if (!args[1].IsValidUsername(true) && args[1] != "filter")
        {
            CommandRepository.SendMessage(session, "Invalid username.");
            return;
        }

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(userId);

        if (user == null)
        {
            CommandRepository.SendMessage(session, "User not found.");
            return;
        }

        if (user.Privilege >= UserPrivileges.Admin)
        {
            CommandRepository.SendMessage(session, "You cannot filter their nickname due to their privilege level.");
            return;
        }

        if (args[1] == "filter")
        {
            await database.UserService.UpdateUserUsername(user, user.Username, $"filtered_{user.Id}", session.User.Id);
            CommandRepository.SendMessage(session, "Users nickname has been filtered.");

        }
        else
        {
            var foundUserByUsername = await database.UserService.GetUser(username: args[1]);

            if (foundUserByUsername != null && foundUserByUsername.IsActive())
            {
                CommandRepository.SendMessage(session, "Username is already taken.");
                return;
            }

            if (foundUserByUsername != null)
            {
                await database.UserService.UpdateUserUsername(
                    foundUserByUsername,
                    foundUserByUsername.Username,
                    foundUserByUsername.Username.SetUsernameAsOld());
            }


            await database.UserService.UpdateUserUsername(user, user.Username, args[1], session.User.Id);

            CommandRepository.SendMessage(session, "Users nickname has been updated.");
        }


    }
}