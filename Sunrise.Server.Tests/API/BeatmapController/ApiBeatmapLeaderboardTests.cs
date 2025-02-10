using System.Net;
using System.Net.Http.Json;
using osu.Shared;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Extensions;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiBeatmapLeaderboardRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetBeatmapLeaderboard()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");

        var user = await CreateTestUser();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.EnrichWithBeatmapData(beatmap);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        score = await database.ScoreService.InsertScore(score);

        // Act
        var response = await client.GetAsync($"beatmap/{beatmap.Id}/leaderboard?mode={score.GameMode}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ScoresResponse>();
        Assert.NotNull(content);

        Assert.Contains(content.Scores, s => s.Id == score.Id);
    }

    [Theory]
    [InlineData(null)] // Global
    [InlineData(Mods.None)] // GlobalWithMods
    [InlineData(Mods.Hidden)] // GlobalWithMods
    public async Task TestGetBeatmapLeaderboardWithMultipleScores(Mods? mods = null)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

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

            score = await database.ScoreService.InsertScore(score);
            scores.Add(score);
        }

        // Act
        var response = await client.GetAsync($"beatmap/{beatmap.Id}/leaderboard?mode={beatmap.ModeInt}{(mods != null ? $"&mods={(int)mods}" : "")}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ScoresResponse>();
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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmap/1/leaderboard?mods={mods}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestGetBeatmapLeaderboardNotFound()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmap/1/leaderboard");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}