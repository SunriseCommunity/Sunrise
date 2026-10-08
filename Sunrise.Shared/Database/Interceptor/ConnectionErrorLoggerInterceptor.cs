using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Extensions;

namespace Sunrise.Shared.Database.Interceptor;

public class ConnectionErrorLoggerInterceptor(ILoggerFactory loggerFactory) : DbConnectionInterceptor
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(DbLoggerCategory.Database.Connection.Name);

    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
    {
        LogFailure(connection, eventData);
    }

    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        LogFailure(connection, eventData, cancellationToken);
        return Task.CompletedTask;
    }

    private void LogFailure(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Exception.IsExpectedCancellation(cancellationToken))
            return;

        _logger.Log(eventData.LogLevel, RelationalEventId.ConnectionError, eventData.Exception,
            "An error occurred using the connection to database '{database}' on server '{server}'.",
            connection.Database, connection.DataSource);
    }
}
