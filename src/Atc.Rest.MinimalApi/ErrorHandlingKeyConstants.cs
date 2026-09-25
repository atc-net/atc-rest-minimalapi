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
}