using System.Reflection;

namespace Sunrise.Server.Types.Enums;

public static class RequestType
{
    // Assets controller
    public const string GetBanchoAvatar = "{id:int}";
    public const string GetAvatar = "avatar/{id:int}";
    public const string GetBanner = "banner/{id:int}";
    public const string GetScreenshot = "ss/{id:int}.jpg";
    public const string BannerUpload = "upload/banner";
    public const string AvatarUpload = "upload/avatar";
    public const string MenuContent = "menu-content.json";
    public const string EventBanner = "events/EventBanner.jpg";

    // Bancho controller
    public const string BanchoProcess = "/";

    // Direct controller
    public const string OsuSearch = "osu-search.php";
    public const string OsuSearchSet = "osu-search-set.php";

    // Score controller
    public const string OsuGetScores = "osu-osz2-getscores.php";
    public const string OsuSubmitScore = "osu-submit-modular-selector.php";
    public const string OsuGetReplay = "osu-getreplay.php";

    // Web controller
    public const string OsuGetFriends = "osu-getfriends.php";
    public const string OsuScreenshot = "osu-screenshot.php";
    public const string OsuError = "osu-error.php";
    public const string LastFm = "lastfm.php";
    public const string OsuMarkAsRead = "osu-markasread.php";
    public const string BanchoConnect = "bancho_connect.php";
    public const string OsuSession = "osu-session.php";
    public const string CheckUpdates = "check-updates.php";
    public const string OsuGetSeasonalBackground = "osu-getseasonal.php";
    public const string PostRegister = "/users";

    public static bool IsValidRequestType(this string requestType)
    {
        return typeof(RequestType)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false })
            .Any(f => (string)f.GetValue(null)! == requestType);
    }
}