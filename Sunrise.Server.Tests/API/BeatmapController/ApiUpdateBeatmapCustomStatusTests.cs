using System.Net;
using System.Net.Http.Json;
using System.Text;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiUpdateBeatmapCustomStatusRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task TestUpdateBeatmapCustomStatus(bool hadCustomStatusBefore, bool wasHypedBefore)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        if (wasHypedBefore)
        {
            var addBeatmapHypeResult = await Database.Beatmaps.Hypes.AddBeatmapHypeFromUserInventory(user, beatmapSet.Id);
            if (addBeatmapHypeResult.IsFailure)
                throw new Exception(addBeatmapHypeResult.Error);
        }

        if (hadCustomStatusBefore)
        {
            var addCustomBeatmapStatusResult = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
            {
                Status = BeatmapStatusWeb.Qualified,
                UpdatedByUserId = user.Id,
                BeatmapHash = beatmap.Checksum!,
                BeatmapSetId = beatmap.BeatmapsetId
            });

            if (addCustomBeatmapStatusResult.IsFailure)
                throw new Exception(addCustomBeatmapStatusResult.Error);
        }

        // Act
        var response = await client.PostAsJsonAsync("beatmap/update-custom-status",
            new UpdateBeatmapsCustomStatusRequest
            {
                Ids = [beatmap.Id],
                Status = BeatmapStatusWeb.Loved
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var beatmapSetUpdated = await Database.Beatmaps.GetCachedBeatmapSet(beatmapSetId: beatmap.BeatmapsetId);
        Assert.NotNull(beatmapSetUpdated);

        var beatmapUpdated = beatmapSetUpdated.Beatmaps.First(b => b.Id == beatmap.Id);
        Assert.Equal(BeatmapStatusWeb.Loved, beatmapUpdated.StatusGeneric);

        var expectedEventsCount = 1;

        if (hadCustomStatusBefore)
            expectedEventsCount += 1;

        if (wasHypedBefore)
            expectedEventsCount += 2; // Both hype and clear hypes events;

        var (beatmapSetEvents, _) = await Database.Events.Beatmaps.GetBeatmapSetEvents(beatmapSet.Id);

        Assert.Equal(expectedEventsCount, beatmapSetEvents.Count);
    }

    [Fact]
    public async Task TestUpdateBeatmapCustomStatusWithMultipleIds()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var beatmapSetFirst = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmapFirst = beatmapSetFirst.Beatmaps.First();
        await _mocker.Beatmap.MockBeatmapSet(beatmapSetFirst);

        var beatmapSetSecond = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmapSecond = beatmapSetSecond.Beatmaps.First();
        await _mocker.Beatmap.MockBeatmapSet(beatmapSetSecond);

        // Act
        var response = await client.PostAsJsonAsync("beatmap/update-custom-status",
            new UpdateBeatmapsCustomStatusRequest
            {
                Ids = [beatmapFirst.Id, beatmapSecond.Id],
                Status = BeatmapStatusWeb.Approved
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var beatmapFirstUpdated = (await Database.Beatmaps.GetCachedBeatmapSet(beatmapId: beatmapFirst.Id))?.Beatmaps.First();
        Assert.NotNull(beatmapFirstUpdated);
        Assert.Equal(BeatmapStatusWeb.Approved, beatmapFirstUpdated.StatusGeneric);

        var (beatmapSetFirstEvents, _) = await Database.Events.Beatmaps.GetBeatmapSetEvents(beatmapFirst.BeatmapsetId);
        Assert.Single(beatmapSetFirstEvents);

        var beatmapSecondUpdated = (await Database.Beatmaps.GetCachedBeatmapSet(beatmapId: beatmapSecond.Id))?.Beatmaps.First();
        Assert.NotNull(beatmapSecondUpdated);
        Assert.Equal(BeatmapStatusWeb.Approved, beatmapSecondUpdated.StatusGeneric);

        var (beatmapSetSecondEvents, _) = await Database.Events.Beatmaps.GetBeatmapSetEvents(beatmapSecond.BeatmapsetId);
        Assert.Single(beatmapSetSecondEvents);

        var (beatmapSetsEvents, _) = await Database.Events.Beatmaps.GetBeatmapSetEvents();
        Assert.Equal(2, beatmapSetsEvents.Count);
    }
    
    [Fact]
    public async Task TestUpdateBeatmapCustomStatusToDefault()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First();
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        
        var addCustomBeatmapStatusResult = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Qualified,
            UpdatedByUserId = user.Id,
            BeatmapHash = beatmap.Checksum!,
            BeatmapSetId = beatmap.BeatmapsetId
        });

        if (addCustomBeatmapStatusResult.IsFailure)
            throw new Exception(addCustomBeatmapStatusResult.Error);

        // Act
        var response = await client.PostAsJsonAsync("beatmap/update-custom-status",
            new UpdateBeatmapsCustomStatusRequest
            {
                Ids = [beatmap.Id],
                Status = BeatmapStatusWeb.Unknown
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var beatmapCustomStatus = await Database.Beatmaps.CustomStatuses.GetCustomBeatmapStatus(beatmap.Checksum ?? throw new InvalidOperationException());
        Assert.Null(beatmapCustomStatus);

        var (beatmapSetsEvents, _) = await Database.Events.Beatmaps.GetBeatmapSetEvents();
        Assert.Equal(2, beatmapSetsEvents.Count);
    }
}

public class ApiUpdateBeatmapCustomStatusTests() : ApiTest(true)
{
    [Fact]
    public async Task TestUpdateBeatmapCustomStatusUnauthorized()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsync("beatmap/update-custom-status", new StringContent(string.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestUpdateBeatmapCustomStatusUnprivileged()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync("beatmap/update-custom-status", new StringContent(string.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestUpdateBeatmapCustomStatusInvalidBeatmapSetId(string beatmapId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        var json = $"{{\"ids\":\"[{beatmapId}]\", \"status\":\"{BeatmapStatusWeb.Ranked}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsJsonAsync("beatmap/update-custom-status", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestUpdateBeatmapCustomStatusBeatmapNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        // Act
        var response = await client.PostAsJsonAsync("beatmap/update-custom-status",
            new UpdateBeatmapsCustomStatusRequest
            {
                Ids = [1],
                Status = BeatmapStatusWeb.Ranked
            });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}