using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Services;

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
        var data = System.IO.File.ReadAllBytes("./Data/Files/EventBanner.png");
        return new FileContentResult(data, "image/jpeg");
    }
}