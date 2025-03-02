using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using static System.Int32;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("roll", isGlobal: true)]
public class RollCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var maxNumber = 100;

        if (args?.Length > 0)
            if (TryParse(args[0], out var parsedValue) && parsedValue > 0)
                maxNumber = parsedValue;
        
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                
        var user = await database.Users.GetUser(session.UserId);
        if (user == null)
            return;

        var message =
            $"{user.GetUserInGameChatString()} rolls {new Random().Next(0, maxNumber)} point(s)";

        if (channel != null)
            channel.SendToChannel(message);
        else
            ChatCommandRepository.SendMessage(session, message);
    }
}