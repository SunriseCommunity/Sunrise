using System.Text.Json;
using Sunrise.API.Enums;
using Sunrise.Shared.Application;

namespace Sunrise.API.Objects;

public class WebSocketMessage(WebSocketEventType type, object message)
{
    private readonly object _message = message;
    private JsonSerializerOptions JsonSerializerOptions { get; } = Configuration.SystemTextJsonOptions;

    public WebSocketEventType MessageType { get; } = type;

    public string Data => JsonSerializer.Serialize(new
        {
            type = MessageType,
            data = _message
        },
        JsonSerializerOptions);
}