using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Database;
using Sunrise.Database.Schemas;
using Sunrise.GameClient.Objects.Serializable;

namespace Sunrise.GameClient.Objects;

public class UserAttributes
{
    private int Timezone { get; set; }
    private float Longitude { get; set; }
    private float Latitude { get; set; }
    public BanchoUserStatus Status { get; set; }
    private User _user { get; set; }

    public UserAttributes(User user, Location location)
    {
        var numbers = location.Loc.Split(',');
        Longitude = float.Parse(numbers[1]);
        Latitude = float.Parse(numbers[0]);
        Timezone = location.TimeOffset;
        _user = user;
        Status = new BanchoUserStatus();
    }

    public BanchoUserPresence GetPlayerPresence()
    {
        return new BanchoUserPresence()
        {
            UserId = _user.Id,
            Username = _user.Username,
            Timezone = Timezone,
            Latitude = Latitude,
            Longitude = Longitude,
            CountryCode = byte.Parse(_user.Country.ToString()),
            Permissions = _user.Privilege,
            Rank = _user.Id, // todo: Add actual rank;
            PlayMode = GameMode.Standard, // todo: Get from request?
            UsesOsuClient = true
        };
    }

    public BanchoUserData GetPlayerData()
    {
        return new BanchoUserData()
        {
            UserId = _user.Id,
            Status = Status,
            Rank = _user.Id, // todo: Add actual rank;
            Performance = _user.PerformancePoints,
            Accuracy = _user.Accuracy / 100, // Bancho counts accuracy from 0 to 1
            Playcount = _user.PlayCount,
            RankedScore = _user.RankedScore,
            TotalScore = _user.TotalScore,
        };
    }

    public BanchoUserStatus GetPlayerStatus()
    {
        return Status;
    }
}