using Microsoft.EntityFrameworkCore;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("claimowner", isHidden: true)]
public class ClaimOwnerCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            return;
        }

        var superUserSecretPassword = Configuration.SuperUserSecretPassword;

        if (superUserSecretPassword == null || args[0] != superUserSecretPassword)
        {
            return;
        }

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var superUserExists = await database.DbContext.Set<User>().AnyAsync(u => u.Privilege.HasFlag(UserPrivilege.SuperUser));

        if (superUserExists)
        {
            ChatCommandRepository.SendMessage(session, "I'll be frank, someone either updated the database with super user or you found some bug/exploit. Either way, you cannot claim owner privilege now. Try after some time.");
            return;
        }

        var sessionUser = await database.Users.GetUser(session.UserId);

        if (sessionUser == null)
        {
            ChatCommandRepository.SendMessage(session, "User not found.");
            return;
        }

        var oldPrivilege = sessionUser.Privilege;

        sessionUser.Privilege |= UserPrivilege.SuperUser;

        await database.Users.UpdateUser(sessionUser);

        await database.Events.Users.AddUserChangePrivilegeEvent(
            new UserEventAction(sessionUser, session.IpAddress, sessionUser.Id, sessionUser),
            oldPrivilege,
            sessionUser.Privilege);

        ChatCommandRepository.SendMessage(session, $"Your privilege has been updated to {sessionUser.Privilege}! If you want to get admin privileges, use the {Configuration.BotPrefix}giveselfprivilege command.");
    }
}