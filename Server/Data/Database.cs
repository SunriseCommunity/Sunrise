using DatabaseWrapper.Core;
using ExpressionTree;
using osu.Shared;
using StackExchange.Redis;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Sqlite;
using RedisKey = Sunrise.Server.Types.Enums.RedisKey;

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
        _orm.InitializeTables([typeof(User), typeof(UserStats), typeof(UserFile), typeof(Score)]);
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

        await Redis.Set(string.Format(RedisKey.User, user.Id), user);

        return user;
    }

    public async Task<User?> GetUser(int? id = null, string? username = null, string? token = null, string? email = null)
    {
        var cachedUser = await Redis.Get<User?>(string.Format(RedisKey.User, id));

        if (cachedUser != null && id != null)
        {
            return cachedUser;
        }

        var exp = new Expr("Id", OperatorEnum.Equals, id ?? -1);
        if (username != null) exp.PrependOr(new Expr("Username", OperatorEnum.Equals, username));
        if (token != null) exp.PrependOr(new Expr("Passhash", OperatorEnum.Equals, token));
        if (email != null) exp.PrependOr(new Expr("Email", OperatorEnum.Equals, email));

        var user = await _orm.SelectFirstAsync<User?>(exp);

        if (user == null)
        {
            return null;
        }

        await Redis.Set(string.Format(RedisKey.User, id), user);

        return user;
    }

    public async Task<User> UpdateUser(User user)
    {
        await _orm.UpdateAsync(user);
        await Redis.Set(string.Format(RedisKey.User, user.Id), user);

        return user;
    }

    public async Task<UserStats> InsertUserStats(UserStats stats)
    {
        stats = await _orm.InsertAsync(stats);
        await SetUserRank(stats.UserId, stats.GameMode);
        await Redis.Set(string.Format(RedisKey.UserStats, stats.UserId, (int)stats.GameMode), stats);

        return stats;
    }

    public async Task<UserStats> GetUserStats(int id, GameMode mode)
    {
        var cachedStats = await Redis.Get<UserStats?>(string.Format(RedisKey.UserStats, id, (int)mode));

        if (cachedStats != null)
        {
            return cachedStats;
        }

        var exp = new Expr("UserId", OperatorEnum.Equals, id).PrependAnd("GameMode", OperatorEnum.Equals, (int)mode);
        var stats = await _orm.SelectFirstAsync<UserStats?>(exp);

        if (stats == null)
        {
            throw new Exception("User stats not found");
        }

        await Redis.Set(string.Format(RedisKey.UserStats, id, (int)mode), stats);

        return stats;
    }

    public async Task<UserStats> UpdateUserStats(UserStats stats)
    {
        await _orm.UpdateAsync(stats);
        await Redis.Set(string.Format(RedisKey.UserStats, stats.UserId, (int)stats.GameMode), stats);

        await SetUserRank(stats.UserId, stats.GameMode);
        return stats;
    }

    public async Task<Score> InsertScore(Score score)
    {
        score = await _orm.InsertAsync(score);
        await Redis.Set(string.Format(RedisKey.Score, score.Id), score);
        return score;
    }

    public async Task<Score?> GetScore(int id)
    {
        var cachedScore = await Redis.Get<Score?>(string.Format(RedisKey.Score, id));

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

        await Redis.Set(string.Format(RedisKey.Score, id), score);

        return score;
    }

    public async Task<List<Score>> GetUserBestScores(int userId, GameMode mode, int excludeBeatmapId = -1)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode", OperatorEnum.Equals, (int)mode).PrependAnd("BeatmapId", OperatorEnum.NotEquals, excludeBeatmapId);

        var scores = await _orm.SelectManyAsync<Score>(exp,
            new ResultOrder[]
            {
                new("TotalScore", OrderDirectionEnum.Descending)
            });

        return scores.GroupBy(x => x.BeatmapId).Select(x => x.First()).ToList();
    }

    public async Task<List<Score>> GetBeatmapScores(string beatmapHash, GameMode gameMode, bool pbOnly = true)
    {
        var exp = new Expr("BeatmapHash", OperatorEnum.Equals, beatmapHash).PrependAnd("GameMode", OperatorEnum.Equals, (int)gameMode);
        var scores = await _orm.SelectManyAsync<Score>(exp);

        return pbOnly ? ScoresHelper.GetSortedScores(scores) : scores;
    }

    public async Task SetAvatar(int id, byte[] avatar)
    {
        var path = $"{DataPath}Files/Avatars/{id}.png";
        await File.WriteAllBytesAsync(path, avatar);

        var file = new UserFile
        {
            OwnerId = id,
            Path = path,
            Type = FileType.Avatar,
            CreatedAt = DateTime.UtcNow
        };

        await _orm.InsertAsync(file);
        await Redis.Set(string.Format(RedisKey.Avatar, id), avatar);
    }

    public async Task<byte[]> GetAvatar(int id)
    {
        var cachedAvatar = await Redis.Get<byte[]>(string.Format(RedisKey.Avatar, id));

        if (cachedAvatar != null)
        {
            return cachedAvatar;
        }

        if (id == int.MaxValue)
        {
            var botAvatar = await File.ReadAllBytesAsync($"{DataPath}Files/Avatars/Bot.png");
            await Redis.Set(string.Format(RedisKey.Avatar, id), botAvatar);
            return botAvatar;
        }

        var exp = new Expr("OwnerId", OperatorEnum.Equals, id);
        exp.PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Avatar);

        var file = await _orm.SelectFirstAsync<UserFile?>(exp);

        var avatarPath = file?.Path ?? $"{DataPath}Files/Avatars/Default.png";
        var avatar = await File.ReadAllBytesAsync(avatarPath);

        if (avatar == null)
        {
            throw new Exception("Avatar not found");
        }

        await Redis.Set(string.Format(RedisKey.Avatar, id), avatar);

        return avatar;
    }

    public async Task SetBanner(int userId, byte[] banner)
    {
        var path = $"{DataPath}Files/Banners/{userId}.png";
        await File.WriteAllBytesAsync(path, banner);

        var file = new UserFile
        {
            OwnerId = userId,
            Path = path,
            Type = FileType.Banner,
            CreatedAt = DateTime.UtcNow
        };

        await _orm.InsertAsync(file);
        await Redis.Set(string.Format(RedisKey.Banner, userId), banner);
    }

    public async Task<byte[]> GetBanner(int userId)
    {
        var cachedBanner = await Redis.Get<byte[]?>(string.Format(RedisKey.Banner, userId));

        if (cachedBanner != null)
        {
            return cachedBanner;
        }

        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId);
        exp.PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Banner);

        var file = await _orm.SelectFirstAsync<UserFile?>(exp);

        var bannerPath = file?.Path ?? $"{DataPath}Files/Banners/Default.png";
        var banner = await File.ReadAllBytesAsync(bannerPath);

        if (banner == null)
        {
            throw new Exception("Banner not found");
        }

        await Redis.Set(string.Format(RedisKey.Banner, userId), banner);

        return banner;
    }

    public async Task<UserFile> UploadReplay(int userId, IFormFile replay)
    {
        var fileName = $"{userId}-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.osr";
        var path = $"{DataPath}Files/Replays/{fileName}";

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        await replay.CopyToAsync(stream);
        stream.Close();

        var file = new UserFile
        {
            OwnerId = userId,
            Path = path,
            Type = FileType.Replay,
            CreatedAt = DateTime.UtcNow
        };

        file = await _orm.InsertAsync(file);
        await Redis.Set(string.Format(RedisKey.Replay, userId), await File.ReadAllBytesAsync(path));

        return file;
    }

    public async Task<byte[]> GetReplay(int scoreId)
    {
        var cachedReplay = await Redis.Get<byte[]>(string.Format(RedisKey.Replay, scoreId));

        if (cachedReplay != null)
        {
            return cachedReplay;
        }

        var score = await GetScore(scoreId);

        if (score == null)
        {
            throw new Exception("Score not found");
        }

        var exp = new Expr("Id", OperatorEnum.Equals, score.ReplayFileId);
        var file = await _orm.SelectFirstAsync<UserFile?>(exp);

        if (file == null)
        {
            throw new Exception("Replay not found");
        }

        var replay = await File.ReadAllBytesAsync(file.Path);
        await Redis.Set(string.Format(RedisKey.Replay, scoreId), replay);

        return replay;
    }

    public async Task<long> GetUserRank(int userId, GameMode mode)
    {

        return (int)(await Redis.Redis.SortedSetRankAsync(string.Format(RedisKey.LeaderboardGlobal, (int)mode), userId, Order.Descending))! + 1;
    }

    public async Task SetUserRank(int userId, GameMode mode)
    {
        var userStats = await GetUserStats(userId, mode);
        await Redis.Redis.SortedSetAddAsync(string.Format(RedisKey.LeaderboardGlobal, (int)mode), userId, userStats.PerformancePoints);
    }

    public async Task RemoveUserRank(int userId, GameMode mode)
    {
        await Redis.Redis.SortedSetRemoveAsync(string.Format(RedisKey.LeaderboardGlobal, (int)mode), userId);
    }
}