using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using PlayerRank = osu.Shared.PlayerRank;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user")]
[Index(nameof(Username), IsUnique = true)]
[Index(nameof(Email), IsUnique = true)]
[Index(nameof(AccountStatus))]
public class User
{
    public int Id { get; set; }

    public string Username { get; set; }

    public string Email { get; set; }
    public string Passhash { get; set; }
    public string? Description { get; set; }
    public CountryCode Country { get; set; }
    public UserPrivilege Privilege { get; set; } = UserPrivilege.User;
    public DateTime RegisterDate { get; set; } = DateTime.UtcNow;
    public DateTime LastOnlineTime { get; set; } = DateTime.UtcNow;
    public UserAccountStatus AccountStatus { get; set; } = UserAccountStatus.Active;
    public DateTime SilencedUntil { get; set; } = DateTime.MinValue;
    public GameMode DefaultGameMode { get; set; } = GameMode.Standard;

    public ICollection<UserFile> UserFiles { get; set; } = new List<UserFile>();
    
    public ICollection<UserInventoryItem> Inventory { get; set; } = new List<UserInventoryItem>();

    public ICollection<UserRelationship> UserInitiatedRelationships { get; set; } = new List<UserRelationship>();
    public ICollection<UserRelationship> UserReceivedRelationships { get; set; } = new List<UserRelationship>();

    public ICollection<UserStats> UserStats { get; set; } = new List<UserStats>();

    public ICollection<UserStatsSnapshot> UserStatsSnapshots { get; set; } = new List<UserStatsSnapshot>();

    [NotMapped]
    public UserFile? AvatarRecord => UserFiles.FirstOrDefault(f => f.Type == FileType.Avatar);

    [NotMapped]
    public string AvatarUrl => $"https://a.{Configuration.Domain}/avatar/{Id}{(AvatarRecord != null ? $"?{new DateTimeOffset(AvatarRecord.UpdatedAt).ToUnixTimeMilliseconds()}" : "")}";

    [NotMapped]
    public UserFile? BannerRecord => UserFiles.FirstOrDefault(f => f.Type == FileType.Banner);

    [NotMapped]
    public string BannerUrl => $"https://a.{Configuration.Domain}/banner/{Id}{(BannerRecord != null ? $"?{new DateTimeOffset(BannerRecord.UpdatedAt).ToUnixTimeMilliseconds()}" : "")}";
    
    public PlayerRank GetPrivilegeRank()
    {
        var privilegeRank = PlayerRank.Default;

        if (Privilege.HasFlag(UserPrivilege.Developer)) privilegeRank |= PlayerRank.SuperMod;

        if (Privilege.HasFlag(UserPrivilege.Admin) || Privilege.HasFlag(UserPrivilege.Bat))
            privilegeRank |= PlayerRank.Bat;

        if (Privilege.HasFlag(UserPrivilege.Supporter)) privilegeRank |= PlayerRank.Supporter;

        return privilegeRank;
    }

    public bool IsRestricted()
    {
        return AccountStatus == UserAccountStatus.Restricted;
    }

    public bool IsActive(bool ignoreRestriction = true)
    {
        return AccountStatus == UserAccountStatus.Active || IsRestricted() && ignoreRestriction;
    }

    public bool IsUserSunriseBot()
    {
        return (Privilege & UserPrivilege.ServerBot) != 0;
    }
}