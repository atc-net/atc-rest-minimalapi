namespace Atc.Rest.MinimalApi.Middleware;

/// <summary>
/// GlobalErrorHandlingMiddleware LoggerMessages.
/// </summary>
[SuppressMessage("Design", "MA0048:File name must match type name", Justification = "OK - By Design")]
public sealed partial class GlobalErrorHandlingMiddleware
{
    private readonly ILogger<GlobalErrorHandlingMiddleware> logger;

    [LoggerMessage(
        EventId = LoggingEventIdConstants.GlobalErrorHandlingMiddleware.UnhandledServerError,
        Level = LogLevel.Error,
        Message = "Unhandled exception for {RequestMethod} {RequestPath} - responding with status code {StatusCode}")]
    private partial void LogUnhandledServerError(
        Exception exception,
        string requestMethod,
        string requestPath,
        int statusCode);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.GlobalErrorHandlingMiddleware.HandledClientError,
        Level = LogLevel.Debug,
        Message = "{ExceptionType} for {RequestMethod} {RequestPath} - responding with status code {StatusCode}: {ExceptionMessage}")]
    private partial void LogHandledClientError(
        string exceptionType,
        string requestMethod,
        string requestPath,
        int statusCode,
        string exceptionMessage);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.GlobalErrorHandlingMiddleware.RequestAborted,
        Level = LogLevel.Debug,
        Message = "Request {RequestMethod} {RequestPath} was aborted by the client - no response written")]
    private partial void LogRequestAborted(
        string requestMethod,
        string requestPath);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.GlobalErrorHandlingMiddleware.ResponseAlreadyStarted,
        Level = LogLevel.Warning,
        Message = "{ExceptionType} thrown for {RequestMethod} {RequestPath} after the response has started - rethrowing so the server aborts the response (the server logs the exception)")]
    private partial void LogResponseAlreadyStarted(
        string exceptionType,
        string requestMethod,
        string requestPath);
}