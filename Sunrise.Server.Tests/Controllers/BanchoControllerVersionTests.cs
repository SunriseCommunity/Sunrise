using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using Configuration = Sunrise.Shared.Application.Configuration;

namespace Sunrise.Server.Tests.Controllers;

[Collection("Integration tests collection")]
public class BanchoControllerVersionTests(IntegrationDatabaseFixture fixture) : BanchoTest(fixture)
{
    private readonly MockService _mocker = new();

    private LoginRequest GetLoginRequestWithVersion(Shared.Database.Models.Users.User user, string version)
    {
        return new LoginRequest(user.Username, user.Passhash, version, 0, true, "", false);
    }

    private void MockChangelogResponse(string stableVersion, string? cuttingEdgeVersion = null)
    {
        App.MockHttpClient?.MockResponse<ChangelogResponse>(ApiType.GetOsuChangelog,
            _ => new ChangelogResponse
            {
                Streams =
                [
                    new ChangelogStream
                    {
                        Name = "stable40",
                        LatestBuild = new ChangelogBuild { Version = stableVersion }
                    },
                    new ChangelogStream
                    {
                        Name = "cuttingedge",
                        LatestBuild = new ChangelogBuild { Version = cuttingEdgeVersion ?? stableVersion }
                    }
                ]
            });
    }

    private void MockChangelogFailure()
    {
        App.MockHttpClient?.MockResponse<ChangelogResponse>(ApiType.GetOsuChangelog,
            _ => throw new Exception("Simulated API failure"));
    }

    [Fact]
    public async Task TestLoginRejectedWhenClientOutdated()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        Configuration.EnforceLatestClientVersion = true;

        MockChangelogResponse("20260301.1");

        var user = await CreateTestUser();
        var loginRequest = GetLoginRequestWithVersion(user, "b20250101.1");
        var authBody = GetUserBodyLoginRequest(loginRequest);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        Assert.Equal((int)LoginResponse.OutdatedClient, new BanchoInt(serverLoginReplyPacket.Data).Value);

        Configuration.EnforceLatestClientVersion = false;
    }

    [Fact]
    public async Task TestLoginAllowedWhenClientIsCurrent()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        Configuration.EnforceLatestClientVersion = true;

        MockChangelogResponse("20260101.1");

        var user = await CreateTestUser();
        var loginRequest = GetLoginRequestWithVersion(user, "b20260101.1");
        var authBody = GetUserBodyLoginRequest(loginRequest);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        Assert.Equal(user.Id, new BanchoInt(serverLoginReplyPacket.Data).Value);

        Configuration.EnforceLatestClientVersion = false;
    }

    [Fact]
    public async Task TestLoginAllowedWhenFetchFails()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        Configuration.EnforceLatestClientVersion = true;

        MockChangelogFailure();

        var user = await CreateTestUser();
        var loginRequest = GetLoginRequestWithVersion(user, "b20260101.1");
        var authBody = GetUserBodyLoginRequest(loginRequest);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        Assert.Equal(user.Id, new BanchoInt(serverLoginReplyPacket.Data).Value);

        Configuration.EnforceLatestClientVersion = false;
    }

    [Fact]
    public async Task TestLoginAllowedWhenEnforcementDisabled()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        Configuration.EnforceLatestClientVersion = false;

        MockChangelogResponse("20260301.1");

        var user = await CreateTestUser();
        var loginRequest = GetLoginRequestWithVersion(user, "b20250101.1");
        var authBody = GetUserBodyLoginRequest(loginRequest);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        Assert.Equal(user.Id, new BanchoInt(serverLoginReplyPacket.Data).Value);
    }

    [Fact]
    public async Task TestLoginAllowedWhenClientIsNewer()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        Configuration.EnforceLatestClientVersion = true;

        MockChangelogResponse("20250101.1");

        var user = await CreateTestUser();
        var loginRequest = GetLoginRequestWithVersion(user, "b20260101.1");
        var authBody = GetUserBodyLoginRequest(loginRequest);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        Assert.Equal(user.Id, new BanchoInt(serverLoginReplyPacket.Data).Value);

        Configuration.EnforceLatestClientVersion = false;
    }

    [Fact]
    public async Task TestLoginRejectedWhenCuttingEdgeClientOutdated()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        Configuration.EnforceLatestClientVersion = true;

        MockChangelogResponse("20260301.1", "20260301.1");

        var user = await CreateTestUser();
        var loginRequest = GetLoginRequestWithVersion(user, "b20240826.2cuttingedge");
        var authBody = GetUserBodyLoginRequest(loginRequest);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        Assert.Equal((int)LoginResponse.OutdatedClient, new BanchoInt(serverLoginReplyPacket.Data).Value);

        Configuration.EnforceLatestClientVersion = false;
    }

    [Fact]
    public async Task TestLoginAllowedWhenCuttingEdgeClientIsCurrent()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        Configuration.EnforceLatestClientVersion = true;

        MockChangelogResponse("20260301.1", "20240826.2");

        var user = await CreateTestUser();
        var loginRequest = GetLoginRequestWithVersion(user, "b20240826.2cuttingedge");
        var authBody = GetUserBodyLoginRequest(loginRequest);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        Assert.Equal(user.Id, new BanchoInt(serverLoginReplyPacket.Data).Value);

        Configuration.EnforceLatestClientVersion = false;
    }

    [Fact]
    public async Task TestLoginRejectedWhenClientWithoutRevisionIsOutdated()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        Configuration.EnforceLatestClientVersion = true;

        MockChangelogResponse("20260301.1");

        var user = await CreateTestUser();
        var loginRequest = GetLoginRequestWithVersion(user, "b20241001");
        var authBody = GetUserBodyLoginRequest(loginRequest);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        Assert.Equal((int)LoginResponse.OutdatedClient, new BanchoInt(serverLoginReplyPacket.Data).Value);

        Configuration.EnforceLatestClientVersion = false;
    }

    [Fact]
    public async Task TestLoginAllowedWhenClientWithoutRevisionIsCurrent()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");
        Configuration.EnforceLatestClientVersion = true;

        MockChangelogResponse("20241001.0");

        var user = await CreateTestUser();
        var loginRequest = GetLoginRequestWithVersion(user, "b20241001");
        var authBody = GetUserBodyLoginRequest(loginRequest);

        // Act
        var response = await client.PostAsync("/", new StringContent(authBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var serverLoginReplyPacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerLoginReply);
        Assert.NotNull(serverLoginReplyPacket);

        Assert.Equal(user.Id, new BanchoInt(serverLoginReplyPacket.Data).Value);

        Configuration.EnforceLatestClientVersion = false;
    }
}
