using System.Diagnostics;

namespace transactionalsystem.Runtime;

/// <summary>
/// Tracing through System.Diagnostics: OpenTelemetry .NET exports these activities when the source is
/// added to the tracer provider (configure exporters with the OTEL_* variables).
/// </summary>
public static class Telemetry
{
    public static readonly ActivitySource Source = new("transactional-system");

    /// <summary>Parent context described by an incoming W3C traceparent header (default when absent or invalid).</summary>
    public static ActivityContext ParentFrom(string? traceparent) =>
        !string.IsNullOrEmpty(traceparent) && ActivityContext.TryParse(traceparent, null, out var context) ? context : default;

    /// <summary>W3C traceparent of an activity (null when nothing is listening).</summary>
    public static string? TraceparentOf(Activity? activity) =>
        activity is null ? null : $"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{(activity.Recorded ? "01" : "00")}";
}
