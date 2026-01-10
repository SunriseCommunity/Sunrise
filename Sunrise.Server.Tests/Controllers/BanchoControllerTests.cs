using System.Diagnostics;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Repositories;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using Configuration = Sunrise.Shared.Application.Configuration;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Tests.Controllers;

[Collection("Integration tests collection")]
public class BanchoControllerGetTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
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

[Collection("Integration tests collection")]
public class BanchoControllerPostTests(IntegrationDatabaseFixture fixture) : BanchoTest(fixture)
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

        var authBody = GetUserBodyLoginRequest(loginRequest);

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

        var isLoginSuccessful = new BanchoInt(serverLoginReplyPacket.Data).Value == user.Id;
        Assert.True(isLoginSuccessful, "Login was not successful.");
    }


    [Fact]
    public async Task TestReturnSuccessDoesntTakeTooLongForMultipleActiveSessions()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();
        var authBody = GetUserBodyLoginRequest(_mocker.User.GetUserLoginRequest(user));

        var users = await CreateTestUsers(1000);

        var sessions = Scope.ServiceProvider.GetRequiredService<SessionRepository>();

        foreach (var us in users)
        {
            sessions.CreateSession(us, new Location(), _mocker.User.GetUserLoginRequest(us));
        }

        // TODO: Forces to create users ranks (which doesn't happen on user creation above). Creating user ranks seems to be a biggest bottleneck here.
        await Database.FlushAndUpdateRedisCache(false);

        var timer = Stopwatch.StartNew();

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        timer.Stop();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        var isLoginSuccessful = new BanchoInt(serverLoginReplyPacket.Data).Value == user.Id;
        Assert.True(isLoginSuccessful, "Login was not successful.");

        Assert.True(timer.ElapsedMilliseconds < 1500, "Login took too long, possible performance issue with multiple active sessions.");
    }


    [Fact]
    public async Task TestReturnExpectedLoginPacketsIfLoginRequestValid()
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

        var expectedPacketsWithData = new List<(PacketType ServerLoginReply, object?)>
        {
            (PacketType.ServerLoginReply, new BanchoInt(user.Id)),
            (PacketType.ServerBanchoVersion, new BanchoInt(19)),
            (PacketType.ServerUserPermissions, new BanchoInt((int)(PlayerRank.Default | PlayerRank.Supporter))),
            (PacketType.ServerChatChannelListingComplete, new BanchoInt(0)),
            (PacketType.ServerChatChannelJoinSuccess, new BanchoString("#osu")),
            (PacketType.ServerChatChannelJoinSuccess, new BanchoString("#announce")),
            (PacketType.ServerChatChannelAvailable, new BanchoChatChannel
            {
                Name = "#osu",
                Topic = "General chat channel.",
                UserCount = 1
            }),
            (PacketType.ServerChatChannelAvailable, new BanchoChatChannel
            {
                Name = "#announce",
                Topic = "Announcement chat channel.",
                UserCount = 1
            }),
            // We could have more chat channels, but it's not critical to test them all here
            (PacketType.ServerFriendsList, new BanchoIntList()),
            (PacketType.ServerUserData, null),
            (PacketType.ServerUserPresence, null),
            (PacketType.ServerNotification, new BanchoString(Configuration.WelcomeMessage))
        };


        var expectedPacketsWithDataToNotExist = new List<(PacketType ServerLoginReply, object)>
        {
            (PacketType.ServerChatChannelJoinSuccess, new BanchoString("#staff"))
        };


        foreach (var (expectedPacketType, expectedData) in expectedPacketsWithData)
        {
            var packet = TakePacketWithSpecificData(responsePackets, expectedPacketType, expectedData);

            Assert.NotNull(packet);
        }

        foreach (var (expectedPacketType, expectedData) in expectedPacketsWithDataToNotExist)
        {
            var packet = TakePacketWithSpecificData(responsePackets, expectedPacketType, expectedData);

            Assert.Null(packet);
        }
    }

    [Fact]
    public async Task TestReturnFriendsListIncludeUserFriends()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();
        var authBody = GetUserBodyLoginRequest(_mocker.User.GetUserLoginRequest(user));


        var friendUser = _mocker.User.GetRandomUser();
        await CreateTestUser(friendUser);

        var relationship = await Database.Users.Relationship.GetUserRelationship(user.Id, friendUser.Id);
        if (relationship == null)
            throw new Exception("Failed to create user relationship for test.");

        relationship.Relation = UserRelation.Friend;
        await Database.Users.Relationship.UpdateUserRelationship(relationship);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerFriendsList);
        Assert.NotNull(serverLoginReplyPacket);

        var packetFriendsList = new BanchoIntList(serverLoginReplyPacket.Data).Value;

        Assert.Contains(friendUser.Id, packetFriendsList);
    }

    [Fact]
    public async Task TestReturnActiveUsersDataAndPresence()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();
        var authBody = GetUserBodyLoginRequest(_mocker.User.GetUserLoginRequest(user));

        var otherPlayerUser = await CreateTestUser();

        var sessions = Scope.ServiceProvider.GetRequiredService<SessionRepository>();
        sessions.CreateSession(otherPlayerUser, new Location(), _mocker.User.GetUserLoginRequest(otherPlayerUser));

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);

        var serverOtherPlayerUserDataPacket = responsePackets.FirstOrDefault(x =>
        {
            if (x.Type != PacketType.ServerUserData)
                return false;

            var packetData = new BanchoUserData(x.Data);
            return packetData.UserId == otherPlayerUser.Id;
        });

        Assert.NotNull(serverOtherPlayerUserDataPacket);

        var responseData = new BanchoUserData(serverOtherPlayerUserDataPacket.Data);
        Assert.Equal(otherPlayerUser.Id, responseData.UserId);

        var serverOtherPlayerPresencePacket = responsePackets.FirstOrDefault(x =>
        {
            if (x.Type != PacketType.ServerUserPresence)
                return false;

            var packetData = new BanchoUserPresence(x.Data);
            return packetData.UserId == otherPlayerUser.Id;
        });

        Assert.NotNull(serverOtherPlayerPresencePacket);

        var presenceData = new BanchoUserPresence(serverOtherPlayerPresencePacket.Data);
        Assert.Equal(otherPlayerUser.Id, presenceData.UserId);
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


    private BanchoPacket? TakePacketWithSpecificData(List<BanchoPacket> packets, PacketType expectedPacketType, object? expectedData)
    {
        var packetWithSameHeaderAndData = packets
            .Where(x => x.Type == expectedPacketType)
            .Select(x =>
                {
                    if (expectedData != null)
                        return (x, Activator.CreateInstance(expectedData.GetType(), x.Data));

                    return (x, null);
                }
            )
            .FirstOrDefault(x =>
            {
                var (packet, packetData) = x;

                if (expectedData == null)
                {
                    packets.Remove(packet);
                    return true;
                }

                try
                {
                    Assert.Equivalent(packetData, expectedData);
                    packets.Remove(packet);
                    return true;
                }
                catch
                {
                }

                return false;
            });

        return packetWithSameHeaderAndData.x;
    }
}