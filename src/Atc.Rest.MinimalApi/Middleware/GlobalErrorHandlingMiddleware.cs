namespace Atc.Rest.MinimalApi.Middleware;

/// <summary>
/// A middleware component for handling uncaught exceptions globally across an application.
/// </summary>
public sealed partial class GlobalErrorHandlingMiddleware
{
    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalErrorHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The delegate representing the remaining middleware in the request pipeline.</param>
    public GlobalErrorHandlingMiddleware(
        RequestDelegate next)
    {
        this.next = next;
    }

    /// <summary>
    /// Invokes the middleware for processing HTTP requests.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
    public async Task Invoke(
        HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Handles the exception by writing the appropriate error response to the HTTP response.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="exception">The exception to handle.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var statusCode = GetHttpStatusCodeByExceptionType(exception);
        context.Response.ContentType = MediaTypeNames.Application.Json;
        context.Response.StatusCode = (int)statusCode;
        var exceptionResult = JsonSerializer.Serialize(CreateProblemDetails(context, exception, statusCode));

        return context.Response.WriteAsync(exceptionResult, context.RequestAborted);
    }

    /// <summary>
    /// Determines the appropriate HTTP status code based on the exception type.
    /// </summary>
    /// <param name="exception">The exception to evaluate.</param>
    /// <returns>The corresponding HTTP status code.</returns>
    private static HttpStatusCode GetHttpStatusCodeByExceptionType(
        Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;

        var exceptionType = exception.GetType();
        if (exceptionType == typeof(FluentValidation.ValidationException) ||
            exceptionType == typeof(System.ComponentModel.DataAnnotations.ValidationException))
        {
            statusCode = HttpStatusCode.BadRequest;
        }
        else if (exceptionType == typeof(UnauthorizedAccessException))
        {
            statusCode = HttpStatusCode.Unauthorized;
        }
        else if (exceptionType == typeof(InvalidOperationException))
        {
            statusCode = HttpStatusCode.Conflict;
        }
        else if (exceptionType == typeof(NotImplementedException))
        {
            statusCode = HttpStatusCode.NotImplemented;
        }

        return statusCode;
    }

    /// <summary>
    /// Creates a problem details object to include in the error response.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="exception">The exception to include in the problem details.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <returns>A <see cref="ProblemDetails"/> object representing the error details.</returns>
    private static ProblemDetails CreateProblemDetails(
        HttpContext context,
        Exception exception,
        HttpStatusCode statusCode)
    {
        var result = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = EnsurePascalCaseAndSpacesBetweenWordsRegex().Replace(statusCode.ToString(), " $0"),
            Detail = exception.GetMessage(includeInnerMessage: true, includeExceptionName: true),
        };

        SetExtensionFields(result, context);

        return result;
    }

    /// <summary>
    /// Sets extension fields in the problem details object.
    /// </summary>
    /// <param name="problemDetails">The problem details object to modify.</param>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    private static void SetExtensionFields(
        ProblemDetails problemDetails,
        HttpContext context)
    {
        var correlationId = context.GetCorrelationId();
        var requestId = context.GetRequestId();
        var traceId = context.TraceIdentifier;

        if (!string.IsNullOrEmpty(correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        if (!string.IsNullOrEmpty(requestId))
        {
            problemDetails.Extensions["requestId"] = requestId;
        }

        if (!string.IsNullOrEmpty(traceId))
        {
            problemDetails.Extensions["traceId"] = traceId;
        }
    }

    /// <summary>
    /// Generates a regular expression that ensures pascal casing and spaces between words.
    /// </summary>
    /// <returns>A <see cref="Regex"/> object.</returns>
    [GeneratedRegex("(?<=[a-z])([A-Z])", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnsurePascalCaseAndSpacesBetweenWordsRegex();
}