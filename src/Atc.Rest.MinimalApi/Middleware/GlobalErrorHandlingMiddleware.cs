namespace Atc.Rest.MinimalApi.Middleware;

/// <summary>
/// A middleware component for handling uncaught exceptions globally across an application.
/// </summary>
[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "Guarded by logger.IsEnabled - the analyzer does not see the guard for [LoggerMessage] instance methods.")]
public sealed partial class GlobalErrorHandlingMiddleware
{
    private readonly GlobalErrorHandlingOptions options;
    private readonly ExceptionMappingResolver resolver;
    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalErrorHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The delegate representing the remaining middleware in the request pipeline.</param>
    /// <param name="options">The options for this middleware.</param>
    /// <param name="loggerFactory">The logger factory used to log handled exceptions. When <see langword="null"/>, nothing is logged.</param>
    public GlobalErrorHandlingMiddleware(
        RequestDelegate next,
        GlobalErrorHandlingOptions? options = null,
        ILoggerFactory? loggerFactory = null)
    {
        this.next = next;
        this.options = options ?? new GlobalErrorHandlingOptions();
        resolver = this.options.BuildResolver();
        logger = loggerFactory is null
            ? NullLogger<GlobalErrorHandlingMiddleware>.Instance
            : loggerFactory.CreateLogger<GlobalErrorHandlingMiddleware>();
    }

