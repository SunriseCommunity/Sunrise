using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("whoami")]
public class WhoamiCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        ChatCommandRepository.SendMessage(session, $"You are {session.User.Username}. Your ID is {session.User.Id}. Your privileges are {session.User.Privilege}.");

        return Task.CompletedTask;
    }
}