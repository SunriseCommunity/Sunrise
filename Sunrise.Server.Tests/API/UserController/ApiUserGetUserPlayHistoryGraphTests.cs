using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using Sunrise.Tests;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiUserGetUserPlayHistoryGraphTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetUserPlayHistoryGraphUserNotFound()
    {
        var client = App.CreateClient().UseClient("api");
        var userId = _mocker.GetRandomInteger();

        var response = await client.GetAsync($"user/{userId}/play-history-graph");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseContent?.Detail);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserPlayHistoryGraphInvalidRoute(string userId)
    {
        var client = App.CreateClient().UseClient("api");

        var response = await client.GetAsync($"user/{userId}/play-history-graph");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUserPlayHistoryGraphRestrictedUserNotFound()
    {
        var client = App.CreateClient().UseClient("api");
        var user = await CreateTestUser();
        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        var response = await client.GetAsync($"user/{user.Id}/play-history-graph");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserIsRestricted, responseContent?.Detail);
    }

    [Fact]
    public async Task TestGetUserPlayHistoryGraphReturnsEmptyWhenNoScores()
    {
        var client = App.CreateClient().UseClient("api");
        var user = await CreateTestUser();

        var response = await client.GetAsync($"user/{user.Id}/play-history-graph");

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsyncWithAppConfig<PlayHistorySnapshotsResponse>();
        Assert.NotNull(data);
        Assert.Equal(0, data.TotalCount);
        
        Assert.NotNull(data.Snapshots);
        Assert.Empty(data.Snapshots);
    }

    [Fact]
    public async Task TestGetUserPlayHistoryGraphScoreLastSecondDecemberCountedAsDecember()
    {
        var client = App.CreateClient().UseClient("api");
        var user = await CreateTestUser();
        var lastSecondDecemberUtc = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        await AddValidScoreForUser(user.Id, lastSecondDecemberUtc, beatmapIdSeed: 1);

        var response = await client.GetAsync($"user/{user.Id}/play-history-graph");

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsyncWithAppConfig<PlayHistorySnapshotsResponse>();
        Assert.NotNull(data);
        Assert.True(data.TotalCount >= 1, "Expected at least one snapshot (December 2024)");
        
        var decemberSnapshot = data.Snapshots.FirstOrDefault(s =>
            s.SavedAt is { Year: 2024, Month: 12 });
        Assert.NotNull(decemberSnapshot);
        Assert.True(decemberSnapshot.PlayCount >= 1, "Score played on last second of December must be counted in December");

        var januarySnapshot = data.Snapshots.FirstOrDefault(s =>
            s.SavedAt is { Year: 2025, Month: 1 });
        Assert.True(januarySnapshot == null || januarySnapshot.PlayCount == 0,
            "Score on last second of December must not be counted as January");
    }

    [Fact]
    public async Task TestGetUserPlayHistoryGraphScoreFirstSecondJanuaryCountedAsJanuary()
    {
        var client = App.CreateClient().UseClient("api");
        var user = await CreateTestUser();
        var firstSecondJanuaryUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await AddValidScoreForUser(user.Id, firstSecondJanuaryUtc, beatmapIdSeed: 2);

        var response = await client.GetAsync($"user/{user.Id}/play-history-graph");

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsyncWithAppConfig<PlayHistorySnapshotsResponse>();
        Assert.NotNull(data);
        Assert.True(data.TotalCount >= 1, "Expected at least one snapshot (January 2025)");

        var januarySnapshot = data.Snapshots.FirstOrDefault(s =>
            s.SavedAt is { Year: 2025, Month: 1 });
        Assert.NotNull(januarySnapshot);
        Assert.True(januarySnapshot.PlayCount >= 1, "Score played on first second of January must be counted in January");

        var decemberSnapshot = data.Snapshots.FirstOrDefault(s =>
            s.SavedAt is { Year: 2024, Month: 12 });
        Assert.True(decemberSnapshot == null || decemberSnapshot.PlayCount == 0,
            "Score on first second of January must not be counted as December");
    }

    [Fact]
    public async Task TestGetUserPlayHistoryGraphMultipleScoresDifferentMonths()
    {
        var client = App.CreateClient().UseClient("api");
        var user = await CreateTestUser();
        await AddValidScoreForUser(user.Id, new DateTime(2024, 3, 15, 12, 0, 0, DateTimeKind.Utc), beatmapIdSeed: 10);
        await AddValidScoreForUser(user.Id, new DateTime(2024, 6, 20, 12, 0, 0, DateTimeKind.Utc), beatmapIdSeed: 20);
        await AddValidScoreForUser(user.Id, new DateTime(2024, 9, 1, 12, 0, 0, DateTimeKind.Utc), beatmapIdSeed: 30);

        var response = await client.GetAsync($"user/{user.Id}/play-history-graph");

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsyncWithAppConfig<PlayHistorySnapshotsResponse>();
        Assert.NotNull(data);
        Assert.Equal(3, data.TotalCount);

        Assert.Equal(1, data.Snapshots.First(s => s.SavedAt is { Month: 3, Year: 2024 }).PlayCount);
        Assert.Equal(1, data.Snapshots.First(s => s.SavedAt is { Month: 6, Year: 2024 }).PlayCount);
        Assert.Equal(1, data.Snapshots.First(s => s.SavedAt is { Month: 9, Year: 2024 }).PlayCount);
    }

    [Fact]
    public async Task TestGetUserPlayHistoryGraphMultipleScoresSameMonthAggregated()
    {
        var client = App.CreateClient().UseClient("api");
        var user = await CreateTestUser();
        await AddValidScoreForUser(user.Id, new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc), beatmapIdSeed: 40);
        await AddValidScoreForUser(user.Id, new DateTime(2024, 5, 15, 12, 0, 0, DateTimeKind.Utc), beatmapIdSeed: 41);
        await AddValidScoreForUser(user.Id, new DateTime(2024, 5, 31, 23, 59, 59, DateTimeKind.Utc), beatmapIdSeed: 42);

        var response = await client.GetAsync($"user/{user.Id}/play-history-graph");

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsyncWithAppConfig<PlayHistorySnapshotsResponse>();
        Assert.NotNull(data);
        Assert.Equal(1, data.TotalCount);
        
        var maySnapshot = data.Snapshots.Single(s => s.SavedAt is { Month: 5, Year: 2024 });
        Assert.Equal(3, maySnapshot.PlayCount);
    }

    [Fact]
    public async Task TestGetUserPlayHistoryGraphReturnsCorrectStructure()
    {
        var client = App.CreateClient().UseClient("api");
        var user = await CreateTestUser();
        await AddValidScoreForUser(user.Id, new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc), beatmapIdSeed: 50);

        var response = await client.GetAsync($"user/{user.Id}/play-history-graph");

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsyncWithAppConfig<PlayHistorySnapshotsResponse>();
        Assert.NotNull(data);
        Assert.True(data.TotalCount >= 1);
        Assert.NotNull(data.Snapshots);
        Assert.True(data.Snapshots.Count >= 1);
        var snapshot = data.Snapshots.First();
        Assert.True(snapshot.PlayCount >= 1);
        Assert.True(snapshot.SavedAt is { Year: >= 2024, Month: >= 1 });
    }

    private async Task AddValidScoreForUser(int userId, DateTime whenPlayedUtc, int beatmapIdSeed)
    {
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = userId;
        score.WhenPlayed = whenPlayedUtc;
        score.BeatmapId = Math.Abs(userId * 1000 + beatmapIdSeed);
        score.ScoreHash = _mocker.GetRandomString(32);
        score.BeatmapStatus = BeatmapStatus.Ranked;
        score.SubmissionStatus = SubmissionStatus.Best;
        score.IsScoreable = true;
        score.IsPassed = true;
        score.WhenPlayed = score.WhenPlayed.ToDatabasePrecision();
        score.ClientTime = score.ClientTime.ToDatabasePrecision();

        await Database.Scores.AddScore(score);
    }
}
