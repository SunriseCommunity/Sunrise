using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiGetBeatmapSetEventsRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetBeatmapSetEvents()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        await Database.Events.Beatmaps.AddBeatmapSetHypeClearEvent(user.Id, beatmapSet.Id);

        for (var i = 0; i < 2; i++)
        {
            await Database.Events.Beatmaps.AddBeatmapStatusChangedEvent(user.Id, beatmapSet.Id, beatmapSet.Beatmaps.First().Checksum ?? throw new InvalidOperationException(), BeatmapStatusWeb.Loved);
        }

        for (var i = 0; i < 3; i++)
        {
            await Database.Events.Beatmaps.AddBeatmapSetHypeEvent(user.Id, beatmapSet.Id);
        }

        // Act
        var response = await client.GetAsync("beatmapset/1/events");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetEventsResponse>();
        Assert.NotNull(content);

        var events = content.Events;

        Assert.NotEmpty(events);
        Assert.Equal(6, content.TotalCount);

        Assert.Equal(1, events.Count(e => e.BeatmapEventType == BeatmapEventType.BeatmapSetHypeCleared));
        Assert.Equal(2, events.Count(e => e.BeatmapEventType == BeatmapEventType.BeatmapStatusChanged));
        Assert.Equal(3, events.Count(e => e.BeatmapEventType == BeatmapEventType.BeatmapSetHyped));
    }
    
    [Fact]
    public async Task TestGetBeatmapSetEventsTestPageAndLimit()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        
        for (var i = 0; i < 3; i++)
        {
            await Database.Events.Beatmaps.AddBeatmapSetHypeEvent(user.Id, beatmapSet.Id);
        }

        // Act
        var response = await client.GetAsync("beatmapset/1/events?limit=1&page=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetEventsResponse>();
        Assert.NotNull(content);

        var events = content.Events;

        Assert.NotEmpty(events);
        Assert.Equal(3, content.TotalCount);

        Assert.Equal(2, events.First().EventId);
    }
}

public class ApiGetBeatmapSetEventsTests() : ApiTest(true)
{
    [Fact]
    public async Task TestGetBeatmapSetEventsUnauthorized()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmapset/1/events");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetBeatmapSetEventsForbiddenIfHasNoPrivileges()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("beatmapset/1/events");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    public async Task TestGetBeatmapSetEventsInvalidBeatmapSetId(string beatmapSetId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        // Act
        var response = await client.GetAsync($"beatmapset/{beatmapSetId}/events");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}