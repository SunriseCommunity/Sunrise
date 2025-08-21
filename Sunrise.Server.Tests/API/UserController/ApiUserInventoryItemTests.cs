using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserInventoryItemTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestUserInventoryItemInvalidItem(string itemType)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/inventory/item?type={itemType}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestApiUserInventoryItemTests()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var quantity = _mocker.GetRandomInteger();

        var setInventoryItemResult = await Database.Users.Inventory.SetInventoryItem(user, ItemType.Hype, quantity);
        if (setInventoryItemResult.IsFailure)
            throw new Exception(setInventoryItemResult.Error);

        // Act
        var response = await client.GetAsync($"user/inventory/item?type={ItemType.Hype}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseItem = await response.Content.ReadFromJsonAsyncWithAppConfig<InventoryItemResponse>();
        Assert.NotNull(responseItem);

        Assert.Equal(ItemType.Hype, responseItem.ItemType);
        Assert.Equal(quantity, responseItem.Quantity);
    }

    [Fact]
    public async Task TestApiUserInventoryItemEmptyTests()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var userInventoryItemResult = await Database.Users.Inventory.SetInventoryItem(user, ItemType.Hype, 0);
        if (userInventoryItemResult.IsFailure)
            throw new Exception(userInventoryItemResult.Error);

        // Act
        var response = await client.GetAsync($"user/inventory/item?type={ItemType.Hype}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseItem = await response.Content.ReadFromJsonAsyncWithAppConfig<InventoryItemResponse>();
        Assert.NotNull(responseItem);

        Assert.Equal(ItemType.Hype, responseItem.ItemType);
        Assert.Equal(0, responseItem.Quantity);
    }

    [Fact]
    public async Task TestApiUserInventoryItemDefaultTests()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/inventory/item?type={ItemType.Hype}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseItem = await response.Content.ReadFromJsonAsyncWithAppConfig<InventoryItemResponse>();
        Assert.NotNull(responseItem);

        Assert.Equal(ItemType.Hype, responseItem.ItemType);
        Assert.Equal(Configuration.UserHypesWeekly, responseItem.Quantity);
    }

    [Fact]
    public async Task TestApiUserInventoryItemIgnoreForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/inventory/item?type={ItemType.Hype}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}