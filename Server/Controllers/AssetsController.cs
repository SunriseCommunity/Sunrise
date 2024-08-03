using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Services;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[ApiController]
[SubdomainAttribute("a", "assets")]
public class AssetsController(FileService fileService) : ControllerBase
{
    [HttpGet]
    [Route("{id:int}")]
    public async Task<IActionResult> GetAvatar(int id)
    {
        var result = await fileService.GetAvatarBytes(id);
        return new FileContentResult(result, "image/png");
    }

    [HttpGet]
    [Route("menu-content.json")]
    public IActionResult GetMenuContent()
    {
        var eventImageUri = $"https://assets.{Configuration.Domain}/events/EventBanner.jpg";

        var json = """{ "images": [{ "image": "{img}", "url": "https://github.com/SunriseCommunity/Sunrise", "IsCurrent": true, "begins": null, "expires": "2099-06-01T12:00:00+00:00"}] }""";
        json = json.Replace("{img}", eventImageUri);

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