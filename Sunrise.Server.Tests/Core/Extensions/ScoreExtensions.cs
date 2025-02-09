using Sunrise.Server.Database.Models;
using Sunrise.Server.Extensions;
using Sunrise.Server.Objects;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Tests.Core.Extensions;

public static class ScoreExtensions
{
    public static void EnrichWithSessionData(this Score score, Session session, string? storyboardHash = null)
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

    public static void ToBestPerformance(this Score score)
    {
        score.CountKatu = 0;
        score.CountGeki = 0;
        score.CountMiss = 0;
        score.Count50 = 0;
        score.Count100 = 0;
        score.Count300 = int.MaxValue;
        score.MaxCombo = int.MaxValue;
    }
}