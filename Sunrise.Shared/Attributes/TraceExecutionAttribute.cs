using Rougamo.OpenTelemetry;

namespace Sunrise.Shared.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class TraceExecutionAttribute : OtelAttribute
{
}