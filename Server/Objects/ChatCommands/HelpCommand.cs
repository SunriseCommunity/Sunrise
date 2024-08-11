using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("help")]
public class HelpCommand : IChatCommand
{
    public Task Handle(Session session, string[]? args)
    {
        CommandRepository.SendMessage(session, $"Available commands: {Configuration.BotPrefix}" + string.Join($", {Configuration.BotPrefix}", CommandRepository.GetCurrentCommands()));
        return Task.CompletedTask;
    }
}