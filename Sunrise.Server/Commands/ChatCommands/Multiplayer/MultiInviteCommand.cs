using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("invite", "mp", isGlobal: true)]
public class MultiInviteCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");

        if (args == null || args.Length == 0)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp invite <username>");
            return Task.CompletedTask;
        }

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var invitee = sessions.GetSession(args[0]);

        if (invitee == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return Task.CompletedTask;
        }

        invitee.SendMultiInvite(session.Match.Match, session);

        session.SendChannelMessage(channel.Name, $"Invite sent to {args[0]}.");

        return Task.CompletedTask;
    }
}