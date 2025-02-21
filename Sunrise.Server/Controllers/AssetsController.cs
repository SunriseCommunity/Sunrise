using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Services;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Utils.Tools;

namespace Sunrise.Server.Controllers;

[Subdomain("a", "assets")]
public class AssetsController(BanchoService banchoService, AssetService assetService) : ControllerBase
{
    [HttpGet(RequestType.GetAvatar)]
    [HttpGet(RequestType.GetBanchoAvatar)]
    public async Task<IActionResult> GetAvatar(int id, [FromQuery(Name = "default")] bool? fallToDefault)
    {
        if (await assetService.GetAvatar(id, fallToDefault ?? true) is var (avatar, error) &&
            (error != null || avatar == null))
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.GetAvatar, null, error);
            return NotFound();
        }

        return new FileContentResult(avatar, $"image/{ImageTools.GetImageType(avatar) ?? "png"}");
    }

    [HttpGet(RequestType.GetBanner)]
    public async Task<IActionResult> GetBanner(int id, [FromQuery(Name = "default")] bool? fallToDefault)
    {
        if (await assetService.GetBanner(id, fallToDefault ?? true) is var (banner, error) &&
            (error != null || banner == null))
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.GetBanner, null, error);
            return NotFound();
        }

        return new FileContentResult(banner, $"image/{ImageTools.GetImageType(banner) ?? "png"}");
    }

    [HttpGet]
    [Route(RequestType.GetScreenshot)]
    public async Task<IActionResult> GetScreenshot(int id)
    {
        if (await assetService.GetScreenshot(id) is var (screenshot, error) && (error != null || screenshot == null))
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuScreenshot, null, error);
            return NotFound();
        }

        return new FileContentResult(screenshot, "image/jpeg");
    }

    [HttpGet(RequestType.GetMedalHighImage)]
    [HttpGet(RequestType.GetMedalImage)]
    public async Task<IActionResult> GetMedalImage(int medalId)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var medal = await database.MedalService.GetMedal(medalId);

        if (medal == null)
            return NotFound();

        var isHighRes = Request.Path.Value?.Contains("@2x") ?? false;

        if (medal.FileUrl != null)
            return Redirect(
                $"{Configuration.MedalMirrorUrl}{medal.FileUrl}{(isHighRes ? "@2x" : string.Empty)}.png");

        var data = await assetService.GetMedalImage(medal.FileId, isHighRes);
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
    public async Task<IActionResult> GetEventBanner()
    {
        var data = await assetService.GetEventBanner();
        if (data == null)
            return NotFound();

        return new FileContentResult(data, "image/png");
    }
}