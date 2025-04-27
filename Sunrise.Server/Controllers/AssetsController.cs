using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Services;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Utils.Tools;

namespace Sunrise.Server.Controllers;

[Subdomain("a", "assets")]
[ResponseCache(Duration = 300)]
[ApiExplorerSettings(IgnoreApi = true)]
public class AssetsController(BanchoService banchoService, AssetService assetService, DatabaseService database) : ControllerBase
{
    [HttpGet(RequestType.GetAvatar)]
    [HttpGet(RequestType.GetBanchoAvatar)]
    public async Task<IActionResult> GetAvatar(int id, [FromQuery(Name = "default")] bool? fallToDefault, CancellationToken ct = default)
    {
        var getAvatarResult = await assetService.GetAvatar(id, fallToDefault ?? true, ct);

        if (getAvatarResult.IsFailure)
        {
            if (fallToDefault is true)
                SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.GetAvatar, null, getAvatarResult.Error);

            return NotFound();
        }

        return new FileContentResult(getAvatarResult.Value, $"image/{ImageTools.GetImageType(getAvatarResult.Value) ?? "png"}");
    }

    [HttpGet(RequestType.GetBanner)]
    public async Task<IActionResult> GetBanner(int id, [FromQuery(Name = "default")] bool? fallToDefault, CancellationToken ct = default)
    {
        var getBannerResult = await assetService.GetBanner(id, fallToDefault ?? true, ct);

        if (getBannerResult.IsFailure)
        {
            if (fallToDefault is true)
                SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.GetBanner, null, getBannerResult.Error);

            return NotFound();
        }

        return new FileContentResult(getBannerResult.Value, $"image/{ImageTools.GetImageType(getBannerResult.Value) ?? "png"}");
    }

    [HttpGet]
    [Route(RequestType.GetScreenshot)]
    public async Task<IActionResult> GetScreenshot(int id, CancellationToken ct = default)
    {
        var getScreenshotResult = await assetService.GetScreenshot(id, ct);

        if (getScreenshotResult.IsFailure)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuScreenshot, null, getScreenshotResult.Error);
            return NotFound();
        }

        return new FileContentResult(getScreenshotResult.Value, "image/jpeg");
    }

    [HttpGet(RequestType.GetMedalHighImage)]
    [HttpGet(RequestType.GetMedalImage)]
    public async Task<IActionResult> GetMedalImage(int medalId, CancellationToken ct = default)
    {
        var medal = await database.Medals.GetMedal(medalId, ct: ct);

        if (medal == null)
            return NotFound();

        var isHighRes = Request.Path.Value?.Contains("@2x") ?? false;

        if (medal.FileUrl != null)
            return Redirect(
                $"{Configuration.MedalMirrorUrl}{medal.FileUrl}{(isHighRes ? "@2x" : string.Empty)}.png");

        if (!medal.FileId.HasValue)
            return NotFound();

        var data = await assetService.GetMedalImage(medal.FileId.Value, isHighRes, ct);
        if (data == null)
            return NotFound();

        return new FileContentResult(data, $"image/{ImageTools.GetImageType(data) ?? "png"}");
    }

    [HttpGet(RequestType.MenuContent)]
    public IActionResult GetMenuContent()
    {
        return Ok(banchoService.GetCurrentEventJson());
    }

    [HttpGet(RequestType.EventBanner)]
    public async Task<IActionResult> GetEventBanner(CancellationToken ct = default)
    {
        var data = await assetService.GetEventBanner(ct);
        if (data == null)
            return NotFound();

        return new FileContentResult(data, "image/png");
    }
}