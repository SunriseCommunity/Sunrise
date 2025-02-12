using DatabaseWrapper.Core;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories;
using Sunrise.Server.Tests.Core.Services;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Tests.Core.Abstracts;

[Collection("Database tests collection")]
public abstract class DatabaseTest : BaseTest, IDisposable, IClassFixture<DatabaseFixture>
{
    private readonly FileService _fileService = new();
    private readonly MockService _mocker = new();

    protected DatabaseTest(bool useRedis = false)
    {
        UpdateRedisVariables(useRedis);

        CreateFilesCopy();
    }

    public new virtual void Dispose()
    {
        var orm = new WatsonORM(new DatabaseSettings($"{Path.Combine(Configuration.DataPath, Configuration.DatabaseName)}; Pooling=false;"));

        if (!Configuration.DatabaseName.IsDevelopmentFile())
            throw new InvalidOperationException("Database name is not a development file. Are you trying to delete production data?");

        if (!Configuration.DataPath.IsDevelopmentFile())
            throw new InvalidOperationException("Data path is not a development directory. Are you trying to delete production data?");

        orm.InitializeDatabase();

        var tables = orm.Database.ListTables();

        foreach (var table in tables)
        {
            orm.Database.DropTable(table);
        }

        orm.Dispose();
        base.Dispose();

        var jobStorage = JobStorage.Current;
        var monitoringApi = jobStorage.GetMonitoringApi();

        while (true)
        {
            var jobs = monitoringApi.ProcessingJobs(0, int.MaxValue);

            if (jobs.Count == 0)
                break;
        }

        monitoringApi.DeletedJobs(0, int.MaxValue);

        Directory.Delete(Path.Combine(Configuration.DataPath, "Files"), true);

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
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var username = _mocker.User.GetRandomUsername();

        while (await database.UserService.GetUser(username: username) != null)
        {
            username = _mocker.User.GetRandomUsername();
        }

        var user = _mocker.User.GetRandomUser(username);

        return await CreateTestUser(user);
    }

    protected async Task<User> CreateTestUser(User user)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        return await database.UserService.InsertUser(user);
    }

    protected async Task<Score> CreateTestScore(bool withReplay = true)
    {
        var user = await CreateTestUser();
        return await CreateTestScore(user, withReplay);
    }

    protected async Task<Score> CreateTestScore(Score score)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var scoreUser = await database.UserService.GetUser(id: score.UserId);

        if (scoreUser == null)
        {
            await CreateTestUser();
        }

        return await database.ScoreService.InsertScore(score);
    }

    protected async Task<Score> CreateTestScore(User user, bool withReplay = true)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var replayRecordId = _mocker.GetRandomInteger(length: 6);

        if (withReplay)
        {
            IFormFile formFile = new FormFile(new MemoryStream(new byte[1024]), 0, 1024, "data", $"{_mocker.GetRandomString(6)}.osr");
            var replayRecord = await database.ScoreService.Files.UploadReplay(user.Id, formFile);
            replayRecordId = replayRecord.Id;
        }

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.ReplayFileId = replayRecordId;

        score = await database.ScoreService.InsertScore(score);

        return score;
    }

    protected async Task<(ReplayFile, int)> GetValidTestReplay()
    {
        var replayPath = _fileService.GetRandomFilePath("osr");
        var replay = new ReplayFile(replayPath);

        var beatmapId = await _mocker.Redis.MockLocalBeatmapFile(replay.GetScore().BeatmapHash);

        return (replay, beatmapId);
    }

    private void CreateFilesCopy()
    {
        if (!Configuration.DataPath.IsDevelopmentFile())
            throw new InvalidOperationException("Data path is not a development directory. Are you trying to modify production data?");

        var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), Configuration.DataPath.Replace(".tmp", ""));
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), $"{Configuration.DataPath}");

        if (!Directory.Exists(dataPath))
            Directory.CreateDirectory(dataPath);

        FolderUtil.Copy(sourcePath, dataPath);
    }

    private void UpdateRedisVariables(bool useRedis)
    {
        EnvManager.Set("Redis:ClearCacheOnStartup", useRedis ? "true" : "false");
        EnvManager.Set("Redis:UseCache", useRedis ? "true" : "false");
    }
}