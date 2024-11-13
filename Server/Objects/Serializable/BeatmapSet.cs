using System.Text.Json.Serialization;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.Serializable;

public class BeatmapSet
{
    private readonly Dictionary<string, BeatmapStatus> _statusMap = new()
    {
        ["loved"] = BeatmapStatus.Loved,
        ["qualified"] = BeatmapStatus.Qualified,
        ["approved"] = BeatmapStatus.Approved,
        ["ranked"] = BeatmapStatus.Ranked,
        ["pending"] = BeatmapStatus.Pending,
        ["graveyard"] = BeatmapStatus.Pending,
        ["wip"] = BeatmapStatus.Pending
    };

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("artist")]
    public string Artist { get; set; }

    [JsonPropertyName("artist_unicode")]
    public string ArtistUnicode { get; set; }

    [JsonPropertyName("creator")]
    public string Creator { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; }

    [JsonPropertyName("tags")]
    public string Tags { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("title_unicode")]
    public string TitleUnicode { get; set; }

    [JsonPropertyName("covers")]
    public Covers Covers { get; set; }

    [JsonPropertyName("favourite_count")]
    public int FavouriteCount { get; set; }

    [JsonPropertyName("nsfw")]
    public bool NSFW { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("play_count")]
    public int PlayCount { get; set; }

    [JsonPropertyName("preview_url")]
    public string PreviewUrl { get; set; }

    [JsonPropertyName("status")]
    public string StatusString { get; set; }

    public BeatmapStatus Status => _statusMap[StatusString ?? "graveyard"];

    [JsonPropertyName("track_id")]
    public int? TrackId { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("video")]
    public bool HasVideo { get; set; }

    [JsonPropertyName("bpm")]
    public double BPM { get; set; }

    [JsonPropertyName("deleted_at")]
    [JsonConverter(typeof(DateTimeUnixConverter))]
    public DateTime? DeletedAt { get; set; }


    [JsonPropertyName("is_scoreable")]
    public bool IsScoreable { get; set; }

    [JsonPropertyName("last_updated")]
    [JsonConverter(typeof(DateTimeUnixConverter))]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("legacy_thread_url")]
    public string? LegacyThreadUrl { get; set; }

    [JsonPropertyName("ranked")]
    public int Ranked { get; set; }

    [JsonPropertyName("ranked_date")]
    [JsonConverter(typeof(DateTimeUnixConverter))]
    public DateTime? RankedDate { get; set; }

    [JsonPropertyName("storyboard")]
    public bool HasStoryboard { get; set; }

    [JsonPropertyName("submitted_date")]
    [JsonConverter(typeof(DateTimeUnixConverter))]
    public DateTime SubmittedDate { get; set; }

    [JsonPropertyName("availability")]
    public BeatmapAvailability Availability { get; set; }

    [JsonPropertyName("beatmaps")]
    public Beatmap[] Beatmaps { get; set; }

    [JsonPropertyName("converts")]
    public Beatmap[] ConvertedBeatmaps { get; set; }

    [JsonPropertyName("description")]
    public BeatmapSetDescription Description { get; set; }

    [JsonPropertyName("genre")]
    public BeatmapSetGenre Genre { get; set; }

    [JsonPropertyName("language")]
    public BeatmapSetLanguage Language { get; set; }

    [JsonPropertyName("related_users")]
    public CompactUser[]? RelatedUsers { get; set; }

    [JsonPropertyName("user")]
    public CompactUser? User { get; set; }

    public string ToSearchResult(Session session)
    {
        var beatmaps = Beatmaps.GroupBy(x => x.DifficultyRating).OrderBy(x => x.Key).SelectMany(x => x).Aggregate("",
            (current, map) => current + map.ToSearchEntity()).TrimEnd(',');

        var hasVideo = HasVideo ? "1" : "0";

        var beatmapStatus = Parsers.GetBeatmapSearchStatus(StatusString);
        var lastUpdatedTime = (beatmapStatus >= BeatmapStatusSearch.Ranked ? RankedDate : LastUpdated) + TimeSpan.FromHours(session.Attributes.Timezone);

        return $"{Id}.osz|{Artist.Replace('|', 'I')}|{Title.Replace('|', 'I')}|{Creator.Replace('|', 'I')}|{(int)beatmapStatus}|10.0|{lastUpdatedTime}|{Id}|0|{hasVideo}|0|0|0|{beatmaps}";
    }
}

public class Covers
{
    [JsonPropertyName("card")]
    public string Card { get; set; }

    [JsonPropertyName("card@2x")]
    public string Card2x { get; set; }

    [JsonPropertyName("cover")]
    public string Cover { get; set; }

    [JsonPropertyName("cover@2x")]
    public string Cover2x { get; set; }

    [JsonPropertyName("list")]
    public string List { get; set; }

    [JsonPropertyName("list@2x")]
    public string List2x { get; set; }

    [JsonPropertyName("slimcover")]
    public string SlimCover { get; set; }

    [JsonPropertyName("slimcover@2x")]
    public string SlimCover2x { get; set; }
}

public class BeatmapAvailability
{
    [JsonPropertyName("download_disabled")]
    public bool DownloadDisabled { get; set; }

    [JsonPropertyName("more_information")]
    public string? MoreInformation { get; set; }
}

public class BeatmapSetGenre
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class BeatmapSetLanguage
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class BeatmapSetDescription
{
    [JsonPropertyName("description")]
    public string description { get; set; }
}

public class CompactUser
{
    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; }

    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }
}