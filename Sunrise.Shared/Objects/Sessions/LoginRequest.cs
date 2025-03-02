namespace Sunrise.Shared.Objects.Sessions;

public class LoginRequest(string username, string passHash, string version, short utcOffset, bool showCityLocation, string clientHash, bool blockNonFriendPm)
{
    public string Username { get; set; } = username;
    public string PassHash { get; set; } = passHash;
    public string Version { get; set; } = version;
    public short UtcOffset { get; set; } = utcOffset;
    public bool ShowCityLocation { get; set; } = showCityLocation;
    public string ClientHash { get; set; } = clientHash;
    public bool BlockNonFriendPm { get; set; } = blockNonFriendPm;
}