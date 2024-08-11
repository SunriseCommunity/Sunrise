using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("whoami")]
public class WhoamiCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel != null)
        {
            channel.SendToChannel($"You are {session.User.Username}. Your ID is {session.User.Id}. Your privileges are {session.User.Privilege}.");
        }
        else
        {
            CommandRepository.SendMessage(session, $"You are {session.User.Username}. Your ID is {session.User.Id}. Your privileges are {session.User.Privilege}.");
        }

        return Task.CompletedTask;
    }
}