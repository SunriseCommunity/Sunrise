using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Services;
using WebSocketManager = Sunrise.API.Managers.WebSocketManager;

namespace Sunrise.API.Controllers;

[Route("/ws")]
[Subdomain("api")]
public class WebSocketController(WebSocketManager webSocketManager) : ControllerBase
{
    [HttpGet]
    [EndpointDescription("WebSocket route. Sends server events as stringified JSON on connection.")]
    [ProducesResponseType(typeof(WebSocketMessage), StatusCodes.Status200OK)]
    public async Task Get(CancellationToken cancellationToken)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            var userIp = RegionService.GetUserIpAddress(Request);
            await webSocketManager.HandleConnection(userIp, webSocket, cancellationToken);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}