using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("viewconfig", requiredPrivileges: UserPrivilege.Developer)]
public class ViewConfigCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var config = Configuration.GetConfig().AsEnumerable().ToArray();

        string[] configKeys = ["General", "BeatmapHype", "Moderation", "Database", "Files", "Redis", "Hangfire", "Bot", "API"];

        ChatCommandRepository.SendMessage(session,
            config.Length == 0
                ? "No configuration settings found."
                : string.Join("\n",
                    config.Where(x => configKeys.Any(k => x.Key.StartsWith(k)) && x.Value?.Length >= 1)
                        .Select(kv => $"{kv.Key}: {kv.Value}")));

        return Task.CompletedTask;
    }
}