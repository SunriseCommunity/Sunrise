using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;
using Sunrise.Shared.Application;

namespace Sunrise.Shared.Database.Interceptor;

public class SlowQueryLoggerInterceptor : DbCommandInterceptor
{
    private static int SlowQueryThresholdMilliseconds => Configuration.SlowQueryThresholdMilliseconds;

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (eventData.Duration.TotalMilliseconds > SlowQueryThresholdMilliseconds)
        {
            Log.Warning("Slow Query Detected. ({Milliseconds}ms): {QueryString}", eventData.Duration.TotalMilliseconds, command.CommandText);
        }

        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        if (eventData.Duration.TotalMilliseconds > SlowQueryThresholdMilliseconds)
        {
            Log.Warning("Slow Query Detected. ({Milliseconds}ms): {QueryString}", eventData.Duration.TotalMilliseconds, command.CommandText);
        }

        return base.ReaderExecuted(command, eventData, result);
    }
}