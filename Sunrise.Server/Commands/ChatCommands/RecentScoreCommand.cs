using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.Server.Commands.ChatCommands;

[ChatCommand("rs")]
public class RecentScoreCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var lastScore = await database.Scores.GetUserLastScore(session.UserId);

        if (lastScore == null)
        {
            ChatCommandRepository.SendMessage(session, "No recent score found.");
            return;
        }

        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        var beatmapSet = await beatmapService.GetBeatmapSet(session, beatmapHash: lastScore.BeatmapHash);

        if (beatmapSet == null)
        {
            ChatCommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == lastScore.BeatmapId);

        if (beatmap == null)
        {
            ChatCommandRepository.SendMessage(session, "No beatmap found.");
            return;
        }

        var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

        // Mods can change difficulty rating, important to recalculate it for right medal unlocking
        if ((int)lastScore.GameMode != beatmap.ModeInt || (int)lastScore.Mods > 0)
            beatmap.DifficultyRating = await calculatorService
                .RecalculateBeatmapDifficulty(session, lastScore.BeatmapId, (int)lastScore.GameMode, lastScore.Mods);

        ChatCommandRepository.SendMessage(session,
            $"[{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {lastScore.Mods.GetModsString()}| GameMode: {lastScore.GameMode} | Acc: {lastScore.Accuracy:0.00}% | {lastScore.PerformancePoints:0.00}pp | {TimeConverter.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating:0.00} ★");
    }
}