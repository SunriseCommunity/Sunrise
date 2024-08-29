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


        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var lastScore = await database.GetUserLastScore(session.User.Id);

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

        CommandRepository.SendMessage(session, $"[{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {lastScore.Mods.GetModsString()}| Acc: {lastScore.Accuracy:0.00}% | {lastScore.PerformancePoints:0.00}pp | {Parsers.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating} ★");
    }
}