using DatabaseWrapper.Core;
using ExpressionTree;
using Sunrise.Types.Enums;
using Watson.ORM.Sqlite;

namespace Sunrise.Database;

public class Database
{
    private const string DatabasePath = "./Database/";
    private const string DatabaseName = "sunrise.db";
    private readonly WatsonORM _orm = new(new DatabaseSettings(DatabasePath + DatabaseName));

    public Database() {
        Console.WriteLine("Starting database");

        _orm.InitializeDatabase();
        _orm.InitializeTables(new List<Type> { typeof(UserSchema), typeof(FileSchema), typeof(ScoreSchema) });
    }
    
    public async Task InsertUser(UserSchema user)
    {
        await _orm.InsertAsync(user);
    }

    public async Task<UserSchema> GetUser(int? id = null, string? username = null, string? token = null)
    {
        var exp = new Expr("Id", OperatorEnum.Equals, id ?? -1);
        if (username != null) exp.PrependOr(new Expr("Username", OperatorEnum.Equals, username ));
        if (token != null) exp.PrependOr(new Expr("Token", OperatorEnum.Equals, token ));

        var user = await _orm.SelectFirstAsync<UserSchema>(exp);
        
        return user;
    }
    
    public async Task<UserSchema> UpdateUser(UserSchema user)
    {
        return await _orm.UpdateAsync(user);
    }
    
    public async Task<byte[]?> GetAvatar(int id)
    {
        var exp = new Expr("OwnerId", OperatorEnum.Equals, id);
        exp.PrependAnd("Type", OperatorEnum.Equals, FileType.Avatar);

        var file = await _orm.SelectFirstAsync<FileSchema>(exp);

        if (file != null)
            return await File.ReadAllBytesAsync(file.Path);

        Console.WriteLine("Avatar not found");
        return await File.ReadAllBytesAsync("./Database/Files/Default_Avatar.png");
    }
    
    public async Task SetAvatar(int id, byte[] avatar)
    {
        var path = $"./Database/Files/{id}.png";
        await File.WriteAllBytesAsync(path, avatar);
        
        var file = new FileSchema
        {
            OwnerId = id,
            Path = path,
            Type = FileType.Avatar,
            CreatedAt = DateTime.Now
        };
        
        await _orm.InsertAsync<FileSchema>(file);
    }
    
    public async Task<ScoreSchema> InsertScore(ScoreSchema score)
    {
        return await _orm.InsertAsync<ScoreSchema>(score);
    }
    
    public async Task<List<ScoreSchema>> GetScores(string beatmapHash)
    {
        var exp = new Expr("BeatmapHash", OperatorEnum.Equals, beatmapHash);
        var scores = await _orm.SelectManyAsync<ScoreSchema>(exp);
        
        return scores;
    }
}