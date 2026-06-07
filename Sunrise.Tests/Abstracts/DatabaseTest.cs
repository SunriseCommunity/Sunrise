using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils.Processing;

namespace Sunrise.Tests.Abstracts;

// TODO: Switch reuseScopeInContext to true and remove it after fixing tests who depends on EF Core context being clear
// - Okay, this is actually might be the heated topic, so I will clarify some things about this
// New scope was always almost fine for us, since we were refetching the entities with new DB call, not EF native reload
// But if we are going to try to use services like Database inside the test method, they are going to be working incorrectly, since the context is expected to be the sape per HTTP call
// Not to mention that it should be actually expected to have the same scope during the test suite execution, since we are testing the database, and not the scope itself
// BUT! Since for the HTTP calls we create new scope, after any HTTP call execution any of our EF core internal entities would be outdated
// The problem is that EF Core is lazy and will get the element directly after the HTTP call execution instead of going to the database, which is probably outdated. The only way to fix this is to manually clear tracking for entities AFAIK.
// We *could* clear the tracking automatically after the HTTP calls, but I'm afraid it's not the better solution than just to recreate the scope on each call.
// For now, this is actually kinda works, but this is a technical debt :/
public abstract class DatabaseTest(IntegrationDatabaseFixture fixture, bool reuseScopeInContext = false) : BaseTest, IAsyncLifetime
{
    private readonly FileService _fileService = new();
    private readonly MockService _mocker = new();
    private IServiceScope? _scope;

    protected SunriseServerFactory App => fixture.App;

    protected IServiceScope Scope => reuseScopeInContext ? _scope ??= App.Server.Services.CreateScope() : App.Server.Services.CreateScope();

    protected DatabaseService Database => Scope.ServiceProvider.GetRequiredService<DatabaseService>();
    protected SessionRepository Sessions => Scope.ServiceProvider.GetRequiredService<SessionRepository>();

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        if (!reuseScopeInContext)
        {
            _scope?.Dispose();
            _scope = null;
        }

        return Task.CompletedTask;
    }

    protected async Task<(Session, User)> CreateTestSession()
    {
        var user = await CreateTestUser();
        return (CreateTestSession(user), user);
    }

    protected Session CreateTestSession(User user)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var location = new Location();
        var loginRequest = new LoginRequest(
            user.Username,
            user.Passhash,
            _mocker.GetRandomString(6),
            0,
            _mocker.GetRandomBoolean(),
            _mocker.GetRandomString(),
            _mocker.GetRandomBoolean());

        var session = sessions.CreateSession(user, location, loginRequest);

        return session;
    }

    protected async Task<User> CreateTestUser()
    {
        var username = _mocker.User.GetRandomUsername();

        while (await Database.Users.GetUser(username: username) != null)
        {
            username = _mocker.User.GetRandomUsername();
        }

        var user = _mocker.User.GetRandomUser(username);

        return await CreateTestUser(user);
    }

    protected async Task<User> CreateTestUser(User user)
    {
        await Database.Users.AddUser(user);

        user.LastOnlineTime = user.LastOnlineTime.ToDatabasePrecision();
        user.RegisterDate = user.RegisterDate.ToDatabasePrecision();

        return user;
    }

    protected async Task<List<User>> CreateTestUsers(int count = 1)
    {
        using var scope = App.Server.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var users = new List<User>();

        for (var i = 0; i < count; i++)
        {
            var user = _mocker.User.GetRandomUser($"{_mocker.User.GetRandomUsername()}_{i}");

            user.LastOnlineTime = user.LastOnlineTime.ToDatabasePrecision();
            user.RegisterDate = user.RegisterDate.ToDatabasePrecision();

            user.Id = i + 10000;

            users.Add(user);
        }

        await database.DbContext.Users.AddRangeAsync(users);

        var modes = Enum.GetValues<GameMode>();
        var userStats = new List<UserStats>();

        foreach (var mode in modes)
        {
            foreach (var user in users)
            {
                var stats = new UserStats
                {
                    UserId = user.Id,
                    GameMode = mode
                };

                userStats.Add(stats);
            }
        }

        await database.DbContext.UserStats.AddRangeAsync(userStats);

        await database.DbContext.SaveChangesAsync();

        return users;
    }

    protected async Task<Score> CreateTestScore(bool withReplay = true)
    {
        var user = await CreateTestUser();

        user.LastOnlineTime = user.LastOnlineTime.ToDatabasePrecision();
        user.RegisterDate = user.RegisterDate.ToDatabasePrecision();

        return await CreateTestScore(user, withReplay);
    }

    protected async Task<Score> CreateTestScore(Score score)
    {
        var scoreUser = await Database.Users.GetUser(id: score.UserId);

        if (scoreUser == null)
        {
            await CreateTestUser();
        }

        await Database.Scores.AddScore(score);

        score.WhenPlayed = score.WhenPlayed.ToDatabasePrecision();
        score.ClientTime = score.ClientTime.ToDatabasePrecision();

        return score;
    }

    protected async Task<Score> CreateTestScore(User user, bool withReplay = true)
    {
        var replayRecordId = _mocker.GetRandomInteger(length: 6);

        if (withReplay)
        {
            IFormFile formFile = new FormFile(new MemoryStream(new byte[1024]), 0, 1024, "data", $"{_mocker.GetRandomString(6)}.osr");
            var replayRecord = await Database.Scores.Files.AddReplayFile(user.Id, formFile);
            replayRecordId = replayRecord.Value.Id;
        }

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.ReplayFileId = replayRecordId;

        score.WhenPlayed = score.WhenPlayed.ToDatabasePrecision();
        score.ClientTime = score.ClientTime.ToDatabasePrecision();

        await Database.Scores.AddScore(score);

        var scoreLeaderboardTask = await Database.Scores.EnrichScoresWithLeaderboardPosition([score]);

        score = scoreLeaderboardTask.First();

        return score;
    }

    protected (ReplayFile, int) GetValidTestReplay()
    {
        var replayPath = _fileService.GetRandomFilePath("osr");
        var replay = new ReplayFile(replayPath);

        var beatmapId = _mocker.Redis.GetBeatmapIdFromHash(replay.GetScore().BeatmapHash);

        return (replay, beatmapId);
    }

    protected async Task<int> CreateReplayFileId(int userId)
    {
        IFormFile replayFile = new FormFile(new MemoryStream(new byte[1024]), 0, 1024, "data", "score.osr");
        var replayResult = await Database.Scores.Files.AddReplayFile(userId, replayFile);

        Assert.True(replayResult.IsSuccess);
        return replayResult.Value.Id;
    }

    protected async Task<ScoreSubmissionRequest> CreateTestScoreSubmissionRequest(Score score, User user, bool withReplay = true)
    {
        int? replayFileId = withReplay ? await CreateReplayFileId(user.Id) : null;
        var queueEntry = ScoreSubmissionRequestTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);

        await Database.ScoreSubmissionRequests.AddQueueEntry(queueEntry);

        return queueEntry;
    }
}