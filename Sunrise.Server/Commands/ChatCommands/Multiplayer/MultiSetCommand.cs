using HOPEless.osu;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("set", "mp", isGlobal: true)]
public class MultiSetCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
        {
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");
        }

        if (session.Match.HasHostPrivileges(session) == false)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used by the host of the room.");
            return Task.CompletedTask;
        }

        if (args == null || args.Length == 0)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp set <teammode> [<scoremode>] [<size>]");
            return Task.CompletedTask;
        }

        var currentMatch = session.Match.Match;

        if (args.Length >= 1)
        {
            if (Enum.TryParse<MultiTeamTypes>(args[0], true, out var teamMode) == false)
            {
                session.SendChannelMessage(channel.Name, "Invalid team mode.");
                return Task.CompletedTask;
            }

            currentMatch.MultiTeamType = teamMode;
        }

        if (args.Length >= 2)
        {
            if (Enum.TryParse<MultiWinConditions>(args[1], true, out var scoreMode) == false)
            {
                session.SendChannelMessage(channel.Name, "Invalid score mode.");
                return Task.CompletedTask;
            }

            currentMatch.MultiWinCondition = scoreMode;
        }

        if (args.Length >= 3)
        {
            if (int.TryParse(args[2], out var size) == false)
            {
                session.SendChannelMessage(channel.Name, "Invalid size.");
                return Task.CompletedTask;
            }

            if (size is < 1 or > 16)
            {
                session.SendChannelMessage(channel.Name, "Size must be between 1 and 16.");
                return Task.CompletedTask;
            }

            for (var i = 0; i < 16; i++)
            {
                session.Match.UpdateLock(i, i < size);
            }
        }

        session.Match.UpdateMatchSettings(currentMatch, session);

        session.SendChannelMessage(channel.Name, "Match settings have been updated.");
        return Task.CompletedTask;
    }
}