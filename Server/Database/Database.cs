using DatabaseWrapper.Core;
using ExpressionTree;
using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Helpers;
using Sunrise.Server.Managers;
using Sunrise.Server.Repositories;
using Sunrise.Server.Storages;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database;

public sealed class SunriseDb
{
    private const string DataPath = Configuration.DataPath;
    private const string Database = Configuration.DatabaseName;
    private readonly ILogger<SunriseDb> _logger;
    private readonly WatsonORM _orm = new(new DatabaseSettings(DataPath + Database));

    public SunriseDb(RedisRepository redis)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<SunriseDb>();

        Redis = redis;

        _orm.InitializeDatabase();
        _orm.InitializeTable(typeof(Migration));

        var migrationManager = new MigrationManager(_orm);
        var appliedMigrations = migrationManager.ApplyMigrations($"{DataPath}Migrations");

        _orm.InitializeTables([
            typeof(User), typeof(UserStats), typeof(UserFile), typeof(Restriction), typeof(BeatmapFile), typeof(Score),
            typeof(Medal), typeof(MedalFile), typeof(UserMedals), typeof(UserStatsSnapshot)
        ]);

        if (appliedMigrations <= 0) return;

        _logger.LogInformation($"Applied {appliedMigrations} migrations");
        _logger.LogWarning("Cache will be flushed due to database changes. This may cause performance issues.");

        redis.FlushAllCache();
        _logger.LogInformation("Cache flushed. Rebuilding user ranks...");

        for (var i = 0; i < 4; i++) SetAllUserRanks((GameMode)i).Wait();
        _logger.LogInformation("User ranks rebuilt. Cache is now up to date.");
    }

    private RedisRepository Redis { get; }

    public async Task<User> InsertUser(User user)
    {
        user = await _orm.InsertAsync(user);

        var modes = Enum.GetValues<GameMode>();

        foreach (var mode in modes)
        {
            var stats = new UserStats
            {
                UserId = user.Id,
                GameMode = mode
            };
            await InsertUserStats(stats);
        }

        await Redis.Set(RedisKey.UserById(user.Id), user);

        return user;
    }

    public async Task<User?> GetUser(int? id = null, string? username = null, string? email = null,
        string? passhash = null, bool useCache = true)
    {
        var redisKeys = new List<string>
        {
            RedisKey.UserById(id ?? 0),
            RedisKey.UserByEmail(email ?? "")
        };

        if (username != null)
        {
            if (passhash != null)
                redisKeys.Add(RedisKey.UserByUsernameAndPassHash(username, passhash));
            else
                redisKeys.Add(RedisKey.UserByUsername(username));
        }

        var cachedUser = await Redis.Get<User?>([.. redisKeys]);

        if (cachedUser != null && useCache) return cachedUser;

        if (passhash != null && id == null && username == null && email == null)
            throw new Exception("Passhash provided without any other parameters");

        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);
        if (id != null) exp = exp.PrependAnd("Id", OperatorEnum.Equals, id);
        if (username != null) exp = exp.PrependAnd("Username", OperatorEnum.Equals, username);
        if (email != null) exp = exp.PrependAnd("Email", OperatorEnum.Equals, email);
        if (passhash != null) exp = exp.PrependAnd("Passhash", OperatorEnum.Equals, passhash);

        var user = await _orm.SelectFirstAsync<User?>(exp);

        if (user == null) return null;

        await Redis.Set(
            [RedisKey.UserById(user.Id), RedisKey.UserByUsername(user.Username), RedisKey.UserByEmail(user.Email)],
            user);

        return user;
    }

    public async Task UpdateUser(User user)
    {
        await _orm.UpdateAsync(user);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var session = sessions.GetSession(userId: user.Id);

        session?.UpdateUser(user);

        await Redis.Set(
        [
            RedisKey.UserById(user.Id), RedisKey.UserByUsername(user.Username), RedisKey.UserByEmail(user.Email),
            RedisKey.UserByUsernameAndPassHash(user.Username, user.Passhash)
        ], user);
    }

    public async Task<UserStats?> GetUserStats(int userId, GameMode mode, bool useCache = true)
    {
        var cachedStats = await Redis.Get<UserStats?>(RedisKey.UserStats(userId, mode));

        if (cachedStats != null && useCache) return cachedStats;

        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode", OperatorEnum.Equals,
            (int)mode);
        var stats = await _orm.SelectFirstAsync<UserStats?>(exp);

        if (stats == null)
        {
            _logger.LogCritical($"User stats not found for user {userId} in mode {mode}. Is database corrupted?");
            return null;
        }

        await Redis.Set(RedisKey.UserStats(userId, mode), stats);

        return stats;
    }

    public async Task<List<UserStats>> GetAllUserStats(GameMode mode, LeaderboardSortType leaderboardSortType,
        bool useCache = true)
    {
        var cachedStats = await Redis.Get<List<UserStats>>(RedisKey.AllUserStats(mode));

        if (cachedStats != null && useCache) return cachedStats;

        var exp = new Expr("GameMode", OperatorEnum.Equals, (int)mode);

        var stats = await _orm.SelectManyAsync<UserStats>(exp,
        [
            leaderboardSortType switch
            {
                LeaderboardSortType.Pp => new ResultOrder("PerformancePoints", OrderDirectionEnum.Descending),
                LeaderboardSortType.Score => new ResultOrder("TotalScore", OrderDirectionEnum.Descending),
                _ => throw new ArgumentOutOfRangeException(nameof(leaderboardSortType), leaderboardSortType, null)
            }
        ]);

        if (stats == null) return [];

        await Redis.Set(RedisKey.AllUserStats(mode), stats);

        return stats;
    }

    public async Task<List<User>?> GetAllUsers(bool useCache = true)
    {
        var cachedStats = await Redis.Get<List<User>>(RedisKey.AllUsers());

        if (cachedStats != null && useCache) return cachedStats;

        var stats = await _orm.SelectManyAsync<User>(new Expr("Id", OperatorEnum.IsNotNull, null));

        if (stats == null) return null;

        await Redis.Set(RedisKey.AllUsers(), stats);

        return stats;
    }

    public async Task UpdateUserStats(UserStats stats)
    {
        stats = await SetUserRank(stats);
        stats = await _orm.UpdateAsync(stats);

        await Redis.Set(RedisKey.UserStats(stats.UserId, stats.GameMode), stats);
    }

    public async Task InsertUserStats(UserStats stats)
    {
        stats = await SetUserRank(stats);
        stats = await _orm.InsertAsync(stats);

        await Redis.Set(RedisKey.UserStats(stats.UserId, stats.GameMode), stats);
    }

    public async Task InsertScore(Score score)
    {
        score = await _orm.InsertAsync(score);
        await Redis.Set(RedisKey.Score(score.Id), score);
    }

    public async Task<Score?> GetScore(int id)
    {
        var cachedScore = await Redis.Get<Score?>(RedisKey.Score(id));

        if (cachedScore != null) return cachedScore;

        var exp = new Expr("Id", OperatorEnum.Equals, id);
        var score = await _orm.SelectFirstAsync<Score?>(exp);

        if (score == null) throw new Exception("Score not found");

        await Redis.Set(RedisKey.Score(id), score);

        return score;
    }

    public async Task<List<Score>> GetUserBestScores(int userId, GameMode mode, int excludeBeatmapId = -1,
        int? limit = null)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode", OperatorEnum.Equals, (int)mode)
            .PrependAnd("BeatmapId", OperatorEnum.NotEquals, excludeBeatmapId);

        var scores = await _orm.SelectManyAsync<Score>(exp,
        [
            new ResultOrder("PerformancePoints", OrderDirectionEnum.Descending)
        ]);

        var bestScores = scores.GroupBy(x => x.BeatmapId).Select(x => x.First()).ToList();

        return limit == null ? bestScores : bestScores.Take(limit.Value).ToList();
    }

    public async Task<List<Score>> GetUserScores(int userId, GameMode mode, ScoreTableType type)
    {
        var exp = new Expr("GameMode", OperatorEnum.Equals, (int)mode);

        switch (type)
        {
            case ScoreTableType.Best:
                exp = exp.PrependAnd("UserId", OperatorEnum.Equals, userId);
                break;
            case ScoreTableType.Recent:
                exp = exp.PrependAnd("UserId", OperatorEnum.Equals, userId);
                break;
            case ScoreTableType.Top:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        var scores = await _orm.SelectManyAsync<Score>(exp,
        [
            type switch
            {
                ScoreTableType.Best => new ResultOrder("PerformancePoints", OrderDirectionEnum.Descending),
                ScoreTableType.Recent => new ResultOrder("WhenPlayed", OrderDirectionEnum.Descending),
                ScoreTableType.Top => new ResultOrder("TotalScore", OrderDirectionEnum.Descending),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            }
        ]);

        switch (type)
        {
            case ScoreTableType.Top:
                scores = scores.GroupBy(x => x.BeatmapId).Select(x => x.First()).Where(x => x.UserId == userId)
                    .ToList();
                break;
            case ScoreTableType.Best:
                scores = scores.GroupBy(x => x.BeatmapId).Select(x => x.First()).ToList();
                break;
        }

        return scores;
    }

    public async Task<Score?> GetUserLastScore(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);

        var scores = await _orm.SelectManyAsync<Score>(exp);

        return scores.Count == 0 ? null : scores.OrderBy(x => x.WhenPlayed).ToList().Last();
    }

    public async Task<List<Score>> GetBeatmapScores(string beatmapHash, GameMode gameMode,
        LeaderboardType type = LeaderboardType.Global, Mods mods = Mods.None, User? user = null)
    {
        var exp = new Expr("BeatmapHash", OperatorEnum.Equals, beatmapHash).PrependAnd("GameMode", OperatorEnum.Equals,
            (int)gameMode);

        if (type is LeaderboardType.GlobalWithMods) exp.PrependAnd("Mods", OperatorEnum.Equals, (int)mods);
        if (type is LeaderboardType.Friends) exp.PrependAnd("UserId", OperatorEnum.In, user?.FriendsList);

        var scores = await _orm.SelectManyAsync<Score>(exp);
        scores = scores.GetSortedScoresByScore();

        foreach (var score in scores.ToList())
        {
            var scoreUser = await GetUser(score.UserId);

            if ((type == LeaderboardType.Country && scoreUser?.Country != user?.Country) ||
                scoreUser?.IsRestricted == true) scores.Remove(score);
        }

        return scores;
    }

    public async Task<UserStatsSnapshot> GetUserStatsSnapshot(int userId, GameMode mode)
    {
        var cachedSnapshot = await Redis.Get<UserStatsSnapshot>(RedisKey.UserStatsSnapshot(userId, mode));
        if (cachedSnapshot != null) return cachedSnapshot;

        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode", OperatorEnum.Equals,
            (int)mode);
        var snapshot = await _orm.SelectFirstAsync<UserStatsSnapshot?>(exp);

        if (snapshot == null)
        {
            snapshot = new UserStatsSnapshot
            {
                UserId = userId,
                GameMode = mode
            };
            snapshot = await InsertUserStatsSnapshot(snapshot);
        }

        await Redis.Set(RedisKey.UserStatsSnapshot(userId, mode), snapshot);

        return snapshot;
    }

    public async Task UpdateUserStatsSnapshot(UserStatsSnapshot snapshot)
    {
        snapshot = await _orm.UpdateAsync(snapshot);
        await Redis.Set(RedisKey.UserStatsSnapshot(snapshot.UserId, snapshot.GameMode), snapshot);
    }

    public async Task<UserStatsSnapshot> InsertUserStatsSnapshot(UserStatsSnapshot snapshot)
    {
        snapshot = await _orm.InsertAsync(snapshot);
        await Redis.Set(RedisKey.UserStatsSnapshot(snapshot.UserId, snapshot.GameMode), snapshot);
        return snapshot;
    }

    public async Task<List<Medal>> GetMedals(GameMode mode)
    {
        var cachedMedals = await Redis.Get<List<Medal>>(RedisKey.AllMedals(mode));
        if (cachedMedals != null) return cachedMedals;

        var exp = new Expr("GameMode", OperatorEnum.Equals, (int)mode).PrependOr("GameMode", OperatorEnum.IsNull, null);

        var medals = await _orm.SelectManyAsync<Medal>(exp);
        if (medals == null) return [];

        await Redis.Set(RedisKey.AllMedals(mode), medals);

        return medals;
    }

    public async Task<Medal?> GetMedal(int medalId)
    {
        var cachedMedal = await Redis.Get<Medal?>(RedisKey.Medal(medalId));
        if (cachedMedal != null) return cachedMedal;

        var exp = new Expr("Id", OperatorEnum.Equals, medalId);

        var medal = await _orm.SelectFirstAsync<Medal?>(exp);
        if (medal == null) return null;

        await Redis.Set(RedisKey.Medal(medalId), medal);

        return medal;
    }

    public async Task<byte[]?> GetMedalImage(int medalFileId, bool isHighRes = false)
    {
        var cachedRecord = await Redis.Get<MedalFile>(RedisKey.MedalImageRecord(medalFileId));
        byte[]? file;

        if (cachedRecord != null)
        {
            file = await LocalStorage.ReadFileAsync(cachedRecord.Path);
            return file;
        }

        var exp = new Expr("Id", OperatorEnum.Equals, medalFileId);
        var record = await _orm.SelectFirstAsync<MedalFile?>(exp);

        if (record == null)
            return null;

        file = await LocalStorage.ReadFileAsync(isHighRes ? record.Path.Replace(".png", "@2x.png") : record.Path);
        if (file == null)
            return null;

        await Redis.Set(RedisKey.MedalImageRecord(medalFileId), record);

        return file;
    }

    public async Task<List<UserMedals>> GetUserMedals(int userId, GameMode? mode = null)
    {
        var cachedMedals = await Redis.Get<List<UserMedals>>(RedisKey.UserMedals(userId, mode));
        if (cachedMedals != null) return cachedMedals;

        var exp = new Expr("UserId", OperatorEnum.Equals, userId);
        var userMedals = await _orm.SelectManyAsync<UserMedals>(exp);

        if (userMedals == null) return [];

        if (mode != null)
        {
            var modeMedals = await GetMedals(mode.Value);
            userMedals = userMedals.Where(x => modeMedals.Any(y => y.Id == x.MedalId)).ToList();
        }

        await Redis.Set(RedisKey.UserMedals(userId, mode), userMedals);

        return userMedals;
    }

    public async Task UnlockMedal(int userId, int medalId)
    {
        var userMedal = new UserMedals
        {
            UserId = userId,
            MedalId = medalId
        };

        await _orm.InsertAsync(userMedal);
        await Redis.Remove(RedisKey.UserMedals(userId));
    }

    public async Task<bool> IsRestricted(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);
        var restriction = await _orm.SelectFirstAsync<Restriction?>(exp);
        if (restriction == null)
            return false;

        if (restriction.ExpiryDate >= DateTime.UtcNow)
            return true;

        await UnrestrictPlayer(userId, restriction);

        return false;
    }

    public async Task UnrestrictPlayer(int userId, Restriction? restriction = null)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);

        restriction ??= await _orm.SelectFirstAsync<Restriction?>(exp);

        if (restriction == null)
            return;

        await _orm.DeleteAsync(restriction);

        var user = await GetUser(userId);
        if (user == null)
            return;

        user.IsRestricted = false;
        await UpdateUser(user);
    }

    public async Task RestrictPlayer(int userId, int executorId, string reason, TimeSpan? expiresAfter = null)
    {
        var restriction = new Restriction
        {
            UserId = userId,
            ExecutorId = executorId,
            Reason = reason,
            ExpiryDate = DateTime.UtcNow.Add(expiresAfter ?? TimeSpan.FromDays(365))
        };


        var user = await GetUser(userId);
        if (user == null)
            return;

        if (user.Privilege >= UserPrivileges.Admin)
            return;

        user.IsRestricted = true;
        await UpdateUser(user);
        await _orm.InsertAsync(restriction);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var session = sessions.GetSession(userId: userId);
        session?.SendRestriction(reason);
    }

    public async Task<int> GetLeaderboardRank(Score score)
    {
        var exp = new Expr("BeatmapHash", OperatorEnum.Equals, score.BeatmapHash).PrependAnd("GameMode",
            OperatorEnum.Equals, (int)score.GameMode);
        var scores = await _orm.SelectManyAsync<Score>(exp);

        return scores.GetSortedScoresByScore().FindIndex(x => x.Id == score.Id) + 1;
    }

    public async Task<List<int>> GetMostPlayedBeatmapsIds(GameMode? gameMode, int page = 1, int limit = 100)
    {
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);
        if (gameMode != null) exp = exp.PrependAnd("GameMode", OperatorEnum.Equals, (int)gameMode);

        var scores = await _orm.SelectManyAsync<Score>(exp);

        var uniqueScores = scores
            .GroupBy(x => x.BeatmapId)
            .OrderByDescending(x => x.Count())
            .Skip((page - 1) * limit)
            .Take(limit);

        return uniqueScores.Select(x => x.Key).ToList();
    }

    public async Task<long> GetTotalUsers()
    {
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);
        return await _orm.CountAsync<User>(exp);
    }

    public async Task SetBeatmapFile(int beatmapId, byte[] beatmap)
    {
        var filePath = $"{DataPath}Files/Beatmaps/{beatmapId}.osu";
        await File.WriteAllBytesAsync(filePath, beatmap);

        var record = new BeatmapFile
        {
            BeatmapId = beatmapId,
            Path = filePath
        };

        record = await _orm.InsertAsync(record);
        await Redis.Set(RedisKey.BeatmapRecord(beatmapId), record);
    }

    public async Task<byte[]?> GetBeatmapFile(int beatmapId)
    {
        var cachedRecord = await Redis.Get<BeatmapFile?>(RedisKey.BeatmapRecord(beatmapId));
        byte[]? file;

        if (cachedRecord != null)
        {
            file = await LocalStorage.ReadFileAsync(cachedRecord.Path);
            return file;
        }

        var exp = new Expr("BeatmapId", OperatorEnum.Equals, beatmapId);
        var record = await _orm.SelectFirstAsync<BeatmapFile?>(exp);

        if (record == null) return null;

        file = await LocalStorage.ReadFileAsync(record.Path);

        if (file == null) return null;

        await Redis.Set(RedisKey.BeatmapRecord(beatmapId), record);

        return file;
    }

    public async Task<bool> SetAvatar(int id, byte[] avatar)
    {
        var filePath = $"{DataPath}Files/Avatars/{id}.png";

        if (!await LocalStorage.WriteFileAsync(filePath, ImageTools.ResizeImage(avatar, 256, 256)))
            return false;

        var record = new UserFile
        {
            OwnerId = id,
            Path = filePath,
            Type = FileType.Avatar
        };

        record = await _orm.WriteRecordAsync(record);
        if (record == null)
            return false;

        await Redis.Set(RedisKey.AvatarRecord(id), record);
        return true;
    }

    public async Task<byte[]?> GetAvatar(int userId, bool fallToDefault = true)
    {
        var cachedRecord = await Redis.Get<UserFile>(RedisKey.AvatarRecord(userId));
        byte[]? file;

        if (cachedRecord != null)
        {
            file = await LocalStorage.ReadFileAsync(cachedRecord.Path);
            return file;
        }

        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId);
        exp.PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Avatar);

        var record = await _orm.SelectFirstAsync<UserFile?>(exp);

        if (record == null && !fallToDefault) return null;

        var filePath = record?.Path ?? $"{DataPath}Files/Avatars/Default.png";
        file = await LocalStorage.ReadFileAsync(filePath);
        if (file == null)
            return null;

        await Redis.Set(RedisKey.AvatarRecord(userId), record);

        return file;
    }

    public async Task<int> SetScreenshot(int userId, byte[] screenshot)
    {
        var filePath = $"{DataPath}Files/Screenshot/{userId}-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.jpg";
        await File.WriteAllBytesAsync(filePath, screenshot);

        var record = new UserFile
        {
            OwnerId = userId,
            Path = filePath,
            Type = FileType.Screenshot
        };

        record = await _orm.InsertAsync(record);
        await Redis.Set(RedisKey.ScreenshotRecord(userId), record);

        return record.Id;
    }

    public async Task<byte[]?> GetScreenshot(int screenshotId)
    {
        var cachedRecord = await Redis.Get<UserFile>(RedisKey.ScreenshotRecord(screenshotId));
        byte[]? file;

        if (cachedRecord != null)
        {
            file = await LocalStorage.ReadFileAsync(cachedRecord.Path);
            return file;
        }

        var exp = new Expr("Id", OperatorEnum.Equals, screenshotId);
        var record = await _orm.SelectFirstAsync<UserFile?>(exp);

        if (record == null)
            return null;

        file = await LocalStorage.ReadFileAsync(record.Path);
        if (file == null)
            return null;

        await Redis.Set(RedisKey.ScreenshotRecord(screenshotId), record);

        return file;
    }

    public async Task<bool> SetBanner(int userId, byte[] banner)
    {
        var filePath = $"{DataPath}Files/Banners/{userId}.png";

        if (!await LocalStorage.WriteFileAsync(filePath, ImageTools.ResizeImage(banner, 1280, 320)))
            return false;

        var record = new UserFile
        {
            OwnerId = userId,
            Path = filePath,
            Type = FileType.Banner
        };

        record = await _orm.WriteRecordAsync(record);
        if (record == null)
            return false;

        await Redis.Set(RedisKey.BannerRecord(userId), record);
        return true;
    }

    public async Task<byte[]?> GetBanner(int userId, bool fallToDefault = true)
    {
        var cachedRecord = await Redis.Get<UserFile>(RedisKey.BannerRecord(userId));
        byte[]? file;

        if (cachedRecord != null)
        {
            file = await LocalStorage.ReadFileAsync(cachedRecord.Path);
            return file;
        }

        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId);
        exp.PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Banner);
        var record = await _orm.SelectFirstAsync<UserFile?>(exp);

        if (record == null && !fallToDefault) return null;

        var filePath = record?.Path ?? $"{DataPath}Files/Banners/Default.png";
        file = await LocalStorage.ReadFileAsync(filePath);
        if (file == null)
            return null;

        await Redis.Set(RedisKey.BannerRecord(userId), record);

        return file;
    }

    public async Task<UserFile> UploadReplay(int userId, IFormFile replay)
    {
        var fileName = $"{userId}-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.osr";
        var filePath = $"{DataPath}Files/Replays/{fileName}";

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite);
        await replay.CopyToAsync(stream);
        stream.Close();

        var record = new UserFile
        {
            OwnerId = userId,
            Path = filePath,
            Type = FileType.Replay
        };

        record = await _orm.InsertAsync(record);
        await Redis.Set(RedisKey.ReplayRecord(record.Id), record);
        return record;
    }

    public async Task<byte[]?> GetReplay(int replayId)
    {
        var cachedRecord = await Redis.Get<UserFile>(RedisKey.ReplayRecord(replayId));
        byte[]? file;

        if (cachedRecord != null)
        {
            file = await LocalStorage.ReadFileAsync(cachedRecord.Path);
            return file;
        }

        var exp = new Expr("Id", OperatorEnum.Equals, replayId);
        var record = await _orm.SelectFirstAsync<UserFile?>(exp);

        if (record == null)
            return null;

        file = await LocalStorage.ReadFileAsync(record.Path);
        if (file == null)
            return null;

        await Redis.Set(RedisKey.ReplayRecord(replayId), record);

        return file;
    }

    public async Task<long> GetUserRank(int userId, GameMode mode)
    {
        var rank = await Redis.SortedSetRank(RedisKey.LeaderboardGlobal(mode), userId);

        if (!rank.HasValue)
        {
            rank = await Redis.SortedSetRank(RedisKey.LeaderboardGlobal(mode), userId);
            await SetUserRank(userId, mode);
        }

        return rank.HasValue ? rank.Value + 1 : -1;
    }

    public async Task<long> GetUserCountryRank(int userId, GameMode mode)
    {
        var user = await GetUser(userId);
        if (user == null) return -1;

        var rank = await Redis.SortedSetRank(RedisKey.LeaderboardCountry(mode, (CountryCodes)user.Country), userId);

        if (!rank.HasValue)
        {
            rank = await Redis.SortedSetRank(RedisKey.LeaderboardCountry(mode, (CountryCodes)user.Country), userId);
            await SetUserRank(userId, mode);
        }

        return rank.HasValue ? rank.Value + 1 : -1;
    }

    private async Task<UserStats> SetUserRank(int userId, GameMode mode)
    {
        var stats = await GetUserStats(userId, mode);
        if (stats == null) throw new Exception("User stats not found for user " + userId);

        stats = await SetUserRank(stats);
        return stats;
    }

    private async Task<UserStats> SetUserRank(UserStats stats)
    {
        var user = await GetUser(stats.UserId);

        await Redis.SortedSetAdd(RedisKey.LeaderboardGlobal(stats.GameMode), stats.UserId, stats.PerformancePoints);
        await Redis.SortedSetAdd(RedisKey.LeaderboardCountry(stats.GameMode, (CountryCodes)user.Country), stats.UserId,
            stats.PerformancePoints);

        var newRank = await GetUserRank(stats.UserId, stats.GameMode);
        var newCountryRank = await GetUserCountryRank(stats.UserId, stats.GameMode);

        if (newRank < (stats.BestGlobalRank ?? long.MaxValue))
        {
            stats.BestGlobalRankDate = DateTime.UtcNow;
            stats.BestGlobalRank = newRank;
        }

        if (newCountryRank < (stats.BestCountryRank ?? long.MaxValue))
        {
            stats.BestCountryRankDate = DateTime.UtcNow;
            stats.BestCountryRank = newCountryRank;
        }

        return stats;
    }


    private async Task RemoveUserRank(int userId, GameMode mode)
    {
        var user = await GetUser(userId);
        if (user == null) return;

        await Redis.SortedSetRemove(RedisKey.LeaderboardGlobal(mode), userId);
        await Redis.SortedSetRemove(RedisKey.LeaderboardCountry(mode, (CountryCodes)user.Country), userId);
    }

    private async Task SetAllUserRanks(GameMode mode)
    {
        var usersStats = await GetAllUserStats(mode, LeaderboardSortType.Pp);
        if (usersStats == null) return;

        foreach (var stats in usersStats)
            await UpdateUserStats(stats);
    }

    public async Task InitializeBotInDatabase()
    {
        var isBotInitialized = await GetUser(username: Configuration.BotUsername, useCache: false);

        if (isBotInitialized != null) return;

        var bot = new User
        {
            Username = Configuration.BotUsername,
            Country = (short)CountryCodes.AQ, // Antarctica, because our bot is "cool" :D
            Privilege = UserPrivileges.User,
            RegisterDate = DateTime.Now,
            Passhash = "12345678",
            Email = "bot@mail.com",
            IsRestricted = true // Bot is restricted by default to prevent users from logging in as it
        };

        bot = await InsertUser(bot);

        if (bot == null) throw new Exception("Failed to insert bot into the database");

        var botAvatar = await File.ReadAllBytesAsync($"{DataPath}Files/Assets/BotAvatar.png");
        await SetAvatar(bot.Id, botAvatar);
    }
}