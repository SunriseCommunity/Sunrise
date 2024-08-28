using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Data;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[Subdomain("a", "assets")]
public class AssetsController : ControllerBase
{
    [HttpGet(RequestType.GetAvatar)]
    public async Task<IActionResult> GetAvatar(int id)
    {
        var result = await AssetService.GetAvatar(id);
        return new FileContentResult(result, "image/png");
    }

    [HttpGet(RequestType.GetBanner)]
    public async Task<IActionResult> GetBanner(int id)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var file = await database.GetBanner(id);
        return new FileContentResult(file, "image/png");
    }

    [HttpGet(RequestType.MenuContent)]
    public IActionResult GetMenuContent()
    {
        return Ok(BanchoService.GetCurrentEventJson());
    }

    [HttpGet(RequestType.EventBanner)]
    public async Task<IActionResult> GetEventBanner()
    {
        var data = await AssetService.GetEventBanner();
        if (data == null)
            return NotFound();

        return new FileContentResult(data, "image/png");
    }

    [HttpGet]
    [Route(RequestType.GetScreenshot)]
    public async Task<IActionResult> GetScreenshot(int id)
    {
        if (await AssetService.GetScreenshot(id) is var (screenshot, error) && (error != null || screenshot == null))
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuScreenshot, null, error);
            return NotFound();
        }

        return new FileContentResult(screenshot, "image/jpeg");
    }
}