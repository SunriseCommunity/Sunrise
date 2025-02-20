using Sunrise.Shared.Utils.Converters;

namespace Sunrise.Shared.Objects.Multiplayer;

public class MultiplayerTimer
{
    private readonly string _alertMessage;

    private readonly List<int> _alerts = [30, 10, 5, 4, 3, 2, 1];
    private readonly MultiplayerMatch _match;

    private readonly Func<MultiplayerMatch, string, Task>? _onAlert;
    private readonly Func<MultiplayerMatch, Task>? _onFinish;

    private readonly Timer _timer;

    private int _seconds;

    public MultiplayerTimer(int seconds, string alertMessage, MultiplayerMatch match, Func<MultiplayerMatch, Task>? onFinish = null, Func<MultiplayerMatch, string, Task>? onAlert = null)
    {
        _onFinish = onFinish;
        _onAlert = onAlert;
        _match = match;

        for (var i = 1; i <= seconds / 60; i++)
        {
            _alerts.Insert(0, i * 60);
        }

        _alertMessage = alertMessage;

        _seconds = seconds;

        _timer = new Timer(Tick, null, 0, 1000);
    }

    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _timer.Dispose();
    }

    private void Tick(object? state)
    {
        _seconds--;

        if (_seconds > 0)
        {
            var alert = _alerts.Contains(_seconds);

            if (alert)
            {
                _onAlert?.Invoke(_match, string.Format(_alertMessage, TimeConverter.SecondsToMinutes(_seconds)));
            }
        }
        else
        {
            _onFinish?.Invoke(_match);
        }
    }
}