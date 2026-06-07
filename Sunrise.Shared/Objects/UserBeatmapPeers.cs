namespace Sunrise.Shared.Objects;

public sealed record UserBeatmapPeers(
    UserPersonalBestScores? SameModsPeer,
    UserPersonalBestScores? OverallPeer);