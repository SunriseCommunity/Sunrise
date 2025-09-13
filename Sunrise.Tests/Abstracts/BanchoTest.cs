using HOPEless.Bancho;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Tests.Abstracts;

public abstract class BanchoTest(bool useRedis = false) : DatabaseTest(useRedis)
{
    protected readonly PacketHelper PacketHelper = new();

    protected string GetUserBodyLoginRequest(LoginRequest loginRequest)
    {
        string[] strings =
        [
            loginRequest.Username,
            loginRequest.PassHash,
            $"{loginRequest.Version}|{loginRequest.UtcOffset}|{(loginRequest.ShowCityLocation ? "1" : "0")}|{loginRequest.ClientHash}|{(loginRequest.BlockNonFriendPm ? "1" : "0")}"
        ];

        return string.Join('\n', strings);
    }

    protected async Task<List<BanchoPacket>> GetResponsePackets(HttpResponseMessage response)
    {
        await using var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer);
        buffer.Seek(0, SeekOrigin.Begin);

        return BanchoSerializer.DeserializePackets(buffer).ToList();
    }

    protected async Task<string> GetUserAuthOsuToken(HttpClient client, User user, LoginRequest? loginRequest = null)
    {
        var authBody = GetUserBodyLoginRequest(loginRequest ?? new LoginRequest(user.Username, user.Passhash, "version", 0, true, "", false));
        var loginResponse = await client.PostAsync("/", new StringContent(authBody));

        loginResponse.EnsureSuccessStatusCode();

        return loginResponse.Headers.GetValues("cho-token").FirstOrDefault() ?? throw new Exception("Auth token not found");
    }
}