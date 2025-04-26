using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;

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
    public short Country { get; set; }
    public UserPrivilege Privilege { get; set; } = UserPrivilege.User;
    public DateTime RegisterDate { get; set; } = DateTime.UtcNow;
    public DateTime LastOnlineTime { get; set; } = DateTime.UtcNow;
    public string Friends { get; set; } = string.Empty;
    public UserAccountStatus AccountStatus { get; set; } = UserAccountStatus.Active;
    public DateTime SilencedUntil { get; set; } = DateTime.MinValue;

    public ICollection<UserFile> UserFiles { get; set; } = new List<UserFile>();

    [NotMapped]
    public UserFile? AvatarRecord => UserFiles.FirstOrDefault(f => f.Type == FileType.Avatar);

    [NotMapped]
    public string AvatarUrl => $"https://a.{Configuration.Domain}/avatar/{Id}{(AvatarRecord != null ? $"?{new DateTimeOffset(AvatarRecord.UpdatedAt).ToUnixTimeMilliseconds()}" : "")}";

    [NotMapped]
    public UserFile? BannerRecord => UserFiles.FirstOrDefault(f => f.Type == FileType.Banner);

    [NotMapped]
    public string BannerUrl => $"https://a.{Configuration.Domain}/banner/{Id}{(BannerRecord != null ? $"?{new DateTimeOffset(BannerRecord.UpdatedAt).ToUnixTimeMilliseconds()}" : "")}";

    [NotMapped]
    public List<int> FriendsList => Friends.Split(',')
        .Where(x => !string.IsNullOrEmpty(x))
        .Select(int.Parse)
        .ToList();

    public void AddFriend(int friendId)
    {
        if (FriendsList.Contains(friendId))
            return;

        Friends += $",{friendId}";
    }

    public void RemoveFriend(int friendId)
    {
        if (!FriendsList.Contains(friendId))
            return;

        Friends = string.Join(',', FriendsList.Where(x => x != friendId));
    }

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