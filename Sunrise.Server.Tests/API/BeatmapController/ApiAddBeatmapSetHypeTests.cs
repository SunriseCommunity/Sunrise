using System.Net;
using Microsoft.AspNetCore.Mvc;
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

public class ApiAddBeatmapSetHypeRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestAddBeatmapSetHype(bool shouldFillHypeTrain)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        EnvManager.Set("BeatmapHype:HypesToStartHypeTrain", shouldFillHypeTrain ? "1" : "100");

        // Act
        var response = await client.PostAsync("beatmapset/1/hype", new StringContent(string.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var beatmapHypeCount = await Database.Beatmaps.Hypes.GetBeatmapHypeCount(beatmapSet.Id);
        Assert.Equal(1, beatmapHypeCount);

        var (beatmapsWithHypeTrain, _) = await Database.Beatmaps.Hypes.GetHypedBeatmaps();

        Assert.Equal(shouldFillHypeTrain ? 1 : 0, beatmapsWithHypeTrain.Count);

        var (beatmapEvents, _) = await Database.Events.Beatmaps.GetBeatmapSetEvents(beatmapSet.Id);

        Assert.Single(beatmapEvents);
        Assert.Equal(user.Id, beatmapEvents.First().ExecutorId);
        Assert.Equal(BeatmapEventType.BeatmapSetHyped, beatmapEvents.First().EventType);

        var userHypes = await Database.Users.Inventory.GetInventoryItem(user.Id, ItemType.Hype);
        Assert.NotNull(userHypes);
        Assert.Equal(userHypes.Quantity, Configuration.UserHypesWeekly - 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestAddBeatmapSetHypeCantHypeIfNoMultipleHypeIsEnabled(bool isMultipleHypeEnabled)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        EnvManager.Set("BeatmapHype:AllowMultipleHypeFromSameUser", isMultipleHypeEnabled ? "true" : "false");

        var addBeatmapHypeBeforeResult = await Database.Beatmaps.Hypes.AddBeatmapHypeFromUserInventory(user, beatmapSet.Id);
        if (addBeatmapHypeBeforeResult.IsFailure)
            throw new Exception(addBeatmapHypeBeforeResult.Error);

        // Act
        var response = await client.PostAsync("beatmapset/1/hype", new StringContent(string.Empty));

        // Assert
        Assert.Equal(isMultipleHypeEnabled ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);


        if (!isMultipleHypeEnabled)
        {
            var responseString = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
            Assert.Contains("already hyped", responseString?.Detail.ToLower());
        }

        var beatmapHypeCount = await Database.Beatmaps.Hypes.GetBeatmapHypeCount(beatmapSet.Id);
        Assert.Equal(isMultipleHypeEnabled ? 2 : 1, beatmapHypeCount);

        var (beatmapEvents, _) = await Database.Events.Beatmaps.GetBeatmapSetEvents(beatmapSet.Id);
        Assert.Equal(isMultipleHypeEnabled ? 2 : 1, beatmapEvents.Count);

        var hypesShouldBeSpent = isMultipleHypeEnabled ? 2 : 1;

        var userHypes = await Database.Users.Inventory.GetInventoryItem(user.Id, ItemType.Hype);
        Assert.NotNull(userHypes);
        Assert.Equal(userHypes.Quantity, Configuration.UserHypesWeekly - hypesShouldBeSpent);
    }

    [Fact]
    public async Task TestAddBeatmapSetHypeCantHypeIfUserDoesntHasHypes()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        await Database.Users.Inventory.SetInventoryItem(user, ItemType.Hype, 0);

        // Act
        var response = await client.PostAsync("beatmapset/1/hype", new StringContent(string.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains("not enough hypes", responseString?.Detail.ToLower());

        var beatmapHypeCount = await Database.Beatmaps.Hypes.GetBeatmapHypeCount(beatmapSet.Id);
        Assert.Equal(0, beatmapHypeCount);
    }

    [Fact]
    public async Task TestAddBeatmapSetHypeCantHypeIfBeatmapSetHasCustomStatus()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Ranked,
            BeatmapHash = beatmapSet.Beatmaps.First().Checksum ?? throw new InvalidOperationException(),
            BeatmapSetId = beatmapSet.Id,
            UpdatedByUserId = user.Id
        });

        // Act
        var response = await client.PostAsync("beatmapset/1/hype", new StringContent(string.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains("beatmapset with custom beatmap status", responseString?.Detail?.ToLower());

        var beatmapHypeCount = await Database.Beatmaps.Hypes.GetBeatmapHypeCount(beatmapSet.Id);
        Assert.Equal(0, beatmapHypeCount);

        var userHypes = await Database.Users.Inventory.GetInventoryItem(user.Id, ItemType.Hype);
        Assert.NotNull(userHypes);
        Assert.Equal(userHypes.Quantity, Configuration.UserHypesWeekly);
    }
}

public class ApiAddBeatmapSetHypeTests() : ApiTest(true)
{
    [Fact]
    public async Task TestAddBeatmapSetHypeUnauthorized()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsync("beatmapset/1/hype", new StringContent(string.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    public async Task TestAddBeatmapSetHypeInvalidBeatmapSetId(string beatmapSetId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync($"beatmapset/{beatmapSetId}/hype", new StringContent(string.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestAddBeatmapSetHypeNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync("beatmapset/1/hype", new StringContent(string.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}