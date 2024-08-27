using DatabaseWrapper.Core;
using ExpressionTree;
using osu.Shared;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Data;

public sealed class SunriseDb
{
    private const string DataPath = "./Data/";
    private const string Database = "sunrise.db";
    private readonly WatsonORM _orm = new(new DatabaseSettings(DataPath + Database));

    public SunriseDb(RedisRepository redis)
    {
        Redis = redis;

        _orm.InitializeDatabase();
        _orm.InitializeTables([typeof(User), typeof(UserStats), typeof(UserFile), typeof(BeatmapFile), typeof(Score)]);
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

    public async Task<User?> GetUser(int? id = null, string? username = null, string? email = null, string? passhash = null, bool useCache = true)
    {
        var cachedUser = await Redis.Get<User?>([RedisKey.UserById(id ?? -1), RedisKey.UserByUsername(username ?? ""), RedisKey.UserByEmail(email ?? "")]);

        if (cachedUser != null && useCache)
        {
            return cachedUser;
        }

        if (passhash != null && id == null && username == null && email == null)
        {
            throw new Exception("Passhash provided without any other parameters");
        }

        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);
        if (id != null) exp = exp.PrependAnd("Id", OperatorEnum.Equals, id);
        if (username != null) exp = exp.PrependAnd("Username", OperatorEnum.Equals, username);
        if (email != null) exp = exp.PrependAnd("Email", OperatorEnum.Equals, email);
        if (passhash != null) exp = exp.PrependAnd("Passhash", OperatorEnum.Equals, passhash);

        var user = await _orm.SelectFirstAsync<User?>(exp);

        if (user == null)
        {
            return null;
        }

        await Redis.Set([RedisKey.UserById(user.Id), RedisKey.UserByUsername(user.Username), RedisKey.UserByEmail(user.Email)], user);

        return user;
    }

    public async Task UpdateUser(User user)
    {
        await _orm.UpdateAsync(user);

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();
        var session = sessions.GetSession(user.Id);

        session?.UpdateUser(user);

        await Redis.Set([RedisKey.UserById(user.Id), RedisKey.UserByUsername(user.Username), RedisKey.UserByEmail(user.Email)], user);
    }

    public async Task<UserStats> InsertUserStats(UserStats stats)
    {
        stats = await _orm.InsertAsync(stats);
        await SetUserRank(stats.UserId, stats.GameMode);
        await Redis.Set(RedisKey.UserStats(stats.UserId, stats.GameMode), stats);

        return stats;
    }

    public async Task<UserStats> GetUserStats(int userId, GameMode mode, bool useCache = true)
    {
        var cachedStats = await Redis.Get<UserStats?>(RedisKey.UserStats(userId, mode));

        if (cachedStats != null && useCache)
        {
            return cachedStats;
        }

        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode", OperatorEnum.Equals, (int)mode);
        var stats = await _orm.SelectFirstAsync<UserStats?>(exp);

        if (stats == null)
        {
            throw new Exception("User stats not found");
        }

        await Redis.Set(RedisKey.UserStats(userId, mode), stats);

        return stats;
    }

    public async Task UpdateUserStats(UserStats stats)
    {
        await _orm.UpdateAsync(stats);
        await SetUserRank(stats.UserId, stats.GameMode);
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

        if (cachedScore != null)
        {
            return cachedScore;
        }

        var exp = new Expr("Id", OperatorEnum.Equals, id);
        var score = await _orm.SelectFirstAsync<Score?>(exp);

        if (score == null)
        {
            throw new Exception("Score not found");
        }

        await Redis.Set(RedisKey.Score(id), score);

        return score;
    }

    public async Task<List<Score>> GetUserBestScores(int userId, GameMode mode, int excludeBeatmapId = -1)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode", OperatorEnum.Equals, (int)mode).PrependAnd("BeatmapId", OperatorEnum.NotEquals, excludeBeatmapId);

        var scores = await _orm.SelectManyAsync<Score>(exp,
        [
            new ResultOrder("TotalScore", OrderDirectionEnum.Descending)
        ]);

        return scores.GroupBy(x => x.BeatmapId).Select(x => x.First()).ToList();
    }

    public async Task<Score?> GetUserLastScore(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);

        var scores = await _orm.SelectManyAsync<Score>(exp);

        return scores.Count == 0 ? null : scores.OrderBy(x => x.WhenPlayed).ToList().Last();
    }

    public async Task<List<Score>> GetBeatmapScores(string beatmapHash, GameMode gameMode, LeaderboardType type = LeaderboardType.Global, Mods mods = Mods.None, User? user = null)
    {
        var exp = new Expr("BeatmapHash", OperatorEnum.Equals, beatmapHash).PrependAnd("GameMode", OperatorEnum.Equals, (int)gameMode);

        if (type is LeaderboardType.GlobalWithMods) exp.PrependAnd("Mods", OperatorEnum.Equals, (int)mods);
        if (type is LeaderboardType.Friends) exp.PrependAnd("UserId", OperatorEnum.In, user?.FriendsList);

        var scores = await _orm.SelectManyAsync<Score>(exp);
        scores = ScoresHelper.GetSortedScores(scores);

        foreach (var score in scores.ToList())
        {
            var scoreUser = await GetUser(score.UserId);

            if (type == LeaderboardType.Country && scoreUser?.Country != user?.Country || scoreUser?.IsRestricted == true)
            {
                scores.Remove(score);
            }
        }

        return scores;
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

        if (cachedRecord != null)
        {
            return await File.ReadAllBytesAsync(cachedRecord.Path);
        }

        var exp = new Expr("BeatmapId", OperatorEnum.Equals, beatmapId);
        var record = await _orm.SelectFirstAsync<BeatmapFile?>(exp);

        if (record == null)
        {
            return null;
        }

        var file = await File.ReadAllBytesAsync(record.Path);

        await Redis.Set(RedisKey.BeatmapRecord(beatmapId), record);
        return file;
    }

    public async Task SetAvatar(int id, byte[] avatar)
    {
        var filePath = $"{DataPath}Files/Avatars/{id}.png";
        await File.WriteAllBytesAsync(filePath, ImageTools.ResizeImage(avatar, 256, 256));

        var record = new UserFile
        {
            OwnerId = id,
            Path = filePath,
            Type = FileType.Avatar
        };

        record = await _orm.InsertAsync(record);
        await Redis.Set(RedisKey.AvatarRecord(id), record);
    }

    public async Task<byte[]> GetAvatar(int userId, bool fallToDefault = true)
    {
        var cachedRecord = await Redis.Get<UserFile>(RedisKey.AvatarRecord(userId));

        if (cachedRecord != null)
        {
            return await File.ReadAllBytesAsync(cachedRecord.Path);
        }

        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId);
        exp.PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Avatar);

        var record = await _orm.SelectFirstAsync<UserFile?>(exp);

        if (record == null && !fallToDefault)
        {
            throw new Exception("Avatar not found in database");
        }

        var filePath = record?.Path ?? $"{DataPath}Files/Avatars/Default.png";
        var file = await File.ReadAllBytesAsync(filePath);

        if (file == null)
        {
            throw new Exception("Avatar not found");
        }

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

        if (cachedRecord != null)
        {
            return await File.ReadAllBytesAsync(cachedRecord.Path);
        }

        var exp = new Expr("Id", OperatorEnum.Equals, screenshotId);
        var record = await _orm.SelectFirstAsync<UserFile?>(exp);

        if (record == null)
        {
            return null;
        }

        var file = await File.ReadAllBytesAsync(record.Path);
        await Redis.Set(RedisKey.ScreenshotRecord(screenshotId), record);

        return file;
    }

    public async Task SetBanner(int userId, byte[] banner)
    {
        var filePath = $"{DataPath}Files/Banners/{userId}.png";
        await File.WriteAllBytesAsync(filePath, ImageTools.ResizeImage(banner, 1280, 256));

        var record = new UserFile
        {
            OwnerId = userId,
            Path = filePath,
            Type = FileType.Banner
        };

        record = await _orm.InsertAsync(record);
        await Redis.Set(RedisKey.BannerRecord(userId), record);
    }

    public async Task<byte[]> GetBanner(int userId, bool fallToDefault = true)
    {
        var cachedRecord = await Redis.Get<UserFile>(RedisKey.BannerRecord(userId));

        if (cachedRecord != null)
        {
            return await File.ReadAllBytesAsync(cachedRecord.Path);
        }

        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId);
        exp.PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Banner);
        var record = await _orm.SelectFirstAsync<UserFile?>(exp);

        if (record == null && !fallToDefault)
        {
            throw new Exception("Banner not found in database");
        }

        var filePath = record?.Path ?? $"{DataPath}Files/Banners/Default.png";
        var file = await File.ReadAllBytesAsync(filePath);

        if (file == null)
        {
            throw new Exception("Banner not found");
        }

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

    public async Task<byte[]> GetReplay(int replayId)
    {
        var cachedRecord = await Redis.Get<UserFile>(RedisKey.ReplayRecord(replayId));

        if (cachedRecord != null)
        {
            return await File.ReadAllBytesAsync(cachedRecord.Path);
        }

        var exp = new Expr("Id", OperatorEnum.Equals, replayId);
        var record = await _orm.SelectFirstAsync<UserFile?>(exp);

        if (record == null)
        {
            throw new Exception("Replay not found");
        }

        var file = await File.ReadAllBytesAsync(record.Path);
        await Redis.Set(RedisKey.ReplayRecord(replayId), record);

        return file;
    }

    public async Task<long> GetUserRank(int userId, GameMode mode)
    {
        var rank = await Redis.SortedSetRank(RedisKey.LeaderboardGlobal(mode), userId);

        if (!rank.HasValue)
        {
            await SetUserRank(userId, mode);
            rank = await Redis.SortedSetRank(RedisKey.LeaderboardGlobal(mode), userId);
        }

        return rank.HasValue ? rank.Value + 1 : -1;
    }

    public async Task SetUserRank(int userId, GameMode mode)
    {
        var userStats = await GetUserStats(userId, mode, false);
        await Redis.SortedSetAdd(RedisKey.LeaderboardGlobal(mode), userId, userStats.PerformancePoints);
    }

    public async Task RemoveUserRank(int userId, GameMode mode)
    {
        await Redis.SortedSetRemove(RedisKey.LeaderboardGlobal(mode), userId);
    }

    public async Task InitializeBotInDatabase()
    {
        var isBotInitialized = await GetUser(username: Configuration.BotUsername, useCache: false);

        if (isBotInitialized != null)
        {
            return;
        }

        var bot = new User
        {
            Username = Configuration.BotUsername,
            Country = (short)CountryCodes.AQ, // Antarctica, because our bot is "cool" :D
            Privilege = PlayerRank.SuperMod,
            RegisterDate = DateTime.Now,
            Passhash = "12345678",
            Email = "bot@mail.com",
            IsRestricted = true // Bot is restricted by default to prevent users from logging in as it
        };

        bot = await InsertUser(bot);

        if (bot == null)
        {
            throw new Exception("Failed to insert bot into the database");
        }

        var botAvatar = await File.ReadAllBytesAsync($"{DataPath}Files/Assets/BotAvatar.png");
        await SetAvatar(bot.Id, botAvatar);
    }
}