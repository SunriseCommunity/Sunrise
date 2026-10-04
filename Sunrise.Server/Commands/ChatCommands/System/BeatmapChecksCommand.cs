using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Repositories;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.System;

[ChatCommand("beatmapchecks", requiredPrivileges: UserPrivilege.SuperUser)]
public class BeatmapChecksCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var total = await database.DbContext.BeatmapHashStatuses.NotCacheable().CountAsync();
        var deleted = await database.DbContext.BeatmapHashStatuses.NotCacheable().CountAsync(h => h.Status == BeatmapStatus.NotSubmitted);
        var due = await database.Calculations.GetDueBeatmapChecks().CountAsync();
        var next = await database.Calculations.GetDueBeatmapChecks().FirstOrDefaultAsync();

        var nextText = next == null
            ? "nothing is due"
            : $"next is beatmap {next.BeatmapId} (hash {next.BeatmapHash}, row {next.Id} of {total}, last checked {next.CheckedAt:u})";

        ChatCommandRepository.SendMessage(session,
            $"Beatmap checks ({CalculationRepository.BeatmapChecksPerTick} per minute): {due} of {total} hashes due, {deleted} marked deleted; {nextText}.");
    }
}