    /// <summary>
    /// Invokes the middleware for processing HTTP requests.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// <see cref="OperationCanceledException"/> is treated specially:
    /// <list type="bullet">
    ///   <item>
    ///     If <see cref="HttpContext.RequestAborted"/> has fired (the upstream client
    ///     disconnected), the exception is silently swallowed because writing a response is
    ///     pointless and may itself throw on the closed socket.
    ///   </item>
    ///   <item>
    ///     Otherwise, the cancellation is treated as a server-side error (e.g. an inner
    ///     <c>HttpClient.Timeout</c> fired) and is mapped via the configured exception
    ///     resolver — by default to <c>504 Gateway Timeout</c>.
    ///   </item>
    /// </list>
    /// <para>
    /// If the response has already started when an exception is thrown, no error response can be written.
    /// The exception is logged and rethrown, so the server aborts the response (HTTP/1.1 closes the connection,
    /// HTTP/2 resets the stream) instead of completing a silently truncated response.
    /// </para>
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Upstream client disconnected — writing a response is pointless and may throw
            // because the socket is closed.
            LogRequestAbortedIfEnabled(context);
        }
        catch (Exception ex) when (context.Response.HasStarted)
        {
            // The status code and headers have already been sent, so no error response can be written.
            // Rethrow so the server aborts the response (HTTP/1.1 closes the connection, HTTP/2 resets the stream).
            // Otherwise, the client sees a silently truncated response.
            LogResponseAlreadyStartedIfEnabled(context, ex);
            throw;
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Logs, at <see cref="LogLevel.Debug"/>, that the request was aborted by the client.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    private void LogRequestAbortedIfEnabled(HttpContext context)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        using var scope = BeginClientIdsScope(context);

        LogRequestAborted(
            context.Request.Method,
            context.Request.Path.ToString());
    }

    /// <summary>
    /// Logs, at <see cref="LogLevel.Warning"/>, that an exception was thrown after the response had started,
    /// so no error response could be written.
    /// </summary>
    /// <remarks>
    /// The exception itself is not attached: it is rethrown, and the server (Kestrel) logs it at
    /// <see cref="LogLevel.Error"/> with the stack trace. Attaching it here would log every failure twice.
    /// Both entries share the hosting scope's RequestId.
    /// </remarks>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="exception">The exception to log.</param>
    private void LogResponseAlreadyStartedIfEnabled(
        HttpContext context,
        Exception exception)
    {
        if (!logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        using var scope = BeginClientIdsScope(context);

        LogResponseAlreadyStarted(
            exception.GetType().Name,
            context.Request.Method,
            context.Request.Path.ToString());
    }

    /// <summary>
    /// Handles the exception by writing the appropriate error response to the HTTP response.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="exception">The exception to handle.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var statusCode = resolver.ResolveStatusCode(exception);
        LogHandledException(context, exception, statusCode);

        // RFC 9457: a ProblemDetails body is application/problem+json. The plain dictionary body does not claim to be RFC 9457.
        context.Response.ContentType = options.UseProblemDetailsAsResponseBody
            ? MediaTypeNames.Application.ProblemJson
            : MediaTypeNames.Application.Json;
        context.Response.StatusCode = (int)statusCode;

        var exceptionResult = options.UseProblemDetailsAsResponseBody
            ? JsonSerializer.Serialize(CreateProblemDetails(context, exception, statusCode))
            : CreateMessage(context, exception, statusCode);

        return context.Response.WriteAsync(exceptionResult, context.RequestAborted);
    }

    /// <summary>
    /// Logs the handled exception: server errors (5xx) at <see cref="LogLevel.Error"/> with the exception,
    /// client errors (4xx) at <see cref="LogLevel.Debug"/> without the stack trace.
    /// </summary>
    /// <remarks>
    /// The client-supplied ids (<c>x-correlation-id</c>, <c>x-request-id</c>) are added as scope; ASP.NET Core's
    /// hosting scope already carries RequestId (<see cref="HttpContext.TraceIdentifier"/>), TraceId, SpanId and RequestPath.
    /// </remarks>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="statusCode">The HTTP status code the exception resolved to.</param>
    private void LogHandledException(
        HttpContext context,
        Exception exception,
        HttpStatusCode statusCode)
    {
        // Early return before any work (header lookup, scope allocation) when nothing will be written.
        if (!IsLoggingEnabledFor(statusCode))
        {
            return;
        }

        using var scope = BeginClientIdsScope(context);

        if (IsServerError(statusCode))
        {
            LogUnhandledServerError(
                exception,
                context.Request.Method,
                context.Request.Path.ToString(),
                (int)statusCode);
        }
        else
        {
            LogHandledClientError(
                exception.GetType().Name,
                context.Request.Method,
                context.Request.Path.ToString(),
                (int)statusCode,
                exception.Message);
        }
    }

    /// <summary>
    /// Begins a log scope with the client-supplied ids, so log entries can be matched with the
    /// <c>correlationId</c> and <c>requestId</c> fields in the error response body.
    /// </summary>
    /// <remarks>
    /// The <c>x-request-id</c> header value is logged as <c>ClientRequestId</c>, because <c>RequestId</c> is already
    /// used by ASP.NET Core's hosting scope for <see cref="HttpContext.TraceIdentifier"/> (the <c>traceId</c> in the body).
    /// </remarks>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The scope to dispose, or <see langword="null"/> when the request has none of the headers.</returns>
    private IDisposable? BeginClientIdsScope(HttpContext context)
    {
        var scopeValues = new Dictionary<string, object?>(StringComparer.Ordinal);

        var correlationId = context.GetCorrelationId();
        if (!string.IsNullOrEmpty(correlationId))
        {
            scopeValues[ErrorHandlingKeyConstants.LogScope.CorrelationId] = correlationId;
        }

        var requestId = context.GetRequestId();
        if (!string.IsNullOrEmpty(requestId))
        {
            scopeValues[ErrorHandlingKeyConstants.LogScope.ClientRequestId] = requestId;
        }

        return scopeValues.Count > 0
            ? logger.BeginScope(scopeValues)
            : null;
    }

    /// <summary>
    /// Determines whether the log level used for the given status code is enabled:
    /// <see cref="LogLevel.Error"/> for server errors (5xx), <see cref="LogLevel.Debug"/> for client errors (4xx).
    /// </summary>
    /// <param name="statusCode">The HTTP status code the exception resolved to.</param>
    /// <returns>
    /// <see langword="true"/> if a log entry would be written; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// When the middleware is created without an <see cref="ILoggerFactory"/>, the logger is a
    /// <see cref="NullLogger{T}"/>. Its <see cref="NullLogger{T}.IsEnabled(LogLevel)"/> always returns
    /// <see langword="false"/>, so this method returns <see langword="false"/> for every status code
    /// and the caller skips all logging work without a separate null-logger check.
    /// </remarks>
    private bool IsLoggingEnabledFor(HttpStatusCode statusCode)
    {
        var logLevel = IsServerError(statusCode)
            ? LogLevel.Error
            : LogLevel.Debug;

        return logger.IsEnabled(logLevel);
    }

    /// <summary>
    /// Determines whether the status code is a server error (5xx).
    /// </summary>
    /// <param name="statusCode">The HTTP status code to evaluate.</param>
    /// <returns><see langword="true"/> for 5xx status codes; otherwise, <see langword="false"/>.</returns>
    private static bool IsServerError(HttpStatusCode statusCode)
        => (int)statusCode >= StatusCodes.Status500InternalServerError;

    /// <summary>
    /// Gets the <c>type</c> and <c>title</c> that ASP.NET Core itself uses for the status code,
    /// so the middleware's error bodies match <c>TypedResults.Problem(statusCode)</c> in the same app.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <returns>A <see cref="ProblemDetails"/> with the framework's default <c>Type</c> and <c>Title</c>.</returns>
    /// <remarks>
    /// The defaults come from ASP.NET Core at runtime instead of a table kept here. Most titles are the RFC reason phrase
    /// (500 uses "An error occurred while processing your request."). Statuses without an RFC link (e.g. 418, 429)
    /// get no <c>Type</c>, which RFC 9457 treats as "about:blank".
    /// </remarks>
    private static ProblemDetails GetAspNetCoreProblemDetailsDefaults(
        HttpStatusCode statusCode)
        => TypedResults.Problem(statusCode: (int)statusCode).ProblemDetails;

    /// <summary>
    /// Creates a problem details object to include in the error response.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="exception">The exception to include in the problem details.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <returns>A <see cref="ProblemDetails"/> object representing the error details.</returns>
    private ProblemDetails CreateProblemDetails(
        HttpContext context,
        Exception exception,
        HttpStatusCode statusCode)
    {
        var defaults = GetAspNetCoreProblemDetailsDefaults(statusCode);
        var result = new ProblemDetails
        {
            Type = defaults.Type,
            Status = (int)statusCode,
            Title = defaults.Title,
            Detail = UseSimpleMessage(exception)
                ? exception.GetMessage()
                : exception.GetMessage(includeInnerMessage: true, includeExceptionName: true),
            Instance = context.Request.Path,
        };

        SetExtensionFields(result, context);

        return result;
    }

    /// <summary>
    /// Creates a message like a problem details object to include in the error response.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="exception">The exception to include in the problem details.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <returns>A JSON string representing the error details.</returns>
    private string CreateMessage(
        HttpContext context,
        Exception exception,
        HttpStatusCode statusCode)
    {
        var message = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = (int)statusCode,
            ["title"] = GetAspNetCoreProblemDetailsDefaults(statusCode).Title,
            ["instance"] = context.Request.Path.ToString(),
        };

        var detail = UseSimpleMessage(exception)
            ? exception.GetMessage()
            : exception.GetMessage(includeInnerMessage: true, includeExceptionName: true);

        if (!string.IsNullOrEmpty(detail))
        {
            message["detail"] = detail;
        }

        AddExtensionFields(message, context);

        return JsonSerializer.Serialize(message);
    }

    /// <summary>
    /// Sets extension fields in the problem details object.
    /// </summary>
    /// <param name="problemDetails">The problem details object to modify.</param>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    private static void SetExtensionFields(
        ProblemDetails problemDetails,
        HttpContext context)
        => AddExtensionFields(problemDetails.Extensions, context);

    /// <summary>
    /// Adds extension fields (correlationId, requestId, traceId) to a dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary to add fields to.</param>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    private static void AddExtensionFields(
        IDictionary<string, object?> dictionary,
        HttpContext context)
    {
        var correlationId = context.GetCorrelationId();
        if (!string.IsNullOrEmpty(correlationId))
        {
            dictionary[ErrorHandlingKeyConstants.ResponseBody.CorrelationId] = correlationId;
        }

        var requestId = context.GetRequestId();
        if (!string.IsNullOrEmpty(requestId))
        {
            dictionary[ErrorHandlingKeyConstants.ResponseBody.RequestId] = requestId;
        }

        var traceId = context.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
        {
            dictionary[ErrorHandlingKeyConstants.ResponseBody.TraceId] = traceId;
        }
    }

    /// <summary>
    /// Determines whether a simple message should be used based on the type of the exception.
    /// </summary>
    /// <param name="exception">The exception to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> if a simple message should be used for the specified exception types; otherwise, <see langword="false"/>.
    /// </returns>
    private bool UseSimpleMessage(Exception exception)
        => !options.IncludeException ||
           exception is BadHttpRequestException or UnauthorizedAccessException or NotImplementedException;
}