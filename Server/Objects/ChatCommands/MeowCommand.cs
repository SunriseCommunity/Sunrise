using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("meow")]
public class MeowCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        CommandRepository.SendMessage(session, "ヾ(•ω•`)o Meow~");
        return Task.CompletedTask;
    }
}