using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("whoami")]
public class WhoamiCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                
        var user = await database.Users.GetUser(session.UserId);
        if (user == null)
            return;
        
        ChatCommandRepository.SendMessage(session, $"You are {user.Username}. Your ID is {user.Id}. Your privileges are {user.Privilege}.");
    }
}