using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Extensions;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;

using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Tests.Core.Extensions;

public static class ScoreExtensions
{
    public static void EnrichWithSessionData(this Score score, Session session,string? storyboardHash = null)
    {
        score.UserId = session.User.Id;
        score.ScoreHash = score.ComputeOnlineHash(session.User.Username, session.Attributes.UserHash, storyboardHash);
    }
    
    public static void Normalize(this Score score)
    {
        score.Accuracy = Math.Clamp(score.Accuracy, 0, 100);
        score.Mods = score.GameMode.GetGamemodeMods();
    }
    
    public static void ToVanillaScore(this Score score)
    {
        score.Mods &= ~score.GameMode.GetGamemodeMods();
        score.GameMode = (GameMode)score.GameMode.ToVanillaGameMode();
    }
    
}