using Microsoft.AspNetCore.Mvc;
using Sunrise.Database.Sqlite;

namespace Sunrise.WebServer.Controllers;

[Controller]
[Route("/api")]
public class AvatarController : ControllerBase
{
    private readonly SqliteDatabase _database;

    public AvatarController(SqliteDatabase database)
    {
        _database = database;
    }

    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetAvatar(int id)
    {
        var avatar = _database.Files.GetAvatar(id);

        if (avatar == null)
        {
            Response.StatusCode = 404;
            return new BadRequestResult();
        }

        return new FileContentResult(avatar, "image/png");
    }

    [HttpPost]
    [Route("avatar/upload/{id}")]
    public async Task<IActionResult> SetAvatar(int id)
    {
        using var buffer = new System.IO.MemoryStream();
        await Request.Body.CopyToAsync(buffer, Request.HttpContext.RequestAborted);
        var avatar = buffer.ToArray();

        _database.Files.SetAvatar(id, avatar);

        return new OkResult();
    }
}