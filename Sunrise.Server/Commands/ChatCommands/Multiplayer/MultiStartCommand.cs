using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Multiplayer;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("start", "mp", isGlobal: true)]
public class MultiStartCommand : IChatCommand
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

        var time = 0;

        if (args is { Length: > 0 })
        {
            if (!int.TryParse(args[0], out var parsedTime))
                return Task.CompletedTask;

            time = parsedTime;
        }

        switch (time)
        {
            case > 60 * 1000 * 5:
                session.SendChannelMessage(channel.Name, "You can't set the start time to more than 5 minutes.");
                return Task.CompletedTask;
            case < 0:
                session.SendChannelMessage(channel.Name, "You can't set the start time to less than 0 seconds.");
                return Task.CompletedTask;
            case > 0:
                session.SendChannelMessage(channel.Name, $"Match will start in {TimeConverter.SecondsToMinutes(time, true)}.");
                session.Match.StartTimer(time, true, SendAlertMessage, OnFinish);
                return Task.CompletedTask;
            default:
                session.SendChannelMessage(channel.Name, "Match will start immediately.");
                session.Match.StartGame();
                return Task.CompletedTask;
        }

        async Task SendAlertMessage(MultiplayerMatch match, string message)
        {
            foreach (var player in match.Players.Values)
            {
                player.SendChannelMessage(channel.Name, message);
            }

            await Task.CompletedTask;
        }

        async Task OnFinish(MultiplayerMatch match)
        {
            match.StopTimer();
            match.StartGame();

            foreach (var player in match.Players.Values)
            {
                player.SendChannelMessage(channel.Name, "GLHF and enjoy the game!");
            }

            await Task.CompletedTask;
        }
    }
}