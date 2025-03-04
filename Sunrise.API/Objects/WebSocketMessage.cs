using System.Text.Json;
using Sunrise.API.Enums;

namespace Sunrise.API.Objects;

public class WebSocketMessage(WebSocketEventType type, object message)
{
    private readonly object _message = message;
    public WebSocketEventType MessageType { get; } = type;

    public string Data => JsonSerializer.Serialize(new
    {
        type = MessageType,
        data = _message
    });
}