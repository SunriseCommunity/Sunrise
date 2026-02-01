using DotNetEnv;
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

        try
        {
            Configuration.GetConfig().Reload();
        }
        catch (Exception ex)
        {
            throw new Exception("Configuration reload failed during EnvironmentVariableManager disposal.", ex);
        }

        GC.SuppressFinalize(this);
    }

    public void Set(string key, string? value)
    {
        if (!_originalValues.ContainsKey(key))
        {
            _originalValues[key] = Environment.GetEnvironmentVariable(key);
        }

        Env.TraversePath().Load(".env.tests");
        Environment.SetEnvironmentVariable(key, value);

        try
        {
            Configuration.GetConfig().Reload();
        }
        catch (Exception ex)
        {
            throw new Exception($"Configuration reload failed after setting environment variable '{key}'.", ex);
        }
    }

    public void Set(string key, List<string> values)
    {
        if (!_originalValues.ContainsKey(key))
        {
            _originalValues[key] = Environment.GetEnvironmentVariable(key);
        }

        Env.TraversePath().Load(".env.tests");

        Environment.SetEnvironmentVariable(key, null);

        for (var i = 0; i < values.Count; i++)
        {
            Environment.SetEnvironmentVariable($"{key}:{i}", values[i]);
        }

        try
        {
            Configuration.GetConfig().Reload();
        }
        catch (Exception ex)
        {
            throw new Exception($"Configuration reload failed after setting environment variable '{key}'.", ex);
        }
    }
}