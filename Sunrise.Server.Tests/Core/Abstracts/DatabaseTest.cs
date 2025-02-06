using DatabaseWrapper.Core;
using Microsoft.AspNetCore.Http;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Objects;
using Sunrise.Server.Services;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Tests.Utils;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Tests.Core.Abstracts;

[Collection("Database tests collection")]
public abstract class DatabaseTest : IDisposable, IClassFixture<DatabaseFixture>
{
    private static readonly WatsonORM _orm = new(new DatabaseSettings($"{Path.Combine(Configuration.DataPath, Configuration.DatabaseName)}; Pooling=false;"));

    protected DatabaseTest()
    {
        CreateFilesCopy();
        _orm.InitializeDatabase();
    }

    protected static async Task<User> CreateTestUser()
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var username = MockUtil.GetRandomUsername();
        while (await database.UserService.GetUser(username: username) != null)
        {
            username = MockUtil.GetRandomUsername();
        }

        var user = new User
        {
            Username = username,
            Email = MockUtil.GetRandomEmail(username),
            Passhash = MockUtil.GetRandomPassword().GetPassHash(),
            Country = MockUtil.GetRandomCountryCode(),
        };

        return await CreateTestUser(user);
    }

    protected static async Task<User> CreateTestUser(User user)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        return await database.UserService.InsertUser(user);
    }

    protected static async Task<Score> CreateTestScore(bool withReplay = true)
    {
        var user = await CreateTestUser();
        return await CreateTestScore(user, withReplay);
    }

    protected static async Task<Score> CreateTestScore(Score score, bool withReplay = true)
    {
        var user = await CreateTestUser();
        return await CreateTestScore(user, withReplay);
    }

    protected static async Task<Score> CreateTestScore(User user, bool withReplay = true)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var replayRecordId = MockUtil.GetRandomInteger(length: 6);

        if (withReplay)
        {
            IFormFile formFile = new FormFile(new MemoryStream(new byte[1024]), 0, 1024, "data", $"{MockUtil.GetRandomString(6)}.osr");
            var replayRecord = await database.ScoreService.Files.UploadReplay(user.Id, formFile);
            replayRecordId = replayRecord.Id;
        }

        var score = MockUtil.GetRandomScore();
        score.UserId = user.Id;
        score.ReplayFileId = replayRecordId;
        score.IsScoreable = true;
        score.IsPassed = true;
        score.SubmissionStatus = SubmissionStatus.Best;

        score = await database.ScoreService.InsertScore(score);

        return score;
    }

    private static void CreateFilesCopy()
    {
        var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), Configuration.DataPath.Replace(".tmp", ""));
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), $"{Configuration.DataPath}");

        if (!Directory.Exists(dataPath))
            Directory.CreateDirectory(dataPath);

        FolderUtil.Copy(sourcePath, dataPath);
    }

    public virtual void Dispose()
    {
        var tables = _orm.Database.ListTables();

        foreach (var table in tables)
            _orm.Database.DropTable(table);

        _orm.Dispose();
        Directory.Delete(Path.Combine(Configuration.DataPath, "Files"), true);

        GC.SuppressFinalize(this);
    }
}