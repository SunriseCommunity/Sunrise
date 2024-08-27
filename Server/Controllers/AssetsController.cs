using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Data;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Services;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[ApiController]
[Subdomain("a", "assets")]
public class AssetsController : ControllerBase
{
    [HttpGet]
    [Route("{id:int}")]
    public async Task<IActionResult> GetAvatar(int id)
    {
        var result = await FileService.GetAvatarBytes(id);
        return new FileContentResult(result, "image/png");
    }

    [HttpGet]
    [Route("menu-content.json")]
    public IActionResult GetMenuContent()
    {
        var json = BanchoService.GetCurrentEventJson();
        return Ok(json);
    }

    [HttpGet]
    [Route("events/EventBanner.jpg")]
    public IActionResult GetEventBanner()
    {
        var data = System.IO.File.ReadAllBytes("./Data/Files/Assets/EventBanner.png");
        return new FileContentResult(data, "image/png");
    }

    [HttpGet]
    [Route("/ss/{id:int}.jpg")]
    public async Task<IActionResult> GetScreenshot(int id)
    {
        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();
        var screenshot = await database.GetScreenshot(id);

        if (screenshot == null)
        {
            return NotFound();
        }

        return new FileContentResult(screenshot, "image/jpeg");
    }
}