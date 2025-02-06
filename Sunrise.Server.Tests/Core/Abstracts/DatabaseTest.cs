using DatabaseWrapper.Core;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Services;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Tests.Utils;
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

    protected static async Task<User> CreateTestUser(User? user = null)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        if (user != null)
        {
            user = await database.UserService.InsertUser(user);
            return user;
        }

        var username = MockUtil.GetRandomUsername();
        while (await database.UserService.GetUser(username: username) != null)
        {
            username = MockUtil.GetRandomUsername();
        }

        user = new User
        {
            Username = username,
            Email = MockUtil.GetRandomEmail(username),
            Passhash = MockUtil.GetRandomPassword().GetPassHash(),
            Country = MockUtil.GetRandomCountryCode(),
        };

        user = await database.UserService.InsertUser(user);
        return user;
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
        //Directory.Delete(Path.Combine(Configuration.DataPath, "Files"), true);

        GC.SuppressFinalize(this);
    }
}