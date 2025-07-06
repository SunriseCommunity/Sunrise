using System.Net;
using System.Net.WebSockets;
using Sunrise.API.Objects;

namespace Sunrise.API.Managers;

public class WebSocketManager
{
    private const int CONCURRENT_CONNECTIONS_LIMIT = 4;
    
    private readonly Dictionary<IPAddress, HashSet<WebSocketClient>> _clientConnections = [];

    public async Task HandleConnection(IPAddress ip, WebSocket webSocket, CancellationToken cancellationToken)
    {
        var connection = new WebSocketClient(webSocket);
        var connectionAccepted = false;

        lock (_clientConnections)
        {
            if (!_clientConnections.TryGetValue(ip, out var connections)
                || connections.Count < CONCURRENT_CONNECTIONS_LIMIT)
            {
                connectionAccepted = true;
            }
        }

        if (!connectionAccepted)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "WebSocket connections limit reached", CancellationToken.None);
            return;
        }

        AddClientConnection(ip, connection);

        try
        {
            await connection.ProcessWebSocketMessagesAsync(cancellationToken);
        }
        finally
        {
            RemoveClientConnection(ip, connection);
        }
    }

    public void BroadcastJsonAsync(WebSocketMessage message)
    {
        List<WebSocketClient> clients;

        lock (_clientConnections)
        {
            clients = _clientConnections.Values.SelectMany(set => set).ToList();
        }

        if (clients.Count > 0)
        {
            Parallel.ForEach(clients, client => { client.PushMessage(message); });
        }
    }

    private void AddClientConnection(IPAddress ip, WebSocketClient connection)
    {
        lock (_clientConnections)
        {
            if (!_clientConnections.TryGetValue(ip, out var connections))
            {
                connections = [];
                _clientConnections[ip] = connections;
            }

            connections.Add(connection);
        }
    }

    private void RemoveClientConnection(IPAddress ip, WebSocketClient connection)
    {
        lock (_clientConnections)
        {
            if (!_clientConnections.TryGetValue(ip, out var connections))
                return;

            connections.Remove(connection);

            if (connections.Count == 0)
            {
                _clientConnections.Remove(ip);
            }
        }
    }
}