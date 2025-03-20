using System.Diagnostics;
using System.Linq.Expressions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Services;

public static class BackgroundTaskService
{
    public static void TryStartNewBackgroundJob<T>(
        Expression<Func<Task>> action,
        Action<string>? trySendMessage,
        bool? shouldEnterMaintenance = false)
    {
        if (Configuration.OnMaintenance && shouldEnterMaintenance == true)
        {
            trySendMessage?.Invoke("Server is in maintenance mode. Starting new jobs which requires server to enter maintenance mode is not possible.");
            return;
        }

        var jobName = typeof(T).Name;

        trySendMessage?.Invoke($"{jobName} has been started.");

        if (shouldEnterMaintenance == true)
        {
            Configuration.OnMaintenance = true;

            trySendMessage?.Invoke("Server will enter maintenance mode until it's done.");

            var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

            foreach (var userSession in sessions.GetSessions())
            {
                userSession.SendBanchoMaintenance();
            }
        }

        var jobId = BackgroundJob.Enqueue(action);

        trySendMessage?.Invoke($"Use '{Configuration.BotPrefix}canceljob {jobId}' to stop the {jobName} execution.");
    }

    public static async Task ExecuteBackgroundTask<T>(
        Func<Task> action,
        Action<string>? trySendMessage = null)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<T>>();

        var stopwatch = Stopwatch.StartNew();
        var jobName = typeof(T).Name;

        try
        {
            await action();

            trySendMessage?.Invoke($"{jobName} has successfully finished!");
        }
        catch (OperationCanceledException)
        {
            trySendMessage?.Invoke($"{jobName} was stopped.");
            logger.LogInformation($"{jobName} was stopped by user.");
        }
        catch (Exception ex)
        {
            trySendMessage?.Invoke($"Error occurred while executing {jobName}. Check console for more details.");
            trySendMessage?.Invoke($"Error message: {ex.Message}");
            logger.LogError(ex, $"Exception occurred while executing job \"{jobName}\".");
        }
        finally
        {
            stopwatch.Stop();

            Configuration.OnMaintenance = false;
            trySendMessage?.Invoke($"Server is back online. Took time to proceed job \"{jobName}\": {stopwatch.ElapsedMilliseconds / 1000.0} s.");
        }
    }
}