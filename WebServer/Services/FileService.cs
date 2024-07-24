namespace Sunrise.WebServer.Services;

public class FileService
{
    private readonly ServicesProvider _services;
    public FileService(ServicesProvider services)
    {
        _services = services;
    }

    public string[] GetSeasonalBackgrounds()
    {
        const string basePath = "./Database/Files/SeasonalBackgrounds";

        var files = Directory.GetFiles(basePath).Where(x => x.EndsWith(".jpg")).ToArray();
        var backgrounds = new string[files.Length];

        for (var i = 0; i < files.Length; i++)
        {
            backgrounds[i] = Path.GetFileNameWithoutExtension(files[i]);
        }

        // Note: This works because we rewrite ppy.sh requests with Fiddler. Should be improved later.
        var seasonalBackgrounds = backgrounds.Select(x => $"https://osu.ppy.sh/static/{x}.jpg").ToArray();

        return seasonalBackgrounds;
    }

    public async Task<byte[]> GetAvatarBytes(int id)
    {
        var avatar = await _services.Database.GetAvatar(id);

        if (avatar == null)
        {
            throw new Exception("Avatar not found");
        }

        return avatar;
    }


    public async Task SetAvatar(int userId, HttpRequest request)
    {
        using var buffer = new System.IO.MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);

        var avatar = buffer.ToArray();
        await _services.Database.SetAvatar(userId, avatar);
    }
}