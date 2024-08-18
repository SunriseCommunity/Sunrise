using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[ApiController]
[Subdomain("c", "c4", "cho")]
public class BanchoController : ControllerBase
{
    [HttpPost("/")]
    public async Task<IActionResult> Process()
    {
        string? sessionToken = Request.Headers["osu-token"];

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        if (sessionToken == null)
        {
            return await AuthService.Login(Request, Response);
        }

        var session = sessions.GetSession(sessionToken);

        if (session == null)
        {
            return AuthService.Relogin(Response);
        }

        session.Attributes.LastPingRequest = DateTime.UtcNow;

        await using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        ms.Seek(0, SeekOrigin.Begin);

        foreach (var packet in BanchoSerializer.DeserializePackets(ms))
        {
            await PacketRepository.HandlePacket(packet, session);
        }

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }

    [HttpGet("/")]
    public Task<IActionResult> Get()
    {
        return Task.FromResult<IActionResult>(Ok("Hello, world!"));
    }
}