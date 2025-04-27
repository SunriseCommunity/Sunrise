using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Services;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Controllers;

[Subdomain("c", "c4", "cho")]
[ApiExplorerSettings(IgnoreApi = true)]
public class BanchoController(ILogger<BanchoController> logger, AuthService authService, BanchoService banchoService, AssetService assetService) : ControllerBase
{

    [HttpPost(RequestType.BanchoProcess)]
    public async Task<FileContentResult> Process([FromHeader(Name = "osu-token")] string? token)
    {
        if (token == null)
            return await authService.Login(Request, Response);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(out var session, token) || session == null)
            return authService.Relogin();

        await using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer);
        buffer.Seek(0, SeekOrigin.Begin);

        await banchoService.ProcessPackets(session, buffer, logger);

        session.Attributes.UpdateLastPing();

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }

    [HttpGet(RequestType.BanchoProcess)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var image = await assetService.GetPeppyImage(ct);
        if (image == null)
            return NotFound();

        return new FileContentResult(image, "image/jpeg");
    }
}