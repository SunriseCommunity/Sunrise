using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands;

[ChatCommand("whoami")]
public class WhoamiCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        CommandRepository.SendMessage(session, $"You are {session.User.Username}. Your ID is {session.User.Id}. Your privileges are {session.User.Privilege}.");

        return Task.CompletedTask;
    }
}