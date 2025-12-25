using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using Sunrise.Tests;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiUserEditDefaultGameModeTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Select(mode => new object[]
        {
            mode
        });
    }

    [Fact]
    public async Task TestEditDefaultGameModeWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("user/edit/default-gamemode",
            new EditDefaultGameModeRequest
            {
                DefaultGameMode = _mocker.Score.GetRandomGameMode()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestEditDefaultGameModeWithActiveRestriction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var result = await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");
        if (result.IsFailure)
            throw new Exception(result.Error);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/default-gamemode",
            new EditDefaultGameModeRequest
            {
                DefaultGameMode = _mocker.Score.GetRandomGameMode()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestEditDefaultGameModeWithoutBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/default-gamemode", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestEditDefaultGameModeWithInvalidBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var newGameMode = _mocker.GetRandomString();


        var json = $"{{\"default_gamemode\":\"{newGameMode}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("user/edit/default-gamemode", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestEditDefaultGameMode(GameMode mode)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var oldGameMode = user.DefaultGameMode;

        // Act
        var response = await client.PostAsJsonAsync("user/edit/default-gamemode",
            new EditDefaultGameModeRequest
            {
                DefaultGameMode = mode
            });

        // Assert
        response.EnsureSuccessStatusCode();

        var updatedUser = await Database.Users.GetUser(user.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(mode, updatedUser.DefaultGameMode);

        var (totalCount, events) = await Database.Events.Users.GetUserEvents(user.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.ChangeDefaultGameMode)
            });

        Assert.Equal(1, totalCount);
        var gameModeChangeEvent = events.First();
        Assert.Equal(UserEventType.ChangeDefaultGameMode, gameModeChangeEvent.EventType);
        Assert.Equal(user.Id, gameModeChangeEvent.UserId);

        var actualData = gameModeChangeEvent.GetData<JsonElement>();

        Assert.Equal((int)oldGameMode, actualData.GetProperty("OldGameMode").GetInt32());
        Assert.Equal((int)mode, actualData.GetProperty("NewGameMode").GetInt32());
        Assert.Equal(user.Id, actualData.GetProperty("UpdatedById").GetInt32());
    }
}
