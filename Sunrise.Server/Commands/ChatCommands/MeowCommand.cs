using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("meow")]
public class MeowCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        ChatCommandRepository.SendMessage(session, "ヾ(•ω•`)o Meow~");
        return Task.CompletedTask;
    }
}