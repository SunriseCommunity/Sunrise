using System.Net;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Tests.Controllers;

[Collection("Integration tests collection")]
public class ScoreControllerGetScoresTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes() => Enum.GetValues<GameMode>().Select(mode => new object[] { mode });

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestGetScoresReturnsOldestEqualValueFirstForEveryGameMode(GameMode mode)
    {
        var client = App.CreateClient().UseClient("osu");
        var (_, user) = await CreateTestSession();
        var earlierUser = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.ModeInt = (int)mode.ToVanillaGameMode();
        beatmapSet.IgnoreBeatmapRanking();
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var earlierScore = _mocker.Score.GetBestScoreableRandomScore();
        earlierScore.UserId = earlierUser.Id;
        earlierScore.GameMode = mode;
        earlierScore.Mods = mode.GetGamemodeMods();
        earlierScore.TotalScore = 1000;
        earlierScore.PerformancePoints = 100;
        earlierScore.WhenPlayed = DateTime.UtcNow.AddMinutes(-10);
        earlierScore.PrepareForSubmission(beatmap);
        earlierScore.ScoreHash = Guid.NewGuid().ToString("N");
        Assert.Equal(mode, earlierScore.GameMode);
        await Database.Scores.AddScore(earlierScore);

        var laterScore = _mocker.Score.GetBestScoreableRandomScore();
        laterScore.UserId = user.Id;
        laterScore.GameMode = mode;
        laterScore.Mods = earlierScore.Mods;
        laterScore.TotalScore = earlierScore.TotalScore;
        laterScore.PerformancePoints = earlierScore.PerformancePoints;
        laterScore.WhenPlayed = earlierScore.WhenPlayed.AddMinutes(1);
        laterScore.PrepareForSubmission(beatmap);
        laterScore.ScoreHash = Guid.NewGuid().ToString("N");
        await Database.Scores.AddScore(laterScore);

        var response = await client.GetAsync($"/web/osu-osz2-getscores.php?us={Uri.EscapeDataString(user.Username)}&ha={Uri.EscapeDataString(user.Passhash)}&vv=4&v={(int)LeaderboardType.Global}&c={beatmap.Checksum}&f=test.osu&i={beatmap.BeatmapsetId}&m={(int)mode.ToVanillaGameMode()}&mods={(int)earlierScore.Mods}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.NotEqual("error: pass", body);
        var rows = body.Split('\n').Skip(5).Select(line => line.Split('|')).ToList();
        Assert.Equal(new[] { earlierScore.Id, laterScore.Id }, rows.Select(fields => int.Parse(fields[0])));
        Assert.Equal(new[] { 1, 2 }, rows.Select(fields => int.Parse(fields[13])));
    }
}
