using Sunrise.Database.Types.Enums;

namespace Sunrise.Database;

public class Files
{
    private readonly Sqlite.SqliteDatabase _sqlite;

    public Files(Sqlite.SqliteDatabase sqlite)
    {
        _sqlite = sqlite;
    }

    public byte[]? GetAvatar(int id)
    {

        var command = _sqlite.CreateCommand("SELECT path FROM files WHERE owner_id = @id AND type = 0");
        command.Parameters.AddWithValue("@id", id);

        using var reader = _sqlite.ExecuteReader(command);

        if (reader.Read())
        {
            var path = reader.GetString(0);
            return File.ReadAllBytes(path);
        }

        var defaultAvatar = File.ReadAllBytes("./Database/Files/Default_Avatar.png");

        return defaultAvatar;
    }

    public async Task<bool> SetAvatar(int id, byte[] avatar)
    {
        var path = $"./Database/Files/{id}.png";
        await File.WriteAllBytesAsync(path, avatar);


        var command = _sqlite.CreateCommand("INSERT INTO files (owner_id, path, type, created_at) VALUES (@id, @path, @type, @created_at)");
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@path", path);
        command.Parameters.AddWithValue("@type", FileType.Avatar);
        command.Parameters.AddWithValue("@created_at", DateTime.Now);

        return _sqlite.ExecuteNonQuery(command) > 0;
    }

    public string[] GetSeasonalBackgroundsTitles()
    {
        const string basePath = "./Database/Files/SeasonalBackgrounds";

        var files = Directory.GetFiles(basePath).Where(x => x.EndsWith(".jpg")).ToArray();

        var titles = new string[files.Length];

        for (var i = 0; i < files.Length; i++)
        {
            titles[i] = Path.GetFileNameWithoutExtension(files[i]);
        }

        return titles;
    }
}