using Microsoft.Extensions.DependencyInjection;

namespace Sunrise.Shared.Application;

public static class ServicesProviderHolder
{
    private static IServiceProvider _serviceProvider = null!;

    public static IServiceProvider ServiceProvider
    {
        get => _serviceProvider;
        set => _serviceProvider = value ?? throw new ArgumentNullException(nameof(value));
    }

    public static T GetService<T>() where T : notnull
    {
        if (_serviceProvider == null) throw new InvalidOperationException("ServiceProvider has not been set.");

        return _serviceProvider.GetService<T>() ??
               throw new InvalidOperationException($"Service of type {typeof(T)} not found.");
    }

    public static T GetRequiredService<T>() where T : notnull
    {
        if (_serviceProvider == null) throw new InvalidOperationException("ServiceProvider has not been set.");

        return _serviceProvider.GetRequiredService<T>();
    }

    public static IServiceScope CreateScope()
    {
        if (_serviceProvider == null) throw new InvalidOperationException("ServiceProvider has not been set.");

        return _serviceProvider.CreateScope();
    }
}