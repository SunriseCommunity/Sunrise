using osu.Shared;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("beatmap")]
public class BeatmapCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}beatmap <id> [<mods>]; Example: {Configuration.BotPrefix}beatmap 962782 HDHR");
            return;
        }

        if (!int.TryParse(args[0], out var beatmapId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid beatmap id.");
            return;
        }

        var withMods = Mods.None;

        if (args.Length >= 2) withMods = args[1].StringModsToMods();


        using var scope = ServicesProviderHolder.CreateScope();
        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        var beatmapSet = await beatmapService.GetBeatmapSet(session, beatmapId: beatmapId);

        if (beatmapSet == null)
        {
            ChatCommandRepository.SendMessage(session, "Beatmap set not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == beatmapId);
        
        if (beatmap == null)
        {
            ChatCommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        session.LastBeatmapIdUsedWithCommand = beatmapId;

        var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

        var (pp100, pp99, pp98, pp95) =
            await calculatorService.CalculatePerformancePoints(session, beatmapId, beatmap.ModeInt, withMods);

        ChatCommandRepository.SendMessage(session,
            $"{beatmap.GetBeatmapInGameChatString(beatmapSet)} {withMods.GetModsString()}| 95%: {pp95:0.00}pp | 98%: {pp98:0.00}pp | 99%: {pp99:0.00}pp | 100%: {pp100:0.00}pp | {TimeConverter.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating:0.00} ★");
    }
}