namespace Sunrise.Shared.Types.Interfaces;

public interface ILoginRequest
{
    string Username { get; set; }
    string PassHash { get; set; }
    string Version { get; set; }
    short UtcOffset { get; set; }
    bool ShowCityLocation { get; set; }
    string ClientHash { get; set; }
    bool BlockNonFriendPm { get; set; }
}