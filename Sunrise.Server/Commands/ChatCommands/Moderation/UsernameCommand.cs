using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

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
        
        var username = string.Join(" ", args[1..]);

        var (isUsernameValid, error) = username.IsValidUsername();

        if (!isUsernameValid && args[1] != "filter")
        {
            ChatCommandRepository.SendMessage(session, error ?? "Invalid username");
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
            ChatCommandRepository.SendMessage(session, "You cannot change their nickname due to their privilege level.");
            return;
        }

        if (args[1] == "filter")
        {
            await database.Users.UpdateUserUsername(user, user.Username, $"filtered_{user.Id}", session.UserId);
            ChatCommandRepository.SendMessage(session, "Users nickname has been filtered.");
        }
        else
        {
            var foundUserByUsername = await database.Users.GetUser(username: username);

            if (foundUserByUsername != null)
            {
                if (foundUserByUsername.IsActive())
                {
                    ChatCommandRepository.SendMessage(session, "Username is already taken.");
                    return;
                }
                
                await database.Users.UpdateUserUsername(
                    foundUserByUsername,
                    foundUserByUsername.Username,
                    foundUserByUsername.Username.SetUsernameAsOld());
            }

            await database.Users.UpdateUserUsername(user, user.Username, username, session.UserId);

            ChatCommandRepository.SendMessage(session, "Users nickname has been updated.");
        }
    }
}