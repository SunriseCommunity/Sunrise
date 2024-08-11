using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("beatmap")]
public class BeatmapCommand : IChatCommand
{
    public async Task Handle(Session session, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}beatmap <id>; Example: {Configuration.BotPrefix}beatmap 962782");
            return;
        }

        var beatmapId = int.Parse(args[0]);

        var beatmapSet = await BeatmapService.GetBeatmapSet(beatmapId);

        if (beatmapSet == null)
        {
            CommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == beatmapId);

        var (pp100, pp99, pp98, pp95) = await Calculators.CalculatePerformancePoints(beatmapId, (int)session.Attributes.Status.PlayMode, precision: false);

        CommandRepository.SendMessage(session, $"{beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}] | 95%: {pp95}pp | 98%: {pp98}pp | 99%: {pp99}pp | 100%: {pp100}pp | {Parsers.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating} ★");
    }
}