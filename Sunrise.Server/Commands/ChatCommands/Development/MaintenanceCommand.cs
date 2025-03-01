using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("maintenance", requiredPrivileges: UserPrivilege.Developer)]
public class MaintenanceCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}maintenance <on/off>");
            return Task.CompletedTask;
        }

        switch (args[0])
        {
            case "on":
            {
                if (Configuration.OnMaintenance)
                {
                    ChatCommandRepository.SendMessage(session, "Maintenance mode is already enabled.");
                    return Task.CompletedTask;
                }

                Configuration.OnMaintenance = true;
                ChatCommandRepository.SendMessage(session, "Maintenance mode enabled.");

                var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

                foreach (var userSession in sessions.GetSessions())
                {
                    userSession.SendBanchoMaintenance();
                }

                break;
            }
            case "off":
            {
                if (!Configuration.OnMaintenance)
                {
                    ChatCommandRepository.SendMessage(session, "Maintenance mode is already disabled.");
                    return Task.CompletedTask;
                }

                Configuration.OnMaintenance = false;
                ChatCommandRepository.SendMessage(session, "Maintenance mode disabled.");
                break;
            }
            default:
                ChatCommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}maintenance <on/off>");
                break;
        }

        return Task.CompletedTask;
    }
}