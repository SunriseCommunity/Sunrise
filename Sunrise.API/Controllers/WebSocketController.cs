using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Shared.Attributes;
using WebSocketManager = Sunrise.API.Managers.WebSocketManager;

namespace Sunrise.API.Controllers;

[Route("/ws")]
[Subdomain("api")]
[ApiExplorerSettings(IgnoreApi = true)]
public class WebSocketController(WebSocketManager webSocketManager) : ControllerBase
{
    public async Task Get(CancellationToken cancellationToken)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await webSocketManager.HandleConnection(webSocket, cancellationToken);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}