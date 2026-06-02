using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Tests.Extensions;

public static class ScoreExtensions
{
    public static void EnrichWithSessionData(this Score score, Session session, string? storyboardHash = null)
    {
        score.UserId = session.UserId;

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = database.Users.GetUser(id: session.UserId).Result;
        if (user == null)
            throw new NullReferenceException("User not found");

        score.ScoreHash = score.ComputeOnlineHash(user.Username, session.Attributes.UserHash, storyboardHash);
    }

    public static void EnrichWithUserData(this Score score, User user)
    {
        score.UserId = user.Id;
    }

    public static void EnrichWithBeatmapData(this Score score, Beatmap beatmap)
    {
        score.BeatmapHash = beatmap.Checksum ?? throw new Exception("Beatmap checksum is null");
        score.GameMode = (GameMode)beatmap.ModeInt;
        score.BeatmapId = beatmap.Id;
        score.BeatmapStatus = beatmap.Status;
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