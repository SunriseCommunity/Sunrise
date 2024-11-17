using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Chat.Commands;

[ChatCommand("rs")]
public class RecentScoreCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var lastScore = await database.ScoreService.GetUserLastScore(session.User.Id);

        if (lastScore == null)
        {
            CommandRepository.SendMessage(session, "No recent score found.");
            return;
        }

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapHash: lastScore.BeatmapHash);

        if (beatmapSet == null)
        {
            CommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == lastScore.BeatmapId);

        // Mods can change difficulty rating, important to recalculate it for right medal unlocking
        if ((int)lastScore.GameMode != beatmap.ModeInt || (int)lastScore.Mods > 0)
            beatmap.DifficultyRating = await Calculators
                .RecalcuteBeatmapDifficulty(session, lastScore.BeatmapId, (int)lastScore.GameMode, lastScore.Mods);

        CommandRepository.SendMessage(session,
            $"[{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {lastScore.Mods.GetModsString()}| GameMode: {lastScore.GameMode} | Acc: {lastScore.Accuracy:0.00}% | {lastScore.PerformancePoints:0.00}pp | {Parsers.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating:0.00} ★");
    }
}