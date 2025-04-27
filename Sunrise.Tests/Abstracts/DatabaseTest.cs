using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Tests.Abstracts;

[Collection("Database tests collection")]
public abstract class DatabaseTest : BaseTest, IDisposable
{
    private readonly FileService _fileService = new();
    private readonly MockService _mocker = new();

    protected DatabaseTest(bool useRedis = false)
    {
        try
        {
            UpdateRedisVariables(useRedis);
            CreateFilesCopy();

            App = new SunriseServerFactory();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    protected SunriseServerFactory App { get; }
    protected IServiceScope Scope => App.Server.Services.CreateScope();
    protected DatabaseService Database => Scope.ServiceProvider.GetRequiredService<DatabaseService>();
    protected SessionRepository Sessions => Scope.ServiceProvider.GetRequiredService<SessionRepository>();

    public new virtual void Dispose()
    {
        if (!Configuration.DataPath.IsDevelopmentFile())
            throw new InvalidOperationException("Data path is not a development directory. Are you trying to delete production data?");

        Directory.Delete(Path.Combine(Configuration.DataPath), true);

        EnvManager.Dispose();

        Scope?.Dispose();
        App?.Dispose();

        base.Dispose();
        GC.SuppressFinalize(this);
    }

    protected async Task<Session> CreateTestSession()
    {
        var user = await CreateTestUser();
        return CreateTestSession(user);
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

    private void UpdateRedisVariables(bool useRedis)
    {
        EnvManager.Set("Redis:ClearCacheOnStartup", useRedis ? "true" : "false");
        EnvManager.Set("Redis:UseCache", useRedis ? "true" : "false");
    }

    private void CreateFilesCopy()
    {
        var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), Configuration.DataPath);

        EnvManager.Set("Files:DataPath", Configuration.DataPath + _mocker.GetRandomString(12) + ".tmp");

        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), $"{Configuration.DataPath}");

        if (!Directory.Exists(dataPath))
            Directory.CreateDirectory(dataPath);

        FolderUtil.Copy(sourcePath, dataPath);
    }
}