using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.SuperUser;

[ChatCommand("giveselfprivilege", requiredPrivileges: UserPrivilege.SuperUser)]
public class GiveSelfPrivilegeCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}giveselfprivilege <privilege>; Example: {Configuration.BotPrefix}giveselfprivilege Admin");
            return;
        }

        UserPrivilege? privilege = Enum.TryParse(args[0], out UserPrivilege parsedPrivilege)
            ? parsedPrivilege
            : null;

        if (privilege == null)
        {
            ChatCommandRepository.SendMessage(session, $"Invalid privilege. Available privileges: {string.Join(", ", Enum.GetNames<UserPrivilege>())}");
            return;
        }

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var sessionUser = await database.Users.GetUser(session.UserId);

        if (sessionUser == null)
        {
            ChatCommandRepository.SendMessage(session, "User not found.");
            return;
        }

        if (sessionUser.Privilege.GetHighestPrivilege() <= privilege.Value)
        {
            ChatCommandRepository.SendMessage(session, "You cannot grant yourself a privilege equal to or higher than your current highest privilege.");
            return;
        }

        var oldPrivilege = sessionUser.Privilege;

        sessionUser.Privilege |= privilege.Value;

        await database.Users.UpdateUser(sessionUser);

        await database.Events.Users.AddUserChangePrivilegeEvent(
            new UserEventAction(sessionUser, session.IpAddress, sessionUser.Id, sessionUser),
            oldPrivilege,
            sessionUser.Privilege);

        ChatCommandRepository.SendMessage(session, $"Your privilege has been updated to {sessionUser.Privilege}.");
    }
}