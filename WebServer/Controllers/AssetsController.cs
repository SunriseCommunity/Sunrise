using Microsoft.AspNetCore.Mvc;
using Sunrise.WebServer.Services;

namespace Sunrise.WebServer.Controllers;

[Controller]
[Route("/api")]
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

    [HttpPost]
    [Route("avatar/upload/{id}")]
    public async Task<IActionResult> SetAvatar(int id)
    {
        await _fileService.SetAvatar(id, Request);

        return new OkResult();
    }


}