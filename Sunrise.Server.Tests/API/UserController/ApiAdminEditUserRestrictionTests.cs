using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Server.Commands.ChatCommands.System;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminEditUserRestrictionTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminEditUserRestrictionWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/edit/restriction", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditUserRestrictionWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/edit/restriction", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditUserRestrictionWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync("user/999999/edit/restriction",
            new StringContent("{\"is_restrict\":true,\"restriction_reason\":\"test\"}", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminEditUserRestrictionWithoutBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestAdminRestrictUserWithoutReason()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = null
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.RestrictionReasonMustBeProvided, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminRestrictUserWithEmptyReason()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = "   "
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.RestrictionReasonMustBeProvided, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminRestrictUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var isRestrictedBefore = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.False(isRestrictedBefore);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = "Test restriction reason"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var isRestrictedAfter = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.True(isRestrictedAfter);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserAccountStatus.Restricted, updatedUser.AccountStatus);

        var (_, restrictEvents) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.Restrict)
            });
        Assert.NotEmpty(restrictEvents);
        var restrictEvent = restrictEvents.First();
        Assert.Equal(UserEventType.Restrict, restrictEvent.EventType);
        Assert.Contains("Test restriction reason", restrictEvent.JsonData);
        Assert.Contains(adminUser.Id.ToString(), restrictEvent.JsonData);
    }

    [Fact]
    public async Task TestAdminRestrictUserShouldUpdateUserStats()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var (session, targetUser) = await CreateTestSession();

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        adminUser.Country = targetUser.Country;
        await CreateTestUser(adminUser);

        const int beatmapId = 4866852;
        const string beatmapHash = "017478eac4eb68b38cff9d85c9822453";
        const Mods mods = (Mods)72;
        const GameMode gameMode = GameMode.Standard;
        const string osuVersion = "20250815";

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.Checksum = beatmapHash;
        beatmap.Id = beatmapId;
        beatmap.UpdateBeatmapRanking(BeatmapStatusWeb.Ranked);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        IFormFile formFile = new FormFile(new MemoryStream(new byte[1024]), 0, 1024, "data", $"{_mocker.GetRandomString(6)}.osr");
        var replayRecordResult = await Database.Scores.Files.AddReplayFile(session.UserId, formFile);

        if (replayRecordResult.IsFailure)
            throw new Exception(replayRecordResult.Error);

        var replayRecord = replayRecordResult.Value;

        var seedScore = new Score
        {
            UserId = session.UserId,
            BeatmapId = beatmapId,
            ScoreHash = "b4708da107c7f7f0df908c4050673190",
            BeatmapHash = beatmapHash,
            ReplayFileId = replayRecord.Id,
            TotalScore = 542973,
            MaxCombo = 153,
            Count300 = 115,
            Count100 = 12,
            Count50 = 0,
            CountMiss = 3,
            CountKatu = 6,
            CountGeki = 17,
            Perfect = false,
            Mods = mods,
            Grade = "B",
            IsPassed = true,
            IsScoreable = true,
            SubmissionStatus = SubmissionStatus.Best,
            GameMode = gameMode,
            WhenPlayed = DateTime.Parse("2025-10-09 19:39:31.755556"),
            OsuVersion = osuVersion,
            BeatmapStatus = BeatmapStatus.Ranked,
            ClientTime = DateTime.Parse("2025-10-09 19:39:31"),
            Accuracy = 91.53845977783203,
            PerformancePoints = 426.69985159889916
        };

        seedScore.LocalProperties = seedScore.LocalProperties.FromScore(seedScore);
        var addScoreResult = await Database.Scores.AddScore(seedScore);

        if (addScoreResult.IsFailure)
            throw new Exception(addScoreResult.Error);

        var recalculateUserStatsCommand = new RecalculateUserStatsCommand();
        await recalculateUserStatsCommand.RecalculateUserStats(session.UserId, CancellationToken.None);

        var userStats = await Database.Users.Stats.GetUserStats(targetUser.Id, gameMode);
        Assert.NotNull(userStats);
        Assert.True(userStats.PerformancePoints > 0);

        var (globalRank, countryRank) = await Database.Users.Ranks.GetUserRanks(targetUser, gameMode);

        Assert.Equal(1, globalRank);
        Assert.Equal(1, countryRank);

        var isRestrictedBefore = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.False(isRestrictedBefore);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = "Test restriction reason"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var isRestrictedAfter = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.True(isRestrictedAfter);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserAccountStatus.Restricted, updatedUser.AccountStatus);

        var userStatsUpdated = await Database.Users.Stats.GetUserStats(updatedUser.Id, gameMode);
        Assert.NotNull(userStatsUpdated);
        Assert.Equal(0, userStatsUpdated.PerformancePoints);

        var (globalRankUpdated, countryRankUpdated) = await Database.Users.Ranks.GetUserRanks(updatedUser, gameMode);

        Assert.Equal(2, globalRankUpdated);
        Assert.Equal(2, countryRankUpdated);
    }

    [Fact]
    public async Task TestAdminRestrictUserAlreadyRestricted()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Previous restriction");

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = "New restriction reason"
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserAlreadyRestricted, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminUnrestrictUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Test restriction");

        var isRestrictedBefore = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.True(isRestrictedBefore);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = false,
                RestrictionReason = null
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var isRestrictedAfter = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.False(isRestrictedAfter);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.NotEqual(UserAccountStatus.Restricted, updatedUser.AccountStatus);

        var (_, unrestrictEvents) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.Unrestrict)
            });
        Assert.NotEmpty(unrestrictEvents);
        var unrestrictEvent = unrestrictEvents.First();
        Assert.Equal(UserEventType.Unrestrict, unrestrictEvent.EventType);
        Assert.Contains(adminUser.Id.ToString(), unrestrictEvent.JsonData);
    }

    [Fact]
    public async Task TestAdminUnrestrictUserShouldUpdateUserStats()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var (session, targetUser) = await CreateTestSession();

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        adminUser.Country = targetUser.Country;
        await CreateTestUser(adminUser);

        const int beatmapId = 4866852;
        const string beatmapHash = "017478eac4eb68b38cff9d85c9822453";
        const Mods mods = (Mods)72;
        const GameMode gameMode = GameMode.Standard;
        const string osuVersion = "20250815";

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.Checksum = beatmapHash;
        beatmap.Id = beatmapId;
        beatmap.UpdateBeatmapRanking(BeatmapStatusWeb.Ranked);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        IFormFile formFile = new FormFile(new MemoryStream(new byte[1024]), 0, 1024, "data", $"{_mocker.GetRandomString(6)}.osr");
        var replayRecordResult = await Database.Scores.Files.AddReplayFile(session.UserId, formFile);

        if (replayRecordResult.IsFailure)
            throw new Exception(replayRecordResult.Error);

        var replayRecord = replayRecordResult.Value;

        var seedScore = new Score
        {
            UserId = session.UserId,
            BeatmapId = beatmapId,
            ScoreHash = "b4708da107c7f7f0df908c4050673190",
            BeatmapHash = beatmapHash,
            ReplayFileId = replayRecord.Id,
            TotalScore = 542973,
            MaxCombo = 153,
            Count300 = 115,
            Count100 = 12,
            Count50 = 0,
            CountMiss = 3,
            CountKatu = 6,
            CountGeki = 17,
            Perfect = false,
            Mods = mods,
            Grade = "B",
            IsPassed = true,
            IsScoreable = true,
            SubmissionStatus = SubmissionStatus.Best,
            GameMode = gameMode,
            WhenPlayed = DateTime.Parse("2025-10-09 19:39:31.755556"),
            OsuVersion = osuVersion,
            BeatmapStatus = BeatmapStatus.Ranked,
            ClientTime = DateTime.Parse("2025-10-09 19:39:31"),
            Accuracy = 91.53845977783203,
            PerformancePoints = 426.69985159889916
        };

        seedScore.LocalProperties = seedScore.LocalProperties.FromScore(seedScore);
        var addScoreResult = await Database.Scores.AddScore(seedScore);

        if (addScoreResult.IsFailure)
            throw new Exception(addScoreResult.Error);

        var recalculateUserStatsCommand = new RecalculateUserStatsCommand();
        await recalculateUserStatsCommand.RecalculateUserStats(session.UserId, CancellationToken.None);

        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Test restriction");

        var isRestrictedBefore = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.True(isRestrictedBefore);

        var userStats = await Database.Users.Stats.GetUserStats(targetUser.Id, gameMode);
        Assert.NotNull(userStats);
        Assert.Equal(0, userStats.PerformancePoints);

        var (globalRank, countryRank) = await Database.Users.Ranks.GetUserRanks(targetUser, gameMode);

        Assert.Equal(2, globalRank);
        Assert.Equal(2, countryRank);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = false,
                RestrictionReason = null
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var isRestrictedAfter = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.False(isRestrictedAfter);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserAccountStatus.Active, updatedUser.AccountStatus);

        var userStatsUpdated = await Database.Users.Stats.GetUserStats(targetUser.Id, gameMode);
        Assert.NotNull(userStatsUpdated);
        Assert.True(userStatsUpdated.PerformancePoints > 0);

        var (globalRankUpdated, countryRankUpdated) = await Database.Users.Ranks.GetUserRanks(updatedUser, gameMode);
        Assert.Equal(1, globalRankUpdated);
        Assert.Equal(1, countryRankUpdated);
    }

    [Fact]
    public async Task TestAdminUnrestrictUserNotRestricted()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var isRestrictedBefore = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.False(isRestrictedBefore);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = false,
                RestrictionReason = null
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserAlreadyUnrestricted, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminRestrictAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetAdminUser = _mocker.User.GetRandomUser();
        targetAdminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(targetAdminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetAdminUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = "Try to restrict admin"
            });

        // Assert
        // Admin users cannot be restricted
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}