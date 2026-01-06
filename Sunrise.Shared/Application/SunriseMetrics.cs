using System.Diagnostics.Metrics;
using Hangfire;
using HOPEless.Bancho;
using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Serilog;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Repositories.Multiplayer;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Application;

public class SunriseMetrics
{
    private static readonly Meter SunriseMeter = new("Sunrise");

    private static readonly Counter<long> PacketHandlingCounter = SunriseMeter.CreateCounter<long>(
        "bancho_packets_handled_total",
        description: "Counts the total number of handled packets");

    private static readonly Counter<long> ExternalApiRequestsCounter = SunriseMeter.CreateCounter<long>(
        "external_api_requests_total",
        description: "Counts the total number of external API requests");

    private static readonly Counter<long> RequestReturnedErrorCounter = SunriseMeter.CreateCounter<long>(
        "request_returned_error_total",
        description: "Counts the total number of requests that returned an unexpected error");

    private static readonly Counter<long> ScoresSubmittedCounter = SunriseMeter.CreateCounter<long>(
        "scores_submitted_total",
        description: "Counts the total number of successfully submitted scores");

    private static readonly ObservableGauge<int> CurrentMatchesGauge = SunriseMeter.CreateObservableGauge(
        "current_matches_count",
        GetCurrentMatchesCount,
        "matches",
        "Current number of active multiplayer matches");

    private static readonly ObservableGauge<int> TotalSpectatorsGauge = SunriseMeter.CreateObservableGauge(
        "total_spectators_count",
        GetTotalSpectatorsCount,
        "spectators",
        "Current total number of spectators across all sessions");

    private static readonly ObservableGauge<int> TotalGameSessionsGauge = SunriseMeter.CreateObservableGauge(
        "total_game_sessions_count",
        GetTotalGameSessionsCount,
        "sessions",
        "Current total number of active game sessions (users online)");

    private static readonly ObservableGauge<int> TotalUsersGauge = SunriseMeter.CreateObservableGauge(
        "total_users_count",
        () => _cachedTotalUsers,
        "users",
        "Total number of registered users");

    private static readonly ObservableGauge<int> TotalRestrictedUsersGauge = SunriseMeter.CreateObservableGauge(
        "total_restricted_users_count",
        () => _cachedTotalRestrictedUsers,
        "users",
        "Total number of restricted users");

    private static readonly ObservableGauge<long> TotalScoresGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_count",
        () => _cachedScoresByGameMode?.Values.Sum() ?? 0,
        "scores",
        "Total number of scores across all game modes");

    // Per-game-mode score gauges
    private static readonly ObservableGauge<long> TotalScoresStandardGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_standard",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.Standard, 0) ?? 0,
        "scores",
        "Total number of scores in Standard mode");

    private static readonly ObservableGauge<long> TotalScoresTaikoGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_taiko",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.Taiko, 0) ?? 0,
        "scores",
        "Total number of scores in Taiko mode");

    private static readonly ObservableGauge<long> TotalScoresCatchGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_catch",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.CatchTheBeat, 0) ?? 0,
        "scores",
        "Total number of scores in Catch the Beat mode");

    private static readonly ObservableGauge<long> TotalScoresManiaGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_mania",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.Mania, 0) ?? 0,
        "scores",
        "Total number of scores in Mania mode");

    private static readonly ObservableGauge<long> TotalScoresRelaxStandardGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_relax_standard",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.RelaxStandard, 0) ?? 0,
        "scores",
        "Total number of scores in Relax Standard mode");

    private static readonly ObservableGauge<long> TotalScoresRelaxTaikoGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_relax_taiko",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.RelaxTaiko, 0) ?? 0,
        "scores",
        "Total number of scores in Relax Taiko mode");

    private static readonly ObservableGauge<long> TotalScoresRelaxCatchGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_relax_catch",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.RelaxCatchTheBeat, 0) ?? 0,
        "scores",
        "Total number of scores in Relax Catch the Beat mode");

    private static readonly ObservableGauge<long> TotalScoresAutopilotStandardGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_autopilot_standard",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.AutopilotStandard, 0) ?? 0,
        "scores",
        "Total number of scores in Autopilot Standard mode");

    private static readonly ObservableGauge<long> TotalScoresScoreV2StandardGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_v2_standard",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.ScoreV2Standard, 0) ?? 0,
        "scores",
        "Total number of scores in V2 Standard mode");

    private static readonly ObservableGauge<long> TotalScoresScoreV2TaikoGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_v2_taiko",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.ScoreV2Taiko, 0) ?? 0,
        "scores",
        "Total number of scores in V2 Taiko mode");

    private static readonly ObservableGauge<long> TotalScoresScoreV2CatchGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_v2_catch",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.ScoreV2CatchTheBeat, 0) ?? 0,
        "scores",
        "Total number of scores in V2 Catch the Beat mode");

    private static readonly ObservableGauge<long> TotalScoresScoreV2ManiaGauge = SunriseMeter.CreateObservableGauge(
        "total_scores_v2_mania",
        () => _cachedScoresByGameMode?.GetValueOrDefault(GameMode.ScoreV2Mania, 0) ?? 0,
        "scores",
        "Total number of scores in V2 Mania mode");

    private static int _cachedTotalUsers;
    private static int _cachedTotalRestrictedUsers;
    private static Dictionary<GameMode, long> _cachedScoresByGameMode = new();

    public SunriseMetrics()
    {
        _ = RefreshDatabaseMetricsAsync();

        RecurringJob.AddOrUpdate("Fetch database metrics", () => RefreshDatabaseMetricsAsync(), "*/1 * * * *");
    }


    public static async Task RefreshDatabaseMetricsAsync()
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var totalUsers = await database.Users.CountUsers();
        var restrictedUsers = await database.Users.CountRestrictedUsers();
        var scoresByMode = await database.Scores.CountScoresByGameMode();

        _cachedTotalUsers = totalUsers;
        _cachedTotalRestrictedUsers = restrictedUsers;
        _cachedScoresByGameMode = scoresByMode;
    }

    public static void PacketHandlingCounterInc(BanchoPacket packet, Session session)
    {
        PacketHandlingCounter.Add(1,
            new KeyValuePair<string, object?>("packet_type", packet.Type.ToString()),
            new KeyValuePair<string, object?>("user_id", session.UserId.ToString()));
    }

    public static void ExternalApiRequestsCounterInc(ApiType type, ApiServer server, BaseSession session)
    {
        ExternalApiRequestsCounter.Add(1,
            new KeyValuePair<string, object?>("api_type", type.ToString()),
            new KeyValuePair<string, object?>("api_server", server.ToString()),
            new KeyValuePair<string, object?>("user_id", session.UserId.ToString()));
    }

    public static void RequestReturnedErrorCounterInc(string requestType, Session? session, string? errorMessage)
    {
        RequestReturnedErrorCounter.Add(1,
            new KeyValuePair<string, object?>("request_type", requestType),
            new KeyValuePair<string, object?>("user_id", session?.UserId.ToString() ?? "-1"));

        Log.Error("Request {RequestType} by (user id: {UserId}) returned error: {ErrorMessage}",
            requestType,
            session?.UserId.ToString() ?? "-1",
            errorMessage ?? "Not specified");
    }

    public static void ScoreSubmittedCounterInc(Session session, int beatmapId, GameMode gameMode, Mods mods, double pp,  int scoreId)
    {
        ScoresSubmittedCounter.Add(1,
            new KeyValuePair<string, object?>("user_id", session.UserId.ToString()),
            new KeyValuePair<string, object?>("beatmap_id", beatmapId.ToString()),
            new KeyValuePair<string, object?>("game_mode", gameMode.ToString()),
            new KeyValuePair<string, object?>("mods", mods.ToString()),
            new KeyValuePair<string, object?>("performance_points", pp),
            new KeyValuePair<string, object?>("score_id", scoreId.ToString()));
    }

    private static int GetCurrentMatchesCount()
    {
        var matchRepository = ServicesProviderHolder.GetRequiredService<MatchRepository>();
        return matchRepository.GetMatchCount();
    }

    private static int GetTotalSpectatorsCount()
    {
        var sessionRepository = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        return sessionRepository.GetTotalSpectatorsCount();
    }

    private static int GetTotalGameSessionsCount()
    {
        var sessionRepository = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        return sessionRepository.GetSessionCount();
    }
}