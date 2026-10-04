using System.Net;
using osu.Shared;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Tests;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Tests.API.BeatmapController;

[Collection("Integration tests collection")]
public class ApiBeatmapLeaderboardTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetBeatmapLeaderboard()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");

        var user = await CreateTestUser();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.PrepareForSubmission(beatmap);

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
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");

        EnvManager.Set("General:UseNewPerformanceCalculationAlgorithm", "true");

        var user = await CreateTestUser();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var scoreBestByPerformance = _mocker.Score.GetBestScoreableRandomScore();
        scoreBestByPerformance.UserId = user.Id;
        scoreBestByPerformance.PerformancePoints = 1000;
        scoreBestByPerformance.TotalScore = 1;
        scoreBestByPerformance.PrepareForSubmission(beatmap);

        await Database.Scores.AddScore(scoreBestByPerformance);

        var scoreBestByTotalScore = _mocker.Score.GetBestScoreableRandomScore();
        scoreBestByTotalScore.UserId = user.Id;
        scoreBestByTotalScore.PerformancePoints = 1;
        scoreBestByTotalScore.TotalScore = 1000;
        scoreBestByTotalScore.PrepareForSubmission(beatmap);
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

    public static IEnumerable<object[]> GetGameModes() => Enum.GetValues<GameMode>().Select(mode => new object[] { mode });

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestGetBeatmapLeaderboardEqualScoresKeepsEarlierScoreFirstAndPreservesRankTie(GameMode mode)
    {
        var client = App.CreateClient().UseClient("api");
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.ModeInt = (int)mode.ToVanillaGameMode();
        beatmapSet.IgnoreBeatmapRanking();
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var earlierUser = await CreateTestUser();
        var laterUser = await CreateTestUser();
        var playedAt = DateTime.UtcNow.AddMinutes(-10);

        var earlierScore = _mocker.Score.GetBestScoreableRandomScore();
        earlierScore.UserId = earlierUser.Id;
        earlierScore.GameMode = mode;
        earlierScore.Mods = mode.GetGamemodeMods();
        earlierScore.TotalScore = 1000;
        earlierScore.PerformancePoints = 100;
        earlierScore.WhenPlayed = playedAt;
        earlierScore.PrepareForSubmission(beatmap);
        Assert.Equal(mode, earlierScore.GameMode);
        earlierScore.ScoreHash = Guid.NewGuid().ToString("N");
        await Database.Scores.AddScore(earlierScore);

        var laterScore = _mocker.Score.GetBestScoreableRandomScore();
        laterScore.UserId = laterUser.Id;
        laterScore.GameMode = earlierScore.GameMode;
        laterScore.Mods = earlierScore.Mods;
        laterScore.TotalScore = 1000;
        laterScore.PerformancePoints = earlierScore.PerformancePoints;
        laterScore.WhenPlayed = playedAt.AddMinutes(1);
        laterScore.PrepareForSubmission(beatmap);
        laterScore.ScoreHash = Guid.NewGuid().ToString("N");
        await Database.Scores.AddScore(laterScore);

        var sameUserLaterScore = _mocker.Score.GetBestScoreableRandomScore();
        sameUserLaterScore.UserId = earlierUser.Id;
        sameUserLaterScore.GameMode = earlierScore.GameMode;
        sameUserLaterScore.Mods = earlierScore.Mods;
        sameUserLaterScore.TotalScore = 1000;
        sameUserLaterScore.PerformancePoints = earlierScore.PerformancePoints;
        sameUserLaterScore.WhenPlayed = playedAt;
        sameUserLaterScore.PrepareForSubmission(beatmap);
        sameUserLaterScore.ScoreHash = Guid.NewGuid().ToString("N");
        await Database.Scores.AddScore(sameUserLaterScore);

        var lowerUser = await CreateTestUser();
        var lowerScore = _mocker.Score.GetBestScoreableRandomScore();
        lowerScore.UserId = lowerUser.Id;
        lowerScore.GameMode = mode;
        lowerScore.Mods = earlierScore.Mods;
        lowerScore.TotalScore = mode.IsGameModeWithoutScoreMultiplier() ? 5000 : 500;
        lowerScore.PerformancePoints = mode.IsGameModeWithoutScoreMultiplier() ? 50 : 200;
        lowerScore.WhenPlayed = playedAt.AddMinutes(2);
        lowerScore.PrepareForSubmission(beatmap);
        lowerScore.ScoreHash = Guid.NewGuid().ToString("N");
        await Database.Scores.AddScore(lowerScore);

        var response = await client.GetAsync($"beatmap/{beatmap.Id}/leaderboard?mode={mode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoresResponse>();
        Assert.NotNull(content);
        Assert.Equal(new[] { earlierScore.Id, laterScore.Id, lowerScore.Id }, content.Scores.Select(score => score.Id));
        Assert.Equal(new int?[] { 1, 1, 3 }, content.Scores.Select(score => score.LeaderboardRank));
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
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var scoresNumber = _mocker.GetRandomInteger(minInt: 2, maxInt: 6);
        var scores = new List<Score>();

        for (var i = 0; i < scoresNumber; i++)
        {
            var user = await CreateTestUser();
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.UserId = user.Id;
            score.PrepareForSubmission(beatmap);

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
