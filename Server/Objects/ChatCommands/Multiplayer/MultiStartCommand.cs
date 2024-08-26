using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects.ChatCommands.Multiplayer;

[ChatCommand("start", "mp", isGlobal: true)]
public class MultiStartCommand : IChatCommand
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

        var startTime = 0;

        if (args is { Length: > 0 })
        {
            if (!int.TryParse(args[0], out var parsedStartTime))
                return;

            startTime = parsedStartTime;
        }

        if (startTime > 60 * 1000 * 10)
        {
            session.SendChannelMessage(channel.Name, "You can't set the start time to more than 10 minutes.");
            return;
        }

        if (startTime > 0)
        {
            session.SendChannelMessage(channel.Name, $"Match will start in {startTime} seconds.");
            await Task.Delay(startTime * 1000);
        }

        session.Match.StartGame();

    }
}