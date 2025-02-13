using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands;

[ChatCommand("meow")]
public class MeowCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        CommandRepository.SendMessage(session, "ヾ(•ω•`)o Meow~");
        return Task.CompletedTask;
    }
}