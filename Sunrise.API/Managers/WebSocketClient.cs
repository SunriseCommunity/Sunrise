using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Sunrise.API.Objects;

namespace Sunrise.API.Managers;

public class WebSocketClient(WebSocket webSocket)
{
    private readonly ConcurrentQueue<WebSocketMessage> _wsMessagesToPush = new();
    private Task? _pushMessagesTask;

    public void PushMessage(WebSocketMessage message)
    {
        _wsMessagesToPush.Enqueue(message);
    }

    public async Task HandleClientMessageAsync(CancellationToken cancellationToken)
    {
        var tempBuffer = new byte[1024 * 4];
        WebSocketReceiveResult receiveResult;

        do
        {
            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(tempBuffer), cancellationToken);
            if (!receiveResult.CloseStatus.HasValue)
                continue;

            await webSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, cancellationToken);
            return;
        } while (!receiveResult.EndOfMessage && !cancellationToken.IsCancellationRequested);
    }

    public async Task ProcessWebSocketMessagesAsync(CancellationToken cancellationToken)
    {
        using var internalCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            internalCts.Token
        );

        var pushTask = PushPendingMessagesAsync(linkedCts.Token);

        try
        {
            while (!linkedCts.Token.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                await HandleClientMessageAsync(linkedCts.Token);
            }
        }
        finally
        {
            await internalCts.CancelAsync();
            await pushTask;
        }
    }

    public async Task PushPendingMessagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_wsMessagesToPush.TryDequeue(out var message))
            {
                var messageBytes = Encoding.UTF8.GetBytes(message.Data);
                await webSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, cancellationToken);
            }

            if (_wsMessagesToPush.Count == 0)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}