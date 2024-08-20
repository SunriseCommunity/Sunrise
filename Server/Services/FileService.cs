using Sunrise.Server.Data;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class FileService
{
    private const int MegaByte = 1024 * 1024;

    public static async Task<byte[]> GetOsuReplayBytes(int scoreId)
    {
        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var score = await database.GetScore(scoreId);

        if (score == null)
        {
            throw new Exception("Score not found");
        }

        var replay = await database.GetReplay(score.ReplayFileId);

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

    public static async Task<string> SaveScreenshot(HttpRequest request, User user)
    {
        var screenshot = request.Form.Files["ss"];

        if (screenshot == null)
        {
            throw new Exception("Invalid request: Missing parameters");
        }

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        using var buffer = new MemoryStream();
        await screenshot.CopyToAsync(buffer, request.HttpContext.RequestAborted);

        if (buffer.Length > 5 * MegaByte)
        {
            throw new Exception("Screenshot is too large. Max size is 5MB");
        }

        var screenshotId = await database.SetScreenshot(user.Id, buffer.ToArray());

        return $"https://a.{Configuration.Domain}/ss/{screenshotId}.jpg";
    }
}