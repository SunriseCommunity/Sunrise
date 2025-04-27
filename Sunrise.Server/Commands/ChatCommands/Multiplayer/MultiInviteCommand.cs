using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("invite", "mp", isGlobal: true)]
public class MultiInviteCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");

        if (args == null || args.Length == 0)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp invite <username>");
            return;
        }
        
        var username = string.Join(" ", args[0..]);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var invitee = sessions.GetSession(username);

        if (invitee == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return;
        }
        
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var selfUser = await database.Users.GetUser(session.UserId);

        if (selfUser == null)
        {
            session.SendChannelMessage(channel.Name, "Sender not found.");
            return;
        }
        
        // FIXME: Should sender see the invite message? Need to investigate

        invitee.SendMultiInvite(session.Match.Match, selfUser);

        session.SendChannelMessage(channel.Name, $"Invite sent to {username}.");
    }
}