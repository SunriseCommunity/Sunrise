using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Chat.Commands;

[ChatCommand("beatmap")]
public class BeatmapCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            CommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}beatmap <id> [<mods>]; Example: {Configuration.BotPrefix}beatmap 962782 HDHR");
            return;
        }

        if (!int.TryParse(args[0], out var beatmapId))
        {
            CommandRepository.SendMessage(session, "Invalid beatmap id.");
            return;
        }

        var withMods = Mods.None;

        if (args.Length >= 2) withMods = args[1].StringModsToMods();

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapId: beatmapId);

        if (beatmapSet == null)
        {
            CommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == beatmapId);

        session.LastBeatmapIdUsedWithCommand = beatmapId;

        var (pp100, pp99, pp98, pp95) =
            await Calculators.CalculatePerformancePoints(session, beatmapId, beatmap?.ModeInt ?? 0, withMods);

        CommandRepository.SendMessage(session,
            $"[{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {withMods.GetModsString()}| 95%: {pp95:0.00}pp | 98%: {pp98:0.00}pp | 99%: {pp99:0.00}pp | 100%: {pp100:0.00}pp | {Parsers.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating:0.00} ★");
    }
}