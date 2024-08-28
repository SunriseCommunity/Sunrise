using Sunrise.Server.Managers;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("with")]
public class WithCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {

        if (session.LastBeatmapIdUsedWithCommand == null)
        {
            CommandRepository.SendMessage(session, "You must use the !beatmap command (or use /np) before using the !with command.");
            return;
        }

        if (args == null || args.Length == 0)
        {
            CommandRepository.SendMessage(session, $"Usage: {Configuration.BotPrefix}with <mods>; Example: {Configuration.BotPrefix}with HDHR");
            return;
        }

        var withMods = args[0].StringModsToMods();

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapId: session.LastBeatmapIdUsedWithCommand);

        if (beatmapSet == null)
        {
            CommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == session.LastBeatmapIdUsedWithCommand);

        var (pp100, pp99, pp98, pp95) = await Calculators.CalculatePerformancePoints(session, session.LastBeatmapIdUsedWithCommand.Value, beatmap?.ModeInt ?? 0, withMods);

        CommandRepository.SendMessage(session, $"[{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {withMods.GetModsString()}| 95%: {pp95:0.00}pp | 98%: {pp98:0.00}pp | 99%: {pp99:0.00}pp | 100%: {pp100:0.00}pp | {Parsers.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating} ★");
    }
}