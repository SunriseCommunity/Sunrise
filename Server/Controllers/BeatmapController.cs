using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Objects.CustomAttributes;

namespace Sunrise.Server.Controllers;

[ApiController]
[Subdomain("b")]
public class BeatmapController : ControllerBase
{
    [HttpGet]
    [Route("{type}/{path}")]
    public IActionResult RedirectToResource(string type, string path)
    {
        return Redirect($"https://b.ppy.sh/{type}/{path}");
    }
}