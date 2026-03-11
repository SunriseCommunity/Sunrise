using Microsoft.Extensions.DependencyInjection;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Repositories;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.System;

[ChatCommand("refreshosuversions", requiredPrivileges: UserPrivilege.SuperUser)]
public class RefreshOsuVersionsCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        ChatCommandRepository.SendMessage(session, "Refreshing osu! client version cache...");

        using var scope = ServicesProviderHolder.CreateScope();
        var osuVersionService = scope.ServiceProvider.GetRequiredService<OsuVersionService>();
        var osuVersionRepository = scope.ServiceProvider.GetRequiredService<OsuVersionRepository>();

        await osuVersionService.ForceRefreshVersions();

        var lines = new List<string>();
        foreach (var stream in OsuVersion.SupportedStreams)
        {
            var version = await osuVersionRepository.GetCachedVersion(stream);
            if (version != null)
                lines.Add($"  {stream}: {version}");
        }

        if (lines.Count == 0)
        {
            ChatCommandRepository.SendMessage(session, "Failed to fetch osu! versions. Check server logs for details.");
            return;
        }

        ChatCommandRepository.SendMessage(session, $"Refreshed osu! client versions:\n{string.Join("\n", lines)}");
    }
}
