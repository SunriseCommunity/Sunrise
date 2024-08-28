using Watson.ORM.Sqlite;

namespace Sunrise.Server.Storage;

public static class DatabaseStorage
{
    private static readonly ILogger Logger;

    static DatabaseStorage()
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        Logger = loggerFactory.CreateLogger("DatabaseStorage");
    }

    public static async Task<T?> WriteRecordAsync<T>(this WatsonORM orm, T record) where T : class, new()
    {
        try
        {
            await orm.InsertAsync(record);
            return record;
        }
        catch (Exception e)
        {
            Logger.LogError(e, $"Failed to write record to database: {record}");
            return null;
        }
    }
}