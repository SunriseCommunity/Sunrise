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

    public async Task<Result<byte[]>> GetOsuReplayBytes(int scoreId, CancellationToken ct = default)
    {
        var score = await database.Scores.GetScore(scoreId, ct: ct);
        if (score is null)
            return Result.Failure<byte[]>($"Score with ID {scoreId} not found");

        if (score.ReplayFileId == null)
            return Result.Failure<byte[]>($"Replay file for score with ID {scoreId} not found");

        var replay = await database.Scores.Files.GetReplayFile(score.ReplayFileId.Value, ct);
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
        await using var stream = screenshot.OpenReadStream();

        if (stream.Length > 5 * Megabyte)
            return Result.Failure<string>($"Screenshot is too large ({stream.Length / Megabyte}MB)");

        if (!ImageTools.IsValidImage(stream))
            return Result.Failure<string>("Invalid image format");

        var addScreenshotResult = await database.Users.Files.AddScreenshot(session.UserId, stream);
        if (addScreenshotResult.IsFailure)
            return Result.Failure<string>(addScreenshotResult.Error);

        return Result.Success($"https://a.{Configuration.Domain}/ss/{addScreenshotResult.Value}.jpg");
    }

    public async Task<Result<byte[]>> GetScreenshot(int screenshotId, CancellationToken ct = default)
    {
        var screenshot = await database.Users.Files.GetScreenshot(screenshotId, ct);
        if (screenshot == null)
            return Result.Failure<byte[]>("Screenshot not found");

        return Result.Success(screenshot);
    }

    public async Task<Result<byte[]>> GetAvatar(int userId, bool toFallback = true, CancellationToken ct = default)
    {
        var avatar = await database.Users.Files.GetAvatar(userId, toFallback, ct);
        if (avatar == null)
            return Result.Failure<byte[]>("Avatar not found");

        return Result.Success(avatar);
    }

    public async Task<Result<byte[]>> GetBanner(int userId, bool toFallback = true, CancellationToken ct = default)
    {
        var banner = await database.Users.Files.GetBanner(userId, toFallback, ct);
        if (banner == null)
            return Result.Failure<byte[]>("Banner not found");

        return Result.Success(banner);
    }

    public async Task<byte[]?> GetEventBanner(CancellationToken ct = default)
    {
        return await LocalStorageRepository.ReadFileAsync(Path.Combine(DataPath, "Files/Assets/EventBanner.png"), ct);
    }

    public async Task<byte[]?> GetMedalImage(int medalFileId, bool isHighRes = false, CancellationToken ct = default)
    {
        var medalImage = await database.Medals.GetMedalImage(medalFileId, isHighRes, ct: ct);

        var defaultImagePath = Path.Combine(Configuration.DataPath, "Files/Medals/default.png");
        var defaultImage = isHighRes ? defaultImagePath.Replace(".png", "@2x.png") : defaultImagePath;

        return medalImage ?? await LocalStorageRepository.ReadFileAsync(Path.Combine(Directory.GetCurrentDirectory(), defaultImage), ct);
    }

    public async Task<byte[]?> GetPeppyImage(CancellationToken ct = default)
    {
        return await LocalStorageRepository.ReadFileAsync(Path.Combine(DataPath, "Files/Assets/Peppy.jpg"), ct);
    }
}