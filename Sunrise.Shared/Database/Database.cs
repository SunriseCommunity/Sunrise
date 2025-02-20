using DatabaseWrapper.Core;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Event;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Database.Services.Beatmap;
using Sunrise.Shared.Database.Services.Event;
using Sunrise.Shared.Database.Services.Score;
using Sunrise.Shared.Database.Services.User;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Repositories;
using Watson.ORM.Sqlite;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database;

public sealed class DatabaseManager
{

    private readonly ILogger<DatabaseManager> _logger;
    private readonly WatsonORM _orm = new(new DatabaseSettings($"{Path.Combine(DataPath, Database)}; Pooling=false;"));
    private readonly RedisRepository _redis;

    public readonly BeatmapService BeatmapService;
    public readonly EventService EventService;
    public readonly MedalService MedalService;
    public readonly ScoreService ScoreService;
    public readonly UserService UserService;

    public DatabaseManager(RedisRepository redis)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<DatabaseManager>();

        _redis = redis;

        UserService = new UserService(this, _redis, _orm);
        BeatmapService = new BeatmapService(this, _redis, _orm);
        ScoreService = new ScoreService(this, _redis, _orm);
        MedalService = new MedalService(this, _redis, _orm);
        EventService = new EventService(this, _redis, _orm);

        _orm.InitializeDatabase();
        CheckMigrations();
    }

    private static string DataPath => Configuration.DataPath;
    private static string Database => Configuration.DatabaseName;

    public async Task InitializeBotInDatabase()
    {
        var isBotInitialized = await UserService.GetUser(username: Configuration.BotUsername, useCache: false);
        if (isBotInitialized != null) return;

        var bot = new User
        {
            Username = Configuration.BotUsername,
            Country = (short)CountryCode.AQ,
            Privilege = UserPrivilege.User,
            RegisterDate = DateTime.Now,
            Passhash = "12345678",
            Email = "bot@mail.com",
            AccountStatus = UserAccountStatus.Restricted // TODO: Find a better way to handle this
        };

        bot = await UserService.InsertUser(bot);
        if (bot == null) throw new Exception("Failed to insert bot into the database");

        var botAvatar = await File.ReadAllBytesAsync(Path.Combine(DataPath, "Files/Assets/BotAvatar.png"));
        await UserService.Files.SetAvatar(bot.Id, botAvatar);
    }

    private void CheckMigrations()
    {
        _orm.InitializeTable(typeof(Migration));

        var migrationManager = new MigrationManager(_orm);
        var appliedMigrations = migrationManager.ApplyMigrations(Path.Combine(DataPath, "Migrations"));

        _orm.InitializeTables([
            typeof(User), typeof(UserStats), typeof(UserFile), typeof(Restriction), typeof(BeatmapFile), typeof(Score),
            typeof(Medal), typeof(MedalFile), typeof(UserMedals), typeof(UserStatsSnapshot), typeof(EventUser),
            typeof(UserFavouriteBeatmap)
        ]);

        if (appliedMigrations <= 0 && !Configuration.ClearCacheOnStartup) return;

        _logger.LogInformation($"Applied {appliedMigrations} migrations");
        _logger.LogWarning("Cache will be flushed due to database changes. This may cause performance issues.");

        _redis.FlushAllCache();
        _logger.LogInformation("Cache flushed. Rebuilding user ranks...");

        foreach (var i in Enum.GetValues(typeof(GameMode)))
        {
            UserService.Stats.SetAllUserRanks((GameMode)i).Wait();
        }

        _logger.LogInformation("User ranks rebuilt. Cache is now up to date.");
    }
}