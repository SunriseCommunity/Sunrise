using Microsoft.AspNetCore.Mvc;
using Sunrise.GameClient.Services;

namespace Sunrise.GameClient.Controllers;

[ApiController]
[Route("/assets")]
public class AssetsController : ControllerBase
{
    private readonly FileService _fileService;

    public AssetsController(FileService fileService)
    {
        _fileService = fileService;
    }

    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetAvatar(int id)
    {
        try
        {
            var result = await _fileService.GetAvatarBytes(id);
            return new FileContentResult(result, "image/png");
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpGet]
    [Route("menu-content.json")]
    public IActionResult GetMenuContent()
    {
        const string eventImageUri = "https://osu.ppy.sh/api/events/EventBanner.jpg";

        var json = """{ "images": [{ "image": "{img}", "url": "https://github.com/SunriseCommunity/Sunrise", "IsCurrent": true, "begins": null, "expires": "2099-06-01T12:00:00+00:00"}] }""";
        json = json.Replace("{img}", eventImageUri);

        return Ok(json);
    }
}