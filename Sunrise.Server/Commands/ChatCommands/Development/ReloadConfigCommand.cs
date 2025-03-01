using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("reloadconfig", requiredPrivileges: UserPrivilege.Developer)]
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
            ChatCommandRepository.SendMessage(session, "No changes detected.");
        }
        else
        {
            ChatCommandRepository.SendMessage(session, "Changes detected:");

            foreach (var change in changes)
            {
                ChatCommandRepository.SendMessage(session, change);
            }
        }

        return Task.CompletedTask;
    }
}