using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("help")]
public class HelpCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var message = $"Available commands: {Configuration.BotPrefix}" + string.Join($", {Configuration.BotPrefix}",
            await ChatCommandRepository.GetAvailableCommands(session));

        ChatCommandRepository.SendMessage(session, message);
    }
}