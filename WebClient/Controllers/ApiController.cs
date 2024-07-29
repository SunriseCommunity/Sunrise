using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Sunrise.WebClient.Services;

namespace Sunrise.WebClient.Controllers;

[ApiController]
[Route("/api")]
public class ApiController : ControllerBase
{
    private readonly ApiService _apiService;

    public ApiController(ApiService apiService)
    {
        _apiService = apiService;
    }

    [HttpGet]
    [Route("avatars/{id}")]
    public async Task<IActionResult> GetAvatar(int id)
    {
        try
        {
            var result = await _apiService.GetAvatarBytes(id);
            return new FileContentResult(result, "image/png");
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpPost]
    [Route("avatars/upload/{id}")]
    public async Task<IActionResult> SetAvatar(int id)
    {
        var error = await _apiService.SetAvatar(id, Request);
        if (error != null)
            return BadRequest(error.Message);

        return new OkResult();
    }

    [HttpGet]
    [Route("events/EventBanner.jpg")]
    public IActionResult GetEventBanner()
    {
        var data = System.IO.File.ReadAllBytes("./Database/Files/EventBanner.png");
        return new FileContentResult(data, "image/jpeg");
    }
}