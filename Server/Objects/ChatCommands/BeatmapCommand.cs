using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("beatmap")]
public class BeatmapCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}beatmap <id>; Example: {Configuration.BotPrefix}beatmap 962782");
            return;
        }

        var beatmapId = int.Parse(args[0]);

        var beatmapSet = await BeatmapService.GetBeatmapSet(session, beatmapId: beatmapId);

        if (beatmapSet == null)
        {
            CommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == beatmapId);

        var (pp100, pp99, pp98, pp95) = await Calculators.CalculatePerformancePoints(session, beatmapId, beatmap?.ModeInt ?? 0);

        CommandRepository.SendMessage(session, $"[{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] | 95%: {pp95:0.00}pp | 98%: {pp98:0.00}pp | 99%: {pp99:0.00}pp | 100%: {pp100:0.00}pp | {Parsers.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating} ★");
    }
}