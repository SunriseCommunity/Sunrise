using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;

namespace Sunrise.Server.Commands.ChatCommands.Moderation;

[ChatCommand("username", requiredPrivileges: UserPrivilege.Admin)]
public class UsernameCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}username <user id> <\"new username\" or filter>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid user id.");
            return;
        }

        if (args[1].Length is < 2 or > 32)
        {
            ChatCommandRepository.SendMessage(session, "Username must be between 2 and 32 characters.");
            return;
        }

        var (isUsernameValid, error) = args[1].IsValidUsername();

        if (!isUsernameValid && args[1] != "filter")
        {
            ChatCommandRepository.SendMessage(session, error ?? "Invalid username");
            return;
        }

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(userId);

        if (user == null)
        {
            ChatCommandRepository.SendMessage(session, "User not found.");
            return;
        }

        if (user.Privilege >= UserPrivilege.Admin)
        {
            ChatCommandRepository.SendMessage(session, "You cannot filter their nickname due to their privilege level.");
            return;
        }

        if (args[1] == "filter")
        {
            await database.UserService.UpdateUserUsername(user, user.Username, $"filtered_{user.Id}", session.User.Id);
            ChatCommandRepository.SendMessage(session, "Users nickname has been filtered.");

        }
        else
        {
            var foundUserByUsername = await database.UserService.GetUser(username: args[1]);

            if (foundUserByUsername != null && foundUserByUsername.IsActive())
            {
                ChatCommandRepository.SendMessage(session, "Username is already taken.");
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

            ChatCommandRepository.SendMessage(session, "Users nickname has been updated.");
        }


    }
}