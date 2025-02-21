using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Tools;

namespace Sunrise.Server.Services;

public class AssetService
{
    private const int Megabyte = 1024 * 1024;
    private static string DataPath => Configuration.DataPath;

    public async Task<byte[]?> GetOsuReplayBytes(int scoreId)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var score = await database.ScoreService.GetScore(scoreId);
        if (score?.ReplayFileId == null)
            return null;

        var replay = await database.ScoreService.Files.GetReplay(score.ReplayFileId.Value);

        return replay;
    }

    public string[] GetSeasonalBackgrounds()
    {
        var basePath = Path.Combine(DataPath, "Files/SeasonalBackgrounds");

        var files = Directory.GetFiles(basePath).Where(x => x.EndsWith(".jpg")).ToArray();
        var backgrounds = new string[files.Length];

        for (var i = 0; i < files.Length; i++)
        {
            backgrounds[i] = Path.GetFileNameWithoutExtension(files[i]);
        }

        var seasonalBackgrounds = backgrounds.Select(x => $"https://a.{Configuration.Domain}/static/{x}.jpg").ToArray();

        return seasonalBackgrounds;
    }

    public async Task<(string?, string?)> SaveScreenshot(Session session, IFormFile screenshot,
        CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await screenshot.CopyToAsync(buffer, ct);

        if (buffer.Length > 5 * Megabyte)
            return (null, $"Screenshot is too large ({buffer.Length / Megabyte}MB)");

        if (!ImageTools.IsValidImage(buffer))
            return (null, "Invalid image format");

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var screenshotId = await database.UserService.Files.SetScreenshot(session.User.Id, buffer.ToArray());

        return ($"https://a.{Configuration.Domain}/ss/{screenshotId}.jpg", null);
    }

    public async Task<(byte[]?, string?)> GetScreenshot(int screenshotId)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var screenshot = await database.UserService.Files.GetScreenshot(screenshotId);

        return screenshot == null ? (null, "Screenshot not found") : (screenshot, null);
    }

    public async Task<(byte[]?, string?)> GetAvatar(int userId, bool toFallback = true)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var screenshot = await database.UserService.Files.GetAvatar(userId, toFallback);

        return screenshot == null ? (null, "Avatar not found") : (screenshot, null);
    }

    public async Task<(byte[]?, string?)> GetBanner(int userId, bool toFallback = true)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var screenshot = await database.UserService.Files.GetBanner(userId, toFallback);

        return screenshot == null ? (null, "Banner not found") : (screenshot, null);
    }

    public async Task<byte[]?> GetEventBanner()
    {
        return await LocalStorageRepository.ReadFileAsync(Path.Combine(DataPath, "Files/Assets/EventBanner.png"));
    }

    public async Task<byte[]?> GetMedalImage(int medalFileId, bool isHighRes = false)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var medalImage = await database.MedalService.GetMedalImage(medalFileId, isHighRes);

        const string defaultImagePath = "Files/Medals/default.png";
        var defaultImage = isHighRes ? defaultImagePath.Replace(".png", "@2x.png") : defaultImagePath;

        return medalImage ?? await LocalStorageRepository.ReadFileAsync(Path.Combine(Directory.GetCurrentDirectory(), defaultImage));
    }

    public async Task<byte[]?> GetPeppyImage()
    {
        return await LocalStorageRepository.ReadFileAsync(Path.Combine(DataPath, "Files/Assets/Peppy.jpg"));
    }
}