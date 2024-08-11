using Sunrise.Server.Data;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class FileService
{
    public static async Task<byte[]> GetOsuReplayBytes(int scoreId)
    {
        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var replay = await database.GetReplay(scoreId);

        if (replay == null)
        {
            throw new Exception("Replay not found");
        }

        return replay;
    }

    public static string[] GetSeasonalBackgrounds()
    {
        const string basePath = "./Data/Files/SeasonalBackgrounds";

        var files = Directory.GetFiles(basePath).Where(x => x.EndsWith(".jpg")).ToArray();
        var backgrounds = new string[files.Length];

        for (var i = 0; i < files.Length; i++)
        {
            backgrounds[i] = Path.GetFileNameWithoutExtension(files[i]);
        }

        var seasonalBackgrounds = backgrounds.Select(x => $"https://{Configuration.Domain}/static/{x}.jpg").ToArray();

        return seasonalBackgrounds;
    }

    public static async Task<byte[]> GetAvatarBytes(int id)
    {
        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var avatar = await database.GetAvatar(id);

        if (avatar == null)
        {
            throw new Exception("Avatar not found");
        }

        return avatar;
    }
}