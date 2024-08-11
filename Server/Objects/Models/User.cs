using osu.Shared;
using Watson.ORM.Core;

namespace Sunrise.Server.Objects.Models;

[Table("user")]
public class User
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Username { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Email { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Passhash { get; set; }

    [Column(DataTypes.Int, false)]
    public short Country { get; set; }

    [Column(DataTypes.Int, false)]
    public PlayerRank Privilege { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime RegisterDate { get; set; } = DateTime.UtcNow;

    [Column(DataTypes.Nvarchar, 1024, false)]
    public string Friends { get; set; } = string.Empty;

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
}