using DatabaseWrapper.Core;
using ExpressionTree;
using Sunrise.Database.Schemas;
using Sunrise.GameClient.Types.Enums;
using Watson.ORM.Sqlite;
using File = Sunrise.Database.Schemas.File;
using FileIO = System.IO.File;

namespace Sunrise.Database;

public class Database
{
    private const string DatabasePath = "./Database/";
    private const string DatabaseName = "sunrise.db";
    private readonly WatsonORM _orm = new(new DatabaseSettings(DatabasePath + DatabaseName));

    public Database()
    {
        Console.WriteLine("Starting database");

        _orm.InitializeDatabase();
        _orm.InitializeTables(new List<Type> { typeof(User), typeof(File), typeof(Score) });
    }

    public async Task<User> InsertUser(User user)
    {
        return await _orm.InsertAsync(user);
    }

    public async Task<User?> GetUser(int? id = null, string? username = null, string? token = null)
    {
        var exp = new Expr("Id", OperatorEnum.Equals, id ?? -1);
        if (username != null) exp.PrependOr(new Expr("Username", OperatorEnum.Equals, username));
        if (token != null) exp.PrependOr(new Expr("Token", OperatorEnum.Equals, token));

        var user = await _orm.SelectFirstAsync<User>(exp);

        return user;
    }

    public async Task<User> UpdateUser(User user)
    {
        return await _orm.UpdateAsync(user);
    }

    public async Task<byte[]?> GetAvatar(int id)
    {
        var exp = new Expr("OwnerId", OperatorEnum.Equals, id);
        exp.PrependAnd("Type", OperatorEnum.Equals, FileType.Avatar);

        var file = await _orm.SelectFirstAsync<File>(exp);

        if (file != null)
            return await FileIO.ReadAllBytesAsync(file.Path);

        return await FileIO.ReadAllBytesAsync("./Database/Files/DefaultAvatar.png");
    }

    public async Task SetAvatar(int id, byte[] avatar)
    {
        var path = $"./Database/Files/{id}.png";
        await FileIO.WriteAllBytesAsync(path, avatar);

        var file = new File()
        {
            OwnerId = id,
            Path = path,
            Type = FileType.Avatar,
            CreatedAt = DateTime.Now
        };

        await _orm.InsertAsync(file);
    }

    public async Task<Score> InsertScore(Score score)
    {
        return await _orm.InsertAsync(score);
    }

    public async Task<List<Score>> GetScores(string beatmapHash, PlayModes playMode = PlayModes.Osu)
    {
        var exp = new Expr("BeatmapHash", OperatorEnum.Equals, beatmapHash);
        exp.PrependAnd("PlayMode", OperatorEnum.Equals, (int)playMode);

        return await _orm.SelectManyAsync<Score>(exp);
    }
}