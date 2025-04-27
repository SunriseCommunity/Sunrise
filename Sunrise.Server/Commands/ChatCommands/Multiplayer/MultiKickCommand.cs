using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("kick", "mp", isGlobal: true)]
public class MultiKickCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
        {
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");
        }

        if (session.Match.HasHostPrivileges(session) == false)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used by the host of the room.");
            return;
        }

        if (args == null || args.Length == 0)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp kick <username>");
            return;
        }
        
        var username = string.Join(" ", args[0..]);
        
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        
        var userToKick = await database.Users.GetUser(username: username);
        if (userToKick == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return;
        }
        
        var sessionToKick = session.Match.Players.Values.FirstOrDefault(x => x.UserId == userToKick.Id);
        if (sessionToKick == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return;
        }

        session.Match.RemovePlayer(sessionToKick, true);
    }
}