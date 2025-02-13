using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Development;

[ChatCommand("reloadconfig", requiredPrivileges: UserPrivileges.Developer)]
public class ReloadConfigCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var prevConfig = Configuration.GetConfig().AsEnumerable().ToArray();

        Configuration.GetConfig().Reload();

        var newConfig = Configuration.GetConfig().AsEnumerable().ToArray();

        var changes = new List<string>();

        foreach (var prev in prevConfig)
        {
            var newConfigValue = newConfig.FirstOrDefault(x => x.Key == prev.Key);

            if (newConfigValue.Key == null)
            {
                changes.Add($"Removed: {prev.Key}");
            }
            else if (newConfigValue.Value != prev.Value)
            {
                changes.Add($"Changed: {prev.Key} from {prev.Value} to {newConfigValue.Value}");
            }
        }

        foreach (var newField in newConfig)
        {
            var prevConfigValue = prevConfig.FirstOrDefault(x => x.Key == newField.Key);

            if (prevConfigValue.Key == null)
            {
                changes.Add($"Added: {newField.Key} with value {newField.Value}");
            }
        }

        if (changes.Count == 0)
        {
            CommandRepository.SendMessage(session, "No changes detected.");
        }
        else
        {
            CommandRepository.SendMessage(session, "Changes detected:");

            foreach (var change in changes)
            {
                CommandRepository.SendMessage(session, change);
            }
        }

        return Task.CompletedTask;
    }
}