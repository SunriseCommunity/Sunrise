using HOPEless.Bancho.Objects;
using Sunrise.Shared.Types.Enums;

namespace Sunrise.Shared.Types.Interfaces;

public interface IUserAttributes
{
    string OsuVersion { get; }
    string UserHash { get; }
    DateTime LastPingRequest { get; }
    BanchoUserStatus Status { get; set; }
    int Timezone { get; }
    bool ShowUserLocation { get; set; }
    bool IgnoreNonFriendPm { get; set; }
    string? AwayMessage { get; set; }
    bool IsBot { get; set; }
    bool UsesOsuClient { get; set; }

    Task<BanchoUserPresence> GetPlayerPresence();

    Task<BanchoUserData> GetPlayerData();

    void UpdateLastPing();

    GameMode GetCurrentGameMode();

    BanchoUserStatus GetPlayerStatus();
}