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

[ChatCommand("hideprevioususername", requiredPrivileges: UserPrivilege.Admin)]
public class HidePreviousUsername : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}hideprevioususername <username change event id> <is hidden>");
            return;
        }

        if (!int.TryParse(args[0], out var eventId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid event id.");
            return;
        }
        
        if (!bool.TryParse(args[1], out var isHidden))
        {
            ChatCommandRepository.SendMessage(session, "Invalid is hidden value. Use true or false.");
            return;
        }
        
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var changeUsernameEventVisibilityResult = await database.Events.Users.SetUserChangeUsernameEventVisibility(eventId, isHidden);

        if (changeUsernameEventVisibilityResult.IsFailure)
        {
            ChatCommandRepository.SendMessage(session, $"Failed to change event visibility: {changeUsernameEventVisibilityResult.Error}");
            return;
        }

        ChatCommandRepository.SendMessage(session, $"Successfully changed event visibility. Event Id: {eventId}");
    }
}