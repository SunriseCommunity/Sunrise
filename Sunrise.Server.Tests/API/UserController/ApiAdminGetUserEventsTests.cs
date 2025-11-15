using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminGetUserEventsTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetUserEventsWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/events");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUserEventsWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/events");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUserEventsWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/999999/events");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestGetUserEvents()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        await Database.Events.Users.AddUserChangeDescriptionEvent(
            new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
            "Old description",
            "New description");

        await Database.Events.Users.AddUserChangeMetadataEvent(
            new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
            await Database.Users.Metadata.GetUserMetadata(targetUser.Id) ?? throw new Exception("Metadata not found"),
            await Database.Users.Metadata.GetUserMetadata(targetUser.Id) ?? throw new Exception("Metadata not found"));

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/events");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<EventUsersResponse>();
        Assert.NotNull(content);

        var events = content.Events;

        Assert.NotEmpty(events);
        Assert.Equal(2, content.TotalCount);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task TestGetUserEventsWithPagination()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        for (var i = 0; i < 5; i++)
        {
            await Database.Events.Users.AddUserChangeDescriptionEvent(
                new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
                $"Old description {i}",
                $"New description {i}");
        }

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/events?limit=2&page=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<EventUsersResponse>();
        Assert.NotNull(content);

        var events = content.Events;

        Assert.NotEmpty(events);
        Assert.Equal(5, content.TotalCount);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task TestGetUserEventsWithEventTypeFilter()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var (initialCount, _) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.ChangeDescription)
            });

        var userMetadata = await Database.Users.Metadata.GetUserMetadata(targetUser.Id) ?? throw new Exception("Metadata not found");

        await Database.Events.Users.AddUserChangeDescriptionEvent(
            new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
            "Old description",
            "New description");

        await Database.Events.Users.AddUserChangeDescriptionEvent(
            new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
            "Old description 2",
            "New description 2");

        await Database.Events.Users.AddUserChangeMetadataEvent(
            new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
            userMetadata,
            userMetadata);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/events?types={UserEventType.ChangeDescription}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<EventUsersResponse>();
        Assert.NotNull(content);

        var events = content.Events;

        Assert.NotEmpty(events);
        Assert.True(content.TotalCount >= initialCount + 2, $"Expected at least {initialCount + 2} events, but got {content.TotalCount}");
        Assert.True(events.Count >= initialCount + 2, $"Expected at least {initialCount + 2} events, but got {events.Count}");
        Assert.All(events, e => Assert.Equal(UserEventType.ChangeDescription, e.EventType));

        var descriptionEvents = events.Where(e => e.JsonData.Contains("Old description") || e.JsonData.Contains("Old description 2")).ToList();
        Assert.True(descriptionEvents.Count >= 2, "Expected at least 2 description events we created");
    }

    [Fact]
    public async Task TestGetUserEventsWithQueryFilter()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Create events
        await Database.Events.Users.AddUserChangeDescriptionEvent(
            new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
            "Old description",
            "New description");

        // Act - search by event ID
        var (totalCount, allEvents) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>()
            });

        var eventId = allEvents.First().Id;

        var response = await client.GetAsync($"user/{targetUser.Id}/events?query={eventId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<EventUsersResponse>();
        Assert.NotNull(content);

        var events = content.Events;

        Assert.NotEmpty(events);
        Assert.Equal(1, content.TotalCount);
        Assert.Equal(1, events.Count);
        Assert.Equal(eventId, events.First().Id);
    }

    [Fact]
    public async Task TestGetUserEventsWithMultipleEventTypes()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var (initialDescriptionCount, _) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.ChangeDescription)
            });

        var (initialGameModeCount, _) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.ChangeDefaultGameMode)
            });

        var expectedCount = initialDescriptionCount + initialGameModeCount + 2; // +2 for the events we're about to create

        var userMetadata = await Database.Users.Metadata.GetUserMetadata(targetUser.Id) ?? throw new Exception("Metadata not found");

        await Database.Events.Users.AddUserChangeDescriptionEvent(
            new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
            "Old description",
            "New description");

        await Database.Events.Users.AddUserChangeMetadataEvent(
            new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
            userMetadata,
            userMetadata);

        await Database.Events.Users.AddUserChangeDefaultGameModeEvent(
            new UserEventAction(adminUser, "127.0.0.1", targetUser.Id, targetUser),
            GameMode.Standard,
            GameMode.Taiko);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/events?types={UserEventType.ChangeDescription}&types={UserEventType.ChangeDefaultGameMode}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<EventUsersResponse>();
        Assert.NotNull(content);

        var events = content.Events;

        Assert.NotEmpty(events);
        Assert.True(content.TotalCount >= expectedCount, $"Expected at least {expectedCount} events, but got {content.TotalCount}");
        Assert.True(events.Count >= expectedCount, $"Expected at least {expectedCount} events, but got {events.Count}");
        Assert.Contains(events, e => e.EventType == UserEventType.ChangeDescription);
        Assert.Contains(events, e => e.EventType == UserEventType.ChangeDefaultGameMode);
        Assert.DoesNotContain(events, e => e.EventType == UserEventType.ChangeMetadata);

        var descriptionEvent = events.FirstOrDefault(e => e.EventType == UserEventType.ChangeDescription && e.JsonData.Contains("Old description"));
        var gameModeEvent = events.FirstOrDefault(e => e.EventType == UserEventType.ChangeDefaultGameMode);
        Assert.NotNull(descriptionEvent);
        Assert.NotNull(gameModeEvent);
    }
}