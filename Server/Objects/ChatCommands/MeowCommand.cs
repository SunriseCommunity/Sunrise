using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("meow")]
public class MeowCommand : IChatCommand
{
    public Task Handle(Session session, string[]? args)
    {
        CommandRepository.SendMessage(session, "ヾ(•ω•`)o Meow~");
        return Task.CompletedTask;
    }
}