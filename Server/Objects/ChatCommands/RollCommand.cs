using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;
using static System.Int32;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("roll", isGlobal: true)]
public class RollCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var maxNumber = 100;

        if (args?.Length > 0)
        {
            if (TryParse(args[0], out var parsedValue))
            {
                maxNumber = parsedValue;
            }
        }

        var message = $"[https://osu.{Configuration.Domain}/{session.User.Id} {session.User.Username}] rolls {new Random().Next(0, maxNumber)} point(s)";

        if (channel != null)
        {
            channel.SendToChannel(message);
        }
        else
        {
            CommandRepository.SendMessage(session, message);
        }

        return Task.CompletedTask;
    }
}