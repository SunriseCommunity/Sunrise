using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Storages;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class AssetService
{
    private const int Megabyte = 1024 * 1024;

    public static async Task<byte[]?> GetOsuReplayBytes(int scoreId)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var score = await database.GetScore(scoreId);
        if (score == null)
            return null;

        var replay = await database.GetReplay(score.ReplayFileId);

        return replay;
    }

    public static async Task<(bool, string?)> SetBanner(int userId, MemoryStream buffer)
    {
        var (isValid, err) = ImageTools.IsHasValidImageAttributes(buffer);
        if (!isValid || err != null)
            return (false, err);

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var isSuccessful = await database.SetBanner(userId, buffer.ToArray());
        return isSuccessful ? (true, null) : (false, "Failed to save banner. Please try again later.");
    }

    public static string[] GetSeasonalBackgrounds()
    {
        const string basePath = "./Data/Files/SeasonalBackgrounds";

        var files = Directory.GetFiles(basePath).Where(x => x.EndsWith(".jpg")).ToArray();
        var backgrounds = new string[files.Length];

        for (var i = 0; i < files.Length; i++) backgrounds[i] = Path.GetFileNameWithoutExtension(files[i]);

        var seasonalBackgrounds = backgrounds.Select(x => $"https://{Configuration.Domain}/static/{x}.jpg").ToArray();

        return seasonalBackgrounds;
    }

    public static async Task<(bool, string?)> SetAvatar(int userId, MemoryStream buffer)
    {
        var (isValid, err) = ImageTools.IsHasValidImageAttributes(buffer);
        if (!isValid || err != null)
            return (false, err);

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var isSuccessful = await database.SetAvatar(userId, buffer.ToArray());

        return isSuccessful ? (true, null) : (false, "Failed to save avatar. Please try again later.");
    }

    public static async Task<(string?, string?)> SaveScreenshot(Session session, IFormFile screenshot,
        CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await screenshot.CopyToAsync(buffer, ct);

        if (buffer.Length > 5 * Megabyte)
            return (null, $"Screenshot is too large ({buffer.Length / Megabyte}MB)");

        if (!ImageTools.IsValidImage(buffer))
            return (null, "Invalid image format");

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var screenshotId = await database.SetScreenshot(session.User.Id, buffer.ToArray());

        return ($"https://a.{Configuration.Domain}/ss/{screenshotId}.jpg", null);
    }

    public static async Task<(byte[]?, string?)> GetScreenshot(int screenshotId)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var screenshot = await database.GetScreenshot(screenshotId);

        return screenshot == null ? (null, "Screenshot not found") : (screenshot, null);
    }

    public static async Task<(byte[]?, string?)> GetAvatar(int userId, bool toFallback = true)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var screenshot = await database.GetAvatar(userId, toFallback);

        return screenshot == null ? (null, "Avatar not found") : (screenshot, null);
    }

    public static async Task<(byte[]?, string?)> GetBanner(int userId, bool toFallback = true)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var screenshot = await database.GetBanner(userId, toFallback);

        return screenshot == null ? (null, "Banner not found") : (screenshot, null);
    }

    public static async Task<byte[]?> GetEventBanner()
    {
        return await LocalStorage.ReadFileAsync("./Data/Files/Assets/EventBanner.png");
    }

    public static async Task<byte[]?> GetMedalImage(int medalFileId, bool isHighRes = false)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var medalImage = await database.GetMedalImage(medalFileId, isHighRes);

        return medalImage ?? await LocalStorage.ReadFileAsync("./Data/Files/Medals/default.png");
    }

    public static async Task<byte[]?> GetPeppyImage()
    {
        return await LocalStorage.ReadFileAsync("./Data/Files/Assets/Peppy.jpg");
    }
}