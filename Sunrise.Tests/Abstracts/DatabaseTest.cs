using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services;
using Sunrise.Tests.Services.Mock;

namespace Sunrise.Tests.Abstracts;

public abstract class DatabaseTest(IntegrationDatabaseFixture fixture) : BaseTest, IAsyncLifetime
{
    private readonly FileService _fileService = new();
    private readonly MockService _mocker = new();

    protected SunriseServerFactory App => fixture.App;

    protected IServiceScope Scope => App.Server.Services.CreateScope();

    protected DatabaseService Database => Scope.ServiceProvider.GetRequiredService<DatabaseService>();
    protected SessionRepository Sessions => Scope.ServiceProvider.GetRequiredService<SessionRepository>();

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
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
}