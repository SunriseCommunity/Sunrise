using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("announce", requiredPrivileges: UserPrivileges.Developer)]
public class AnnounceCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}announce <mode> <message?>");
            return Task.CompletedTask;
        }

        var messageMode = args[0];

        if (messageMode != "custom" && messageMode != "restart")
        {
            CommandRepository.SendMessage(session, "Invalid mode. Available modes: custom, restart");
            return Task.CompletedTask;
        }

        if (messageMode == "custom" && args.Length < 2)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}announce custom <message>");
            return Task.CompletedTask;
        }

        var message = string.Join(" ", args[1..]);

        if (messageMode == "restart")
        {
            message = "Hello! Server is going down for a restart, it will take around a minute or two. You will be reconnected automatically." + "\n" +
                      "While you wait, you can still continue to play, all your scores will be uploaded once the server is back online." + "\n" +
                      "Thank you for your patience and playing on Sunrise! :)";
        }
        else if (message.Length is < 1 or > 2000)
        {
            CommandRepository.SendMessage(session, "Please don't yap too much. Keep it between 1 and 2000 characters.");
            return Task.CompletedTask;
        }

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var userSession in sessions.GetSessions())
        {
            CommandRepository.SendMessage(userSession, message);
        }

        return Task.CompletedTask;
    }
}