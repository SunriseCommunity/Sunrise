using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Server.Storage;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[Subdomain("c", "c4", "cho")]
public class BanchoController(ILogger<BanchoController> logger) : ControllerBase
{
    [HttpPost(RequestType.BanchoProcess)]
    public async Task<IActionResult> Process([FromHeader(Name = "osu-token")] string? token)
    {
        if (token == null)
            return await AuthService.Login(Request, Response);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(out var session, token: token) || session == null)
            return AuthService.Relogin();

        await using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer);
        buffer.Seek(0, SeekOrigin.Begin);

        await BanchoService.ProcessPackets(session, buffer, logger);

        session.Attributes.UpdateLastPing();

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }

    [HttpGet(RequestType.BanchoProcess)]
    public async Task<IActionResult> Get()
    {
        var image = await AssetService.GetPeppyImage();
        if (image == null)
            return NotFound();

        return new FileContentResult(image, "image/jpeg");
    }
}