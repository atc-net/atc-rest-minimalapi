// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Http;

public static class HttpContextExtensions
{
    public static string? GetCorrelationId(
        this HttpContext context)
        => context.Request.Headers.TryGetValue(
            WellKnownHttpHeaders.CorrelationId,
            out var header)
            ? header.FirstOrDefault()
            : null;

    public static string? GetRequestId(
        this HttpContext context)
        => context.Request.Headers.TryGetValue(
            WellKnownHttpHeaders.RequestId,
            out var header)
            ? header.FirstOrDefault()
            : null;
}