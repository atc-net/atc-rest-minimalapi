namespace Atc.Rest.MinimalApi;

/// <summary>
/// Key names used by the <see cref="Middleware.GlobalErrorHandlingMiddleware"/> for the request/trace ids,
/// both in the error response body and in the log scope.
/// </summary>
/// <remarks>
/// The same value has a camelCase key in the response body (JSON convention) and a PascalCase key in the
/// log scope (.NET logging convention):
/// <list type="table">
///   <listheader>
///     <term>Source</term>
///     <description>Response body key / log scope key</description>
///   </listheader>
///   <item>
///     <term><c>x-correlation-id</c> header</term>
///     <description><c>correlationId</c> / <c>CorrelationId</c></description>
///   </item>
///   <item>
///     <term><c>x-request-id</c> header</term>
///     <description><c>requestId</c> / <c>ClientRequestId</c></description>
///   </item>
///   <item>
///     <term><c>Activity.Current?.Id ?? HttpContext.TraceIdentifier</c> (the same rule as ASP.NET Core)</term>
///     <description><c>traceId</c> / with tracing active, the W3C id <c>00-&lt;TraceId&gt;-&lt;SpanId&gt;-01</c>, whose parts are
///     the hosting scope's <c>TraceId</c> and <c>SpanId</c>; without tracing, the hosting scope's <c>RequestId</c>
///     (both written by ASP.NET Core, not by this middleware)</description>
///   </item>
/// </list>
/// </remarks>
internal static class ErrorHandlingKeyConstants
{
    /// <summary>
    /// Extension keys written to the error response body (ProblemDetails extensions or the plain dictionary body).
    /// </summary>
    internal static class ResponseBody
    {
        /// <summary>
        /// The value of the <c>x-correlation-id</c> request header.
        /// </summary>
        public const string CorrelationId = "correlationId";

        /// <summary>
        /// The value of the <c>x-request-id</c> request header.
        /// </summary>
        public const string RequestId = "requestId";

        /// <summary>
        /// <c>Activity.Current?.Id ?? HttpContext.TraceIdentifier</c>: the W3C trace id when tracing is active,
        /// otherwise <see cref="Microsoft.AspNetCore.Http.HttpContext.TraceIdentifier"/> (the same rule as ASP.NET Core).
        /// </summary>
        public const string TraceId = "traceId";
    }

    /// <summary>
    /// Keys added to the log scope when an exception is logged.
    /// </summary>
    internal static class LogScope
    {
        /// <summary>
        /// The value of the <c>x-correlation-id</c> request header.
        /// </summary>
        public const string CorrelationId = "CorrelationId";

        /// <summary>
        /// The value of the <c>x-request-id</c> request header.
        /// Not named <c>RequestId</c>, because ASP.NET Core's hosting scope already uses that key
        /// for <see cref="Microsoft.AspNetCore.Http.HttpContext.TraceIdentifier"/>.
        /// </summary>
        public const string ClientRequestId = "ClientRequestId";
    }

    /// <summary>
    /// Metric names and tags for a handled exception: the same as ASP.NET Core's own exception handler
    /// (<c>UseExceptionHandler</c>), so dashboards and alerts work the same with either.
    /// </summary>
    internal static class Telemetry
    {
        /// <summary>
        /// ASP.NET Core's diagnostics meter.
        /// </summary>
        public const string DiagnosticsMeterName = "Microsoft.AspNetCore.Diagnostics";

        /// <summary>
        /// The counter of exceptions caught by exception handling middleware.
        /// </summary>
        public const string ExceptionsCounterName = "aspnetcore.diagnostics.exceptions";

        /// <summary>
        /// The unit of <see cref="ExceptionsCounterName"/>.
        /// </summary>
        public const string ExceptionsCounterUnit = "{exception}";

        /// <summary>
        /// The description of <see cref="ExceptionsCounterName"/>.
        /// </summary>
        public const string ExceptionsCounterDescription = "Number of exceptions caught by exception handling middleware.";

        /// <summary>
        /// The OpenTelemetry tag with the exception's full type name, on the request metric
        /// (<c>http.server.request.duration</c>) and on <see cref="ExceptionsCounterName"/>.
        /// </summary>
        public const string ErrorType = "error.type";

        /// <summary>
        /// The tag with what happened to the exception.
        /// </summary>
        public const string ExceptionResult = "aspnetcore.diagnostics.exception.result";

        /// <summary>
        /// The <see cref="ExceptionResult"/> value when an error response was written.
        /// </summary>
        public const string ExceptionResultHandled = "handled";
    }
}