using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.Controllers;

public class BanchoControllerGetTests : DatabaseTest
{
    [Fact]
    public async Task TestOnGetRequestReturnsImage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }
}

public class BanchoControllerPostTests : BanchoTest
{
    
    private readonly MockService _mocker = new();
    
    [Fact]
    public async Task TestOnInvalidPasshashRejectLogin()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();

        var loginRequest = _mocker.User.GetUserLoginRequest(user);
        loginRequest.PassHash = "invalid_passhash";
        
        var authBody = GetUserBodyLoginRequest( loginRequest);
        
        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);
        
        Assert.Equal((int)LoginResponse.InvalidCredentials, new BanchoInt(serverLoginReplyPacket.Data).Value);
    }
    
    [Fact]
    public async Task TestReturnSuccessIfLoginRequestValid()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        
        var user = await CreateTestUser();
        var authBody = GetUserBodyLoginRequest(_mocker.User.GetUserLoginRequest(user));
        
        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);
        
        // We expect the server to return the user ID on successful login
        Assert.Equal(user.Id, new BanchoInt(serverLoginReplyPacket.Data).Value);
    }
    
    [Fact]
    public async Task TestReturnUserDataWithoutPerformanceOverflowIfLoginRequestValid()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        
        var user = await CreateTestUser();
        var authBody = GetUserBodyLoginRequest(_mocker.User.GetUserLoginRequest(user));

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        userStats.PerformancePoints = _mocker.GetRandomDouble(false);
        
        await Database.Users.Stats.UpdateUserStats(userStats, user);
        
        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responsePackets = await GetResponsePackets(response);
        var serverUserDataPacket = responsePackets.FirstOrDefault(x =>
        {
            if (x.Type != PacketType.ServerUserData)
                return false;
            
            var packetData = new BanchoUserData(x.Data);
            return packetData.UserId == user.Id;
        });
        
        Assert.NotNull(serverUserDataPacket);
        
        var responseData = new BanchoUserData(serverUserDataPacket.Data);
        Assert.NotNull(responseData);
        
        Assert.Equal(user.Id, responseData.UserId);
        Assert.Equal((short)userStats.PerformancePoints, responseData.Performance);
    }
    
    [Fact]
    public async Task TestReturnUserDataWithPerformanceOverflowIfLoginRequestValid()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        
        var user = await CreateTestUser();
        var authBody = GetUserBodyLoginRequest(_mocker.User.GetUserLoginRequest(user));

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        userStats.PerformancePoints = _mocker.GetRandomDouble(false) + ushort.MaxValue;
        
        await Database.Users.Stats.UpdateUserStats(userStats, user);
        
        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responsePackets = await GetResponsePackets(response);
        var serverUserDataPacket = responsePackets.FirstOrDefault(x =>
        {
            if (x.Type != PacketType.ServerUserData)
                return false;
            
            var packetData = new BanchoUserData(x.Data);
            return packetData.UserId == user.Id;
        });
        
        Assert.NotNull(serverUserDataPacket);
        
        var responseData = new BanchoUserData(serverUserDataPacket.Data);
        Assert.NotNull(responseData);
        
        Assert.Equal(user.Id, responseData.UserId);
        Assert.Equal(0, responseData.Performance);
        Assert.Equal((long)userStats.PerformancePoints, responseData.RankedScore);
    }
}