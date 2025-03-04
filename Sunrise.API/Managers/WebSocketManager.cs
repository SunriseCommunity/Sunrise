using System.Net.WebSockets;
using Sunrise.API.Objects;

namespace Sunrise.API.Managers;

public class WebSocketManager
{
    private readonly List<WebSocketClient> _clientConnections = [];
    
    public async Task HandleConnection(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var connection = new WebSocketClient(webSocket);
        AddClientConnection(connection);
        try
        {
            await connection.ProcessWebSocketMessagesAsync(cancellationToken);
        }
        finally
        {
            RemoveClientConnection(connection);
        }
    }

    public void BroadcastJsonAsync(WebSocketMessage message)
    {
        lock (_clientConnections)
        {
            if (_clientConnections.Count > 0)
            {
                Parallel.ForEach(_clientConnections, conn => conn.PushMessage(message));
            }
        }
    }
    
    private void AddClientConnection(WebSocketClient connection)
    {
        lock (_clientConnections)
        {
            _clientConnections.Add(connection);
        }
    }

    private void RemoveClientConnection(WebSocketClient connection)
    {
        lock (_clientConnections)
        {
            _clientConnections.Remove(connection);
        }
    }
}