using System.Diagnostics;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.API.Extensions;

public static class ActivityExtensions
{
    public static void SetActivitySessionTags(this Activity activity, BaseSession session)
    {
        activity.SetTag("session.user_id", session.UserId);
        activity.SetTag("session.ip_address", session.IpAddress);
        activity.SetTag("session.is_guest", session.IsGuest);
        activity.SetTag("session.is_server", session.IsServer);

        if (session is Session gameSession)
        {
            activity.SetTag("session.attributes.osu_version", gameSession.Attributes.OsuVersion);
            activity.SetTag("session.attributes.uses_osu_client", gameSession.Attributes.UsesOsuClient);
            activity.SetTag("session.attributes.status", gameSession.Attributes.Status.ToText());
            activity.SetTag("session.attributes.last_ping_request", gameSession.Attributes.LastPingRequest);
            activity.SetTag("session.attributes.show_user_location", gameSession.Attributes.ShowUserLocation);
            activity.SetTag("session.attributes.ignore_non_friend_pm", gameSession.Attributes.IgnoreNonFriendPm);
            activity.SetTag("session.attributes.is_bot", gameSession.Attributes.IsBot);
            activity.SetTag("session.spectators.count", gameSession.Spectators.Count);
            activity.SetTag("session.spectators.ids", string.Join(",", gameSession.Spectators.Keys));
            activity.SetTag("session.last_ratelimit_warning_message_sent_at", gameSession.LastRatelimitWarningMessageSentAt);

            if (gameSession.Match != null)
            {
                activity.SetTag("session.match.id", gameSession.Match.Match.MatchId);
                activity.SetTag("session.match.game_name", gameSession.Match.Match.GameName);
                activity.SetTag("session.match.host_id", gameSession.Match.Match.HostId);
                activity.SetTag("session.match.beatmap_id", gameSession.Match.Match.BeatmapId);
                activity.SetTag("session.match.mods", gameSession.Match.Match.ActiveMods);
            }
        }
    }

    public static void SetActivityUserTags(this Activity activity, User user)
    {
        activity.SetTag("user.id", user.Id);
        activity.SetTag("user.name", user.Username);
    }
}