using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands;

[ChatCommand("help")]
public class HelpCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var message = $"Available commands: {Configuration.BotPrefix}" + string.Join($", {Configuration.BotPrefix}",
            CommandRepository.GetAvailableCommands(session));

        CommandRepository.SendMessage(session, message);

        return Task.CompletedTask;
    }
}