namespace Sunrise.Shared.Extensions;

public static class ExceptionExtensions
{
    public static bool IsExpectedCancellation(this Exception exception, CancellationToken cancellationToken = default)
    {
        return exception is OperationCanceledException canceled &&
               (canceled.CancellationToken.IsCancellationRequested || cancellationToken.IsCancellationRequested);
    }
}
