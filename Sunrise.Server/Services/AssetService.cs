using CSharpFunctionalExtensions;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Tools;

namespace Sunrise.Server.Services;

public class AssetService(DatabaseService database)
{
    private const int Megabyte = 1024 * 1024;
    private static string DataPath => Configuration.DataPath;

    public async Task<Result<byte[]>> GetOsuReplayBytes(int scoreId)
    {
        var score = await database.Scores.GetScore(scoreId);
        if (score is null)
            return Result.Failure<byte[]>($"Score with ID {scoreId} not found");

        if (score.ReplayFileId == null)
            return Result.Failure<byte[]>($"Replay file for score with ID {scoreId} not found");

        var replay = await database.Scores.Files.GetReplayFile(score.ReplayFileId.Value);
        if (replay is null)
            return Result.Failure<byte[]>($"Replay file with ID {score.ReplayFileId.Value} not found");

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

    public async Task<Result<string>> SaveScreenshot(Session session, IFormFile screenshot,
        CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await screenshot.CopyToAsync(buffer, ct);

        if (buffer.Length > 5 * Megabyte)
            return Result.Failure<string>($"Screenshot is too large ({buffer.Length / Megabyte}MB)");

        if (!ImageTools.IsValidImage(buffer))
            return Result.Failure<string>("Invalid image format");

        var addScreenshotResult = await database.Users.Files.AddScreenshot(session.UserId, buffer.ToArray());
        if (addScreenshotResult.IsFailure)
            return Result.Failure<string>(addScreenshotResult.Error);

        return Result.Success($"https://a.{Configuration.Domain}/ss/{addScreenshotResult.Value}.jpg");
    }

    public async Task<Result<byte[]>> GetScreenshot(int screenshotId)
    {
        var screenshot = await database.Users.Files.GetScreenshot(screenshotId);
        if (screenshot == null)
            return Result.Failure<byte[]>("Screenshot not found");

        return Result.Success(screenshot);
    }

    public async Task<Result<byte[]>> GetAvatar(int userId, bool toFallback = true)
    {
        var avatar = await database.Users.Files.GetAvatar(userId, toFallback);
        if (avatar == null)
            return Result.Failure<byte[]>("Avatar not found");

        return Result.Success(avatar);
    }

    public async Task<Result<byte[]>> GetBanner(int userId, bool toFallback = true)
    {
        var banner = await database.Users.Files.GetBanner(userId, toFallback);
        if (banner == null)
            return Result.Failure<byte[]>("Banner not found");

        return Result.Success(banner);
    }

    public async Task<byte[]?> GetEventBanner()
    {
        return await LocalStorageRepository.ReadFileAsync(Path.Combine(DataPath, "Files/Assets/EventBanner.png"));
    }

    public async Task<byte[]?> GetMedalImage(int medalFileId, bool isHighRes = false)
    {
        var medalImage = await database.Medals.GetMedalImage(medalFileId, isHighRes);

        const string defaultImagePath = "Files/Medals/default.png";
        var defaultImage = isHighRes ? defaultImagePath.Replace(".png", "@2x.png") : defaultImagePath;

        return medalImage ?? await LocalStorageRepository.ReadFileAsync(Path.Combine(Directory.GetCurrentDirectory(), defaultImage));
    }

    public async Task<byte[]?> GetPeppyImage()
    {
        return await LocalStorageRepository.ReadFileAsync(Path.Combine(DataPath, "Files/Assets/Peppy.jpg"));
    }
}