using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using Beatmap = Sunrise.Shared.Objects.Serializable.Beatmap;
using GameMode = osu.Shared.GameMode;

namespace Sunrise.Tests.Services.Mock.Services;

public class MockBeatmapService(MockService service)
{
    public BeatmapStatus GetRandomBeatmapStatus()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(BeatmapStatus));
        return (BeatmapStatus)values.GetValue(random.Next(values.Length))!;
    }

    public BeatmapSet GetRandomBeatmapSet()
    {
        var beatmapSet = new BeatmapSet
        {
            Id = service.GetRandomInteger(),
            Artist = service.GetRandomString(),
            ArtistUnicode = service.GetRandomString(),
            Creator = service.GetRandomString(),
            Source = service.GetRandomString(),
            Tags = service.GetRandomString(),
            Title = service.GetRandomString(),
            TitleUnicode = service.GetRandomString(),
            Covers = new Covers
            {
                Cover = service.GetRandomString(),
                Cover2x = service.GetRandomString(),
                Card = service.GetRandomString(),
                Card2x = service.GetRandomString(),
                List = service.GetRandomString(),
                List2x = service.GetRandomString(),
                SlimCover = service.GetRandomString(),
                SlimCover2x = service.GetRandomString()
            },
            FavouriteCount = service.GetRandomInteger(),
            NSFW = service.GetRandomBoolean(),
            Offset = service.GetRandomInteger(),
            PlayCount = service.GetRandomInteger(),
            PreviewUrl = service.GetRandomString(),
            StatusString = service.GetRandomString(),
            UserId = service.GetRandomInteger(),
            HasVideo = service.GetRandomBoolean(),
            BPM = service.GetRandomInteger(length: 2),
            LastUpdated = service.GetRandomDateTime(),
            Ranked = service.GetRandomInteger(1),
            RankedDate = service.GetRandomDateTime(),
            HasStoryboard = service.GetRandomBoolean(),
            SubmittedDate = service.GetRandomDateTime(),
            Availability = new BeatmapAvailability
            {
                DownloadDisabled = false
            },
            Beatmaps = [],
            ConvertedBeatmaps = []
        };

        for (var i = 0; i < service.GetRandomInteger(10, minInt: 1); i++)
        {
            beatmapSet.Beatmaps = beatmapSet.Beatmaps.Prepend(GetRandomBeatmap(beatmapSet)).ToArray();
            beatmapSet.ConvertedBeatmaps = beatmapSet.ConvertedBeatmaps.Prepend(GetRandomBeatmap(beatmapSet, true)).ToArray();
        }

        foreach (var beatmap in beatmapSet.ConvertedBeatmaps)
        {
            beatmap.BeatmapsetId = beatmapSet.Id;
        }

        foreach (var beatmap in beatmapSet.Beatmaps)
        {
            beatmap.BeatmapsetId = beatmapSet.Id;
        }

        return beatmapSet;
    }

    public Beatmap GetRandomBeatmap(bool convert = false)
    {
        return GetRandomBeatmap(GetRandomBeatmapSet(), convert);
    }

    public Beatmap GetRandomBeatmap(BeatmapSet beatmapSet, bool convert = false)
    {
        return new Beatmap
        {
            Id = service.GetRandomInteger(),
            BeatmapsetId = beatmapSet.Id,
            DifficultyRating = service.GetRandomInteger(10),
            Mode = Enum.GetValues<GameMode>().GetValue(service.GetRandomInteger(Enum.GetValues<GameMode>().Length))?.ToString()?.ToLower() ?? "osu",
            StatusString = service.GetRandomString(),
            TotalLength = service.GetRandomInteger(),
            UserId = beatmapSet.UserId,
            Version = service.GetRandomString(),
            Accuracy = service.GetRandomInteger(10),
            AR = service.GetRandomInteger(10),
            BPM = service.GetRandomInteger(length: 2),
            Convert = convert,
            CountCircles = service.GetRandomInteger(),
            CountSliders = service.GetRandomInteger(),
            CountSpinners = service.GetRandomInteger(),
            CS = service.GetRandomInteger(10),
            Drain = service.GetRandomInteger(10),
            HitLength = service.GetRandomInteger(),
            LastUpdated = beatmapSet.LastUpdated,
            ModeInt = service.GetRandomInteger(3),
            Passcount = beatmapSet.PlayCount,
            Playcount = beatmapSet.PlayCount,
            Ranked = beatmapSet.Ranked,
            Url = service.GetRandomString(),
            Checksum = service.GetRandomString(),
            MaxCombo = service.GetRandomInteger()
        };
    }

    public async Task<BeatmapSet> MockRandomBeatmapSet()
    {
        var beatmapSet = service.Beatmap.GetRandomBeatmapSet();

        return await MockBeatmapSet(beatmapSet);
    }

    public async Task<BeatmapSet> MockBeatmapSet(BeatmapSet beatmapSet)
    {
        return await service.Redis.MockBeatmapSetCache(beatmapSet);
    }
}