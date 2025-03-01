using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("with")]
public class WithCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (session.LastBeatmapIdUsedWithCommand == null)
        {
            ChatCommandRepository.SendMessage(session,
                "You must use the !beatmap command (or use /np) before using the !with command.");
            return;
        }

        if (args == null || args.Length == 0)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}with <mods>; Example: {Configuration.BotPrefix}with HDHR");
            return;
        }

        var withMods = args[0].StringModsToMods();

        using var scope = ServicesProviderHolder.CreateScope();
        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        var beatmapSet = await beatmapService.GetBeatmapSet(session, beatmapId: session.LastBeatmapIdUsedWithCommand);

        if (beatmapSet == null)
        {
            ChatCommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == session.LastBeatmapIdUsedWithCommand);

        var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

        var (pp100, pp99, pp98, pp95) = await calculatorService.CalculatePerformancePoints(session,
            session.LastBeatmapIdUsedWithCommand.Value,
            beatmap?.ModeInt ?? 0,
            withMods);

        ChatCommandRepository.SendMessage(session,
            $"[{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {withMods.GetModsString()}| 95%: {pp95:0.00}pp | 98%: {pp98:0.00}pp | 99%: {pp99:0.00}pp | 100%: {pp100:0.00}pp | {TimeConverter.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating} ★");
    }
}