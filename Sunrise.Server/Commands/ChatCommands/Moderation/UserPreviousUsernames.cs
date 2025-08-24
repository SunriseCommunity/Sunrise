using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Moderation;

[ChatCommand("userprevioususernames", requiredPrivileges: UserPrivilege.Admin)]
public class UserPreviousUsernames : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}userprevioususernames <user id>");
            return;
        }

        if (!int.TryParse(args[0], out var userId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid user id.");
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

        var previousUsernames = await database.Events.Users.GetUserPreviousUsernameChangeEvents(user.Id,
            new QueryOptions(true, new Pagination(1, 3))
            {
                QueryModifier = query => query.Cast<EventUser>().OrderByDescending(e => e.Id)
            });

        var usernames = previousUsernames.Select(e => e.GetData<UserUsernameChanged>())
            .Where(u => u != null)
            .Select((data, idx) =>
            {
                var isUsernameFiltered = data!.NewUsername.Contains("filtered");
                var isUsernameHidden = data.IsHiddenFromPreviousUsernames != null && data.IsHiddenFromPreviousUsernames.Value;

                return $"Event Id: {previousUsernames[idx].Id} | {data.OldUsername} -> {data.NewUsername} | Shown as \"{data.OldUsername}\" (Is shown: {!isUsernameHidden && !isUsernameFiltered})";
            }).ToList();


        if (usernames.Count == 0)
        {
            ChatCommandRepository.SendMessage(session, "No previous usernames found.");
            return;
        }

        ChatCommandRepository.SendMessage(session, $"Previous usernames for {user.Username}:\n{string.Join("\n", usernames)}");
    }
}