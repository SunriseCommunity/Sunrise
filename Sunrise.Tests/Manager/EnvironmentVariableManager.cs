using Sunrise.Shared.Application;

namespace Sunrise.Tests.Manager;

public class EnvironmentVariableManager : IDisposable
{
    private readonly Dictionary<string, string?> _originalValues = new();

    public void Dispose()
    {
        foreach (var (key, originalValue) in _originalValues)
        {
            Environment.SetEnvironmentVariable(key, originalValue);
        }

        Configuration.GetConfig().Reload();

        GC.SuppressFinalize(this);
    }

    public void Set(string key, string? value)
    {
        if (!_originalValues.ContainsKey(key))
        {
            _originalValues[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);

        Configuration.GetConfig().Reload();
    }
}