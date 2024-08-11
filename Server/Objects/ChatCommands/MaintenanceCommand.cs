using osu.Shared;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("maintenance", PlayerRank.SuperMod)]
public class MaintenanceCommand : IChatCommand
{
    public Task Handle(Session session, string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}maintenance <on/off>");
            return Task.CompletedTask;
        }

        switch (args[0])
        {
            case "on":
            {
                if (Configuration.OnMaintenance)
                {
                    CommandRepository.SendMessage(session, "Maintenance mode is already enabled.");
                    return Task.CompletedTask;
                }

                Configuration.OnMaintenance = true;
                CommandRepository.SendMessage(session, "Maintenance mode enabled.");

                var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

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
                    CommandRepository.SendMessage(session, "Maintenance mode is already disabled.");
                    return Task.CompletedTask;
                }

                Configuration.OnMaintenance = false;
                CommandRepository.SendMessage(session, "Maintenance mode disabled.");
                break;
            }
            default:
                CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}maintenance <on/off>");
                break;
        }

        return Task.CompletedTask;
    }
}