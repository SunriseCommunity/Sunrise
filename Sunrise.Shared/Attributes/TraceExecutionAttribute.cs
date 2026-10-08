using Rougamo.Context;
using Rougamo.OpenTelemetry;
using Sunrise.Shared.Extensions;

namespace Sunrise.Shared.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class TraceExecutionAttribute : OtelAttribute
{
    public override void OnException(MethodContext context)
    {
        if (context.Exception is { } exception &&
            (exception.IsExpectedCancellation() ||
             context.Arguments.Any(argument => argument is CancellationToken token && exception.IsExpectedCancellation(token))))
            return;

        base.OnException(context);
    }
}
