using Sunrise.Server.Data;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands;

[ChatCommand("best")]
public class BestCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var userId = session.User.Id;

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        if (args is { Length: >= 1 })
        {
            var user = await database.GetUser(username: args[0]);
            Console.WriteLine(user);

            if (user == null)
            {
                CommandRepository.SendMessage(session, "User not found.");
                return;
            }

            userId = user.Id;
        }

        var scores = await database.GetUserBestScores(userId, session.Attributes.Status.PlayMode);

        var bestScores = scores.OrderByDescending(x => x.PerformancePoints).Take(5).ToList();

        var result = $"[★ {(args?.Length > 0 ? args[0] : session.User.Username)}'s Best Scores]\n";

        foreach (var (score, index) in bestScores.Select((value, i) => (value, i)))
        {
            var beatmapSet = await BeatmapService.GetBeatmapSet(session, beatmapHash: score.BeatmapHash);

            if (beatmapSet == null) continue;

            var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == score.BeatmapId);

            result += $"[{index + 1}] [{beatmap!.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap?.Version}]] {score.Mods.GetModsString()}| Acc: {score.Accuracy:0.00}% | {score.PerformancePoints:0.00}pp | {Parsers.SecondsToString(beatmap?.TotalLength ?? 0)} | {beatmap?.DifficultyRating} ★\n";
        }

        CommandRepository.SendMessage(session, result);
    }
}