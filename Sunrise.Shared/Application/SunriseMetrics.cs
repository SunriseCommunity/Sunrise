using System.Diagnostics.Metrics;
using Hangfire;
using HOPEless.Bancho;
using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Serilog;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Scores;
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

    private static readonly Counter<long> ScoreProcessingPollerRunsCounter = SunriseMeter.CreateCounter<long>(
        "score_processing_poller_runs_total",
        description: "Counts each Hangfire ProcessQueue tick, tagged by outcome (empty, drained, cancelled, error)");

    private static readonly Counter<long> ScoreProcessingEntriesCounter = SunriseMeter.CreateCounter<long>(
        "score_processing_entries_total",
        description: "Counts individual queue-entry outcomes, tagged by outcome (success, permanent_failure, retryable_failure, unexpected)");

    private static readonly ObservableGauge<long> ScoreProcessingQueueDepthPendingGauge = SunriseMeter.CreateObservableGauge(
        "score_processing_queue_depth_pending",
        () => _cachedQueueDepthByStatus?.GetValueOrDefault(ScoreProcessingStatus.Pending, 0) ?? 0,
        "entries",
        "Current number of queue rows in Pending status");

    private static readonly ObservableGauge<long> ScoreProcessingQueueDepthProcessingGauge = SunriseMeter.CreateObservableGauge(
        "score_processing_queue_depth_processing",
        () => _cachedQueueDepthByStatus?.GetValueOrDefault(ScoreProcessingStatus.Processing, 0) ?? 0,
        "entries",
        "Current number of queue rows in Processing status");

    private static readonly ObservableGauge<long> ScoreProcessingQueueDepthFailedGauge = SunriseMeter.CreateObservableGauge(
        "score_processing_queue_depth_failed",
        () => _cachedQueueDepthByStatus?.GetValueOrDefault(ScoreProcessingStatus.Failed, 0) ?? 0,
        "entries",
        "Current number of queue rows in Failed status (exhausted retries)");

    private static readonly ObservableGauge<long> ScoreProcessingLastRunSecondsGauge = SunriseMeter.CreateObservableGauge(
        "score_processing_last_run_age_seconds",
        GetSecondsSinceLastPollerRun,
        "seconds",
        "Seconds since the Hangfire ProcessQueue job last completed; alert if this grows unbounded");

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
    private static Dictionary<ScoreProcessingStatus, long> _cachedQueueDepthByStatus = new();
    private static DateTime? _lastPollerRunCompletedAt;

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
        var queueDepth = await database.ScoreProcessingTasks.CountByStatus();

        _cachedTotalUsers = totalUsers;
        _cachedTotalRestrictedUsers = restrictedUsers;
        _cachedScoresByGameMode = scoresByMode;
        _cachedQueueDepthByStatus = queueDepth;
    }

    public static void ScoreProcessingPollerRunCounterInc(string outcome, int batchCount)
    {
        _lastPollerRunCompletedAt = DateTime.UtcNow;
        ScoreProcessingPollerRunsCounter.Add(1,
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("batch_count", batchCount));
    }

    public static void ScoreProcessingEntryCounterInc(string outcome, ScoreTaskType taskType, ScoreProcessingErrorCode? code = null)
    {
        ScoreProcessingEntriesCounter.Add(1,
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("task_type", taskType.ToString()),
            new KeyValuePair<string, object?>("error_code", code?.ToString() ?? "none"));
    }

    private static long GetSecondsSinceLastPollerRun()
    {
        if (_lastPollerRunCompletedAt == null)
            return -1;

        return (long)(DateTime.UtcNow - _lastPollerRunCompletedAt.Value).TotalSeconds;
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

    [Obsolete("Please use logger instead")]
    public static void RequestReturnedErrorCounterInc(string requestType, int userId, string? errorMessage)
    {
        RequestReturnedErrorCounter.Add(1,
            new KeyValuePair<string, object?>("request_type", requestType),
            new KeyValuePair<string, object?>("user_id", userId.ToString()));

        Log.Error("Request {RequestType} by (user id: {UserId}) returned error: {ErrorMessage}",
            requestType,
            userId.ToString(),
            errorMessage ?? "Not specified");
    }

    public static void ScoreSubmittedCounterInc(int userId, int beatmapId, GameMode gameMode, Mods mods, double pp, int scoreId)
    {
        ScoresSubmittedCounter.Add(1,
            new KeyValuePair<string, object?>("user_id", userId.ToString()),
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