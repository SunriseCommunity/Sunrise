using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

[Collection("Integration tests collection")]
public class ApiSearchBeatmapsetsTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestSearchBeatmapsetsWithCustomStatus()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var randomUser = _mocker.User.GetRandomUser();
        await Database.Users.AddUser(randomUser);

        var beatmapSet1 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet1.Id = 1;
        var beatmap1 = beatmapSet1.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap1.Id = 100;
        beatmap1.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        var beatmapSet2 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet2.Id = 2;
        var beatmap2 = beatmapSet2.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap2.Id = 200;
        beatmap2.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet1);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet2);

        var addResult1 = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Loved,
            BeatmapHash = beatmap1.Checksum!,
            BeatmapSetId = beatmapSet1.Id,
            UpdatedByUserId = randomUser.Id
        });

        if (addResult1.IsFailure)
            throw new Exception(addResult1.Error);

        var addResult2 = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Qualified,
            BeatmapHash = beatmap2.Checksum!,
            BeatmapSetId = beatmapSet2.Id,
            UpdatedByUserId = randomUser.Id
        });

        if (addResult2.IsFailure)
            throw new Exception(addResult2.Error);

        // Act
        var response = await client.GetAsync("beatmapset/search?searchByCustomStatus=true&limit=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Sets);
        Assert.Equal(2, result.Sets.Count);
        Assert.NotNull(result.TotalCount);
        Assert.Equal(2, result.TotalCount.Value);
    }

    [Fact]
    public async Task TestSearchBeatmapsetsWithCustomStatusOmitNotFoundBeatmaps()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var randomUser = _mocker.User.GetRandomUser();
        await Database.Users.AddUser(randomUser);

        var beatmapSet1 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet1.Id = 1;
        var beatmap1 = beatmapSet1.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap1.Id = 100;
        beatmap1.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        var beatmapSet2 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet2.Id = 2;
        var beatmap2 = beatmapSet2.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap2.Id = 200;
        beatmap2.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet1);

        var addResult1 = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Loved,
            BeatmapHash = beatmap1.Checksum!,
            BeatmapSetId = beatmapSet1.Id,
            UpdatedByUserId = randomUser.Id
        });

        if (addResult1.IsFailure)
            throw new Exception(addResult1.Error);

        var addResult2 = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Qualified,
            BeatmapHash = beatmap2.Checksum!,
            BeatmapSetId = beatmapSet2.Id,
            UpdatedByUserId = randomUser.Id
        });

        if (addResult2.IsFailure)
            throw new Exception(addResult2.Error);

        // Act
        var response = await client.GetAsync("beatmapset/search?searchByCustomStatus=true&limit=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Sets);
        Assert.Equal(1, result.Sets.Count);
        Assert.NotNull(result.TotalCount);
        Assert.Equal(2, result.TotalCount.Value);
    }

    [Fact]
    public async Task TestSearchBeatmapsetsWithCustomStatusAndStatusFilter()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var randomUser = _mocker.User.GetRandomUser();
        await Database.Users.AddUser(randomUser);

        var beatmapSet1 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet1.Id = 1;
        var beatmap1 = beatmapSet1.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap1.Id = 100;
        beatmap1.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        var beatmapSet2 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet2.Id = 2;
        var beatmap2 = beatmapSet2.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap2.Id = 200;
        beatmap2.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet1);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet2);

        var addResult1 = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Loved,
            BeatmapHash = beatmap1.Checksum!,
            BeatmapSetId = beatmapSet1.Id,
            UpdatedByUserId = randomUser.Id
        });

        if (addResult1.IsFailure)
            throw new Exception(addResult1.Error);

        var addResult2 = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Qualified,
            BeatmapHash = beatmap2.Checksum!,
            BeatmapSetId = beatmapSet2.Id,
            UpdatedByUserId = randomUser.Id
        });

        if (addResult2.IsFailure)
            throw new Exception(addResult2.Error);

        // Act - Search for only Loved beatmaps
        var response = await client.GetAsync($"beatmapset/search?searchByCustomStatus=true&status={BeatmapStatusWeb.Loved:D}&limit=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Sets);
        Assert.Single(result.Sets);
        Assert.Equal(BeatmapStatusWeb.Loved, result.Sets[0].Status);
        Assert.NotNull(result.TotalCount);
        Assert.Equal(1, result.TotalCount.Value);
    }

    [Fact]
    public async Task TestSearchBeatmapsetsWithCustomStatusAndPagination()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var randomUser = _mocker.User.GetRandomUser();
        await Database.Users.AddUser(randomUser);

        // Create 5 beatmapsets with custom statuses
        for (var i = 1; i <= 5; i++)
        {
            var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
            beatmapSet.Id = i;
            var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
            beatmap.Id = i * 100;
            beatmap.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

            await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

            var addResult = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
            {
                Status = BeatmapStatusWeb.Loved,
                BeatmapHash = beatmap.Checksum!,
                BeatmapSetId = beatmapSet.Id,
                UpdatedByUserId = randomUser.Id
            });

            if (addResult.IsFailure)
                throw new Exception(addResult.Error);
        }

        // Act - Page 1 with limit 2
        var response1 = await client.GetAsync("beatmapset/search?searchByCustomStatus=true&page=1&limit=2");

        // Assert - Page 1
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var result1 = await response1.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();

        Assert.NotNull(result1);
        Assert.NotEmpty(result1.Sets);
        Assert.Equal(2, result1.Sets.Count);
        Assert.NotNull(result1.TotalCount);
        Assert.Equal(5, result1.TotalCount.Value);

        // Act - Page 2 with limit 2
        var response2 = await client.GetAsync("beatmapset/search?searchByCustomStatus=true&page=2&limit=2");

        // Assert - Page 2
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var result2 = await response2.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();

        Assert.NotNull(result2);
        Assert.NotEmpty(result2.Sets);
        Assert.Equal(2, result2.Sets.Count);
    }

    [Fact]
    public async Task TestSearchBeatmapsetsWithCustomStatusAndModeReturnsError()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmapset/search?searchByCustomStatus=true&mode={GameMode.Standard:D}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestSearchBeatmapsetsWithCustomStatusAndQueryReturnsError()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmapset/search?searchByCustomStatus=true&query=test");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestSearchBeatmapsetsWithCustomStatusAndLimitOver12ReturnsError()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmapset/search?searchByCustomStatus=true&limit=13");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestSearchBeatmapsetsWithCustomStatusEmptyResults()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        // Act
        var response = await client.GetAsync("beatmapset/search?searchByCustomStatus=true&limit=10");

        // Debug: Print response content if not OK
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Expected OK but got {response.StatusCode}. Response: {content}");
        }

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();

        Assert.NotNull(result);
        Assert.Empty(result.Sets);
        Assert.NotNull(result.TotalCount);
        Assert.Equal(0, result.TotalCount.Value);
    }

    [Fact]
    public async Task TestSearchBeatmapsetsWithCustomStatusMaxLimit()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var randomUser = _mocker.User.GetRandomUser();
        await Database.Users.AddUser(randomUser);

        // Create 15 beatmapsets with custom statuses
        for (var i = 1; i <= 15; i++)
        {
            var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
            beatmapSet.Id = i;
            var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
            beatmap.Id = i * 100;
            beatmap.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

            await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

            var addResult = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
            {
                Status = BeatmapStatusWeb.Loved,
                BeatmapHash = beatmap.Checksum!,
                BeatmapSetId = beatmapSet.Id,
                UpdatedByUserId = randomUser.Id
            });

            if (addResult.IsFailure)
                throw new Exception(addResult.Error);
        }

        // Act - Request with max limit (12)
        var response = await client.GetAsync("beatmapset/search?searchByCustomStatus=true&limit=12");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Sets);
        Assert.Equal(12, result.Sets.Count);
        Assert.NotNull(result.TotalCount);
        Assert.Equal(15, result.TotalCount.Value);
    }

    [Fact]
    public async Task TestSearchBeatmapsetsWithMultipleStatusFilters()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var randomUser = _mocker.User.GetRandomUser();
        await Database.Users.AddUser(randomUser);

        var beatmapSet1 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet1.Id = 1;
        var beatmap1 = beatmapSet1.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap1.Id = 100;
        beatmap1.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        var beatmapSet2 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet2.Id = 2;
        var beatmap2 = beatmapSet2.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap2.Id = 200;
        beatmap2.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        var beatmapSet3 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet3.Id = 3;
        var beatmap3 = beatmapSet3.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap3.Id = 300;
        beatmap3.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet1);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet2);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet3);

        var addResult1 = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Loved,
            BeatmapHash = beatmap1.Checksum!,
            BeatmapSetId = beatmapSet1.Id,
            UpdatedByUserId = randomUser.Id
        });

        if (addResult1.IsFailure)
            throw new Exception(addResult1.Error);

        var addResult2 = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Qualified,
            BeatmapHash = beatmap2.Checksum!,
            BeatmapSetId = beatmapSet2.Id,
            UpdatedByUserId = randomUser.Id
        });

        if (addResult2.IsFailure)
            throw new Exception(addResult2.Error);

        var addResult3 = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Ranked,
            BeatmapHash = beatmap3.Checksum!,
            BeatmapSetId = beatmapSet3.Id,
            UpdatedByUserId = randomUser.Id
        });

        if (addResult3.IsFailure)
            throw new Exception(addResult3.Error);

        // Act - Search for Loved and Qualified beatmaps
        var response = await client.GetAsync($"beatmapset/search?searchByCustomStatus=true&status={BeatmapStatusWeb.Loved:D}&status={BeatmapStatusWeb.Qualified:D}&limit=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Sets);
        Assert.Equal(2, result.Sets.Count);
        Assert.NotNull(result.TotalCount);
        Assert.Equal(2, result.TotalCount.Value);
        Assert.Contains(result.Sets, s => s.Status == BeatmapStatusWeb.Loved);
        Assert.Contains(result.Sets, s => s.Status == BeatmapStatusWeb.Qualified);
    }
}