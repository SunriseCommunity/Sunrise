using Sunrise.Server.Application;

namespace Sunrise.Server.Tests.Core.Manager;

public class EnvironmentVariableManager : IDisposable
{
    private readonly Dictionary<string, string?> _originalValues = new();

    public void Set(string key, string? value)
    {
        if (!_originalValues.ContainsKey(key))
        {
            _originalValues[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
        
        Configuration.GetConfig().Reload();
    }

    public void Dispose()
    {
        foreach (var (key, originalValue) in _originalValues)
        {
            Environment.SetEnvironmentVariable(key, originalValue);
        }
        
        Configuration.GetConfig().Reload();
        
        GC.SuppressFinalize(this);
    }
}
