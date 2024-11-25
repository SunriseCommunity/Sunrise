using Sunrise.Server.Database.Models;

namespace Sunrise.Server.Objects;

public class LocalScore : Score
{
    public int LeaderboardPosition { get; set; }
}