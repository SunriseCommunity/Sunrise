using System.Text.Json;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Tests.Abstracts;

namespace Sunrise.Shared.Tests.Objects;

[CollectionDefinition("Beatmap ranking configuration", DisableParallelization = true)]
public class BeatmapRankingConfigurationCollection
{
}

[Collection("Beatmap ranking configuration")]
public class BeatmapRankingTests : BaseTest
{
    static BeatmapRankingTests()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("API_TOKEN_SECRET")))
            Environment.SetEnvironmentVariable("API_TOKEN_SECRET", "ranking-test-token");
    }

    [Theory]
    [InlineData("ranked", BeatmapStatusWeb.Ranked, true, true)]
    [InlineData("approved", BeatmapStatusWeb.Approved, true, true)]
    [InlineData("loved", BeatmapStatusWeb.Loved, false, true)]
    [InlineData("qualified", BeatmapStatusWeb.Qualified, false, true)]
    [InlineData("pending", BeatmapStatusWeb.Pending, false, false)]
    [InlineData("wip", BeatmapStatusWeb.Wip, false, false)]
    [InlineData("graveyard", BeatmapStatusWeb.Graveyard, false, false)]
    public void StatusPropertiesAreDerivedFromStatusString(string statusString, BeatmapStatusWeb expectedStatus, bool isRanked, bool isScoreable)
    {
        var beatmap = new Beatmap { StatusString = statusString, Ranked = 71 };
        var beatmapSet = new BeatmapSet { StatusString = statusString, Ranked = 71 };

        Assert.Equal(expectedStatus, beatmap.StatusGeneric);
        Assert.Equal(expectedStatus, beatmapSet.StatusGeneric);
        Assert.Equal(isRanked, beatmap.IsRanked);
        Assert.Equal(isRanked, beatmapSet.IsRanked);
        Assert.Equal(isScoreable, beatmap.IsScoreable);
        Assert.Equal(isScoreable, beatmapSet.IsScoreable);
        Assert.Equal(71, beatmap.Ranked);
        Assert.Equal(71, beatmapSet.Ranked);
    }

    [Fact]
    public void CustomStatusUpdateKeepsBeatmapAndSetFieldsConsistent()
    {
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var nominator = new User { Id = 42 };
        var beatmap = new Beatmap { Checksum = "hash" };
        var beatmapSet = new BeatmapSet { Beatmaps = [beatmap] };
        var customStatus = new CustomBeatmapStatus
        {
            BeatmapHash = "hash",
            BeatmapSetId = 1,
            UpdatedByUserId = nominator.Id,
            UpdatedByUser = nominator,
            Status = BeatmapStatusWeb.Graveyard
        };

        beatmapSet.UpdateBeatmapRanking([customStatus]);

        Assert.Equal("graveyard", beatmapSet.StatusString);
        Assert.Equal((int)BeatmapStatusWeb.Graveyard, beatmapSet.Ranked);
        Assert.Same(nominator, beatmapSet.BeatmapNominatorUser);
        Assert.Equal("graveyard", beatmap.StatusString);
        Assert.Equal((int)BeatmapStatusWeb.Graveyard, beatmap.Ranked);
        Assert.Same(nominator, beatmap.BeatmapNominatorUser);
        Assert.Equal(BeatmapStatusWeb.Graveyard, beatmap.StatusGeneric);
        Assert.Equal(BeatmapStatusWeb.Graveyard, beatmapSet.StatusGeneric);
    }

    [Fact]
    public void IgnoreRankingResetsSetAndBeatmapNominatorsTogether()
    {
        var nominator = new User { Id = 42 };
        var beatmap = new Beatmap { BeatmapNominatorUser = nominator, StatusString = "pending", Ranked = 0 };
        var beatmapSet = new BeatmapSet
        {
            BeatmapNominatorUser = nominator,
            StatusString = "pending",
            Ranked = 0,
            Beatmaps = [beatmap]
        };

        beatmapSet.IgnoreBeatmapRanking();

        Assert.Equal("ranked", beatmapSet.StatusString);
        Assert.Equal((int)BeatmapStatusWeb.Ranked, beatmapSet.Ranked);
        Assert.Null(beatmapSet.BeatmapNominatorUser);
        Assert.Equal("ranked", beatmap.StatusString);
        Assert.Equal((int)BeatmapStatusWeb.Ranked, beatmap.Ranked);
        Assert.Null(beatmap.BeatmapNominatorUser);
    }

    [Theory]
    [InlineData("ranked", 1)]
    [InlineData("approved", 2)]
    [InlineData("loved", 4)]
    [InlineData("qualified", 3)]
    [InlineData("pending", 0)]
    [InlineData("wip", -1)]
    [InlineData("graveyard", -2)]
    public void CachedDtoRoundTripPreservesStatusAndNumericRanked(string statusString, int ranked)
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        var cached = new BeatmapSet { StatusString = statusString, Ranked = ranked, LastUpdated = DateTime.UtcNow };

        var json = JsonSerializer.Serialize(cached);
        var restored = JsonSerializer.Deserialize<BeatmapSet>(json, options)!;

        Assert.Contains($"\"ranked\":{ranked}", json);
        Assert.Equal(statusString, restored.StatusString);
        Assert.Equal(ranked, restored.Ranked);
        Assert.Equal(statusString switch
        {
            "ranked" => BeatmapStatusWeb.Ranked,
            "approved" => BeatmapStatusWeb.Approved,
            "loved" => BeatmapStatusWeb.Loved,
            "qualified" => BeatmapStatusWeb.Qualified,
            "wip" => BeatmapStatusWeb.Wip,
            "graveyard" => BeatmapStatusWeb.Graveyard,
            _ => BeatmapStatusWeb.Pending
        }, restored.StatusGeneric);
    }
}
