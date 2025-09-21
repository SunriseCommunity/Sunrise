using System.Net;
using osu.Shared;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiBeatmapLeaderboardRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetBeatmapLeaderboard()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");

        var user = await CreateTestUser();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.EnrichWithBeatmapData(beatmap);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        await Database.Scores.AddScore(score);

        // Act
        var response = await client.GetAsync($"beatmap/{beatmap.Id}/leaderboard?mode={score.GameMode}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoresResponse>();
        Assert.NotNull(content);

        Assert.Contains(content.Scores, s => s.Id == score.Id);
    }

    [Fact]
    public async Task TestGetBeatmapLeaderboardShowBestByScore()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");

        EnvManager.Set("General:UseNewPerformanceCalculationAlgorithm", "true");

        var user = await CreateTestUser();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var scoreBestByPerformance = _mocker.Score.GetBestScoreableRandomScore();
        scoreBestByPerformance.UserId = user.Id;
        scoreBestByPerformance.PerformancePoints = 1000;
        scoreBestByPerformance.TotalScore = 1;
        scoreBestByPerformance.EnrichWithBeatmapData(beatmap);

        await Database.Scores.AddScore(scoreBestByPerformance);

        var scoreBestByTotalScore = _mocker.Score.GetBestScoreableRandomScore();
        scoreBestByTotalScore.UserId = user.Id;
        scoreBestByTotalScore.PerformancePoints = 1;
        scoreBestByTotalScore.TotalScore = 1000;
        scoreBestByTotalScore.EnrichWithBeatmapData(beatmap);
        scoreBestByTotalScore.GameMode = scoreBestByPerformance.GameMode;
        await Database.Scores.AddScore(scoreBestByTotalScore);

        // Act
        var response = await client.GetAsync($"beatmap/{beatmap.Id}/leaderboard?mode={scoreBestByPerformance.GameMode}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoresResponse>();
        Assert.NotNull(content);

        Assert.Contains(content.Scores, s => s.Id == scoreBestByTotalScore.Id); // Even using new performance calculation algorithm, leaderboard should show best by total score
    }

    [Theory]
    [InlineData(null)] // Global
    [InlineData(Mods.None)] // GlobalWithMods
    [InlineData(Mods.Hidden)] // GlobalWithMods
    public async Task TestGetBeatmapLeaderboardWithMultipleScores(Mods? mods = null)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var scoresNumber = _mocker.GetRandomInteger(minInt: 2, maxInt: 6);
        var scores = new List<Score>();

        for (var i = 0; i < scoresNumber; i++)
        {
            var user = await CreateTestUser();
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.UserId = user.Id;
            score.EnrichWithBeatmapData(beatmap);

            score.Mods = i % 2 == 0 ? Mods.Hidden : Mods.None;

            await Database.Scores.AddScore(score);
            scores.Add(score);
        }

        // Act
        var response = await client.GetAsync($"beatmap/{beatmap.Id}/leaderboard?mode={beatmap.ModeInt}{(mods != null ? $"&mods={(int)mods}" : "")}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoresResponse>();
        Assert.NotNull(content);

        scores = scores.Where(s => mods == null || s.Mods == mods).ToList();
        Assert.Equal(scores.Count, content.Scores.Count);
    }
}

public class ApiBeatmapLeaderboardTests : ApiTest
{
    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetBeatmapLeaderboardInvalidBeatmapId(string beatmapId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmap/{beatmapId}/leaderboard");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetBeatmapLeaderboardInvalidMode(string mode)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmap/1/leaderboard?mode={mode}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("101")]
    public async Task TestGetBeatmapLeaderboardInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmap/1/leaderboard?limit={limit}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("9999999999999999999")]
    [InlineData("test")]
    public async Task TestGetBeatmapLeaderboardInvalidMods(string mods)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmap/1/leaderboard?mods={mods}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestGetBeatmapLeaderboardNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmap/1/leaderboard?mode=0");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}