using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public class FileService(ServicesProvider services)
{
    public async Task<byte[]> GetOsuReplayBytes(int scoreId)
    {
        var replay = await services.Database.GetReplay(scoreId);

        if (replay == null)
        {
            throw new Exception("Replay not found");
        }

        return replay;
    }

    public string[] GetSeasonalBackgrounds()
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

    public async Task<byte[]> GetAvatarBytes(int id)
    {
        var avatar = await services.Database.GetAvatar(id);

        if (avatar == null)
        {
            throw new Exception("Avatar not found");
        }

        return avatar;
    }
}