namespace Atc.Rest.MinimalApi.Middleware;

/// <summary>
/// A middleware component for handling uncaught exceptions globally across an application.
/// </summary>
public sealed partial class GlobalErrorHandlingMiddleware
{
    private readonly GlobalErrorHandlingOptions options;
    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalErrorHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The delegate representing the remaining middleware in the request pipeline.</param>
    /// <param name="options">The options for this middleware.</param>
    public GlobalErrorHandlingMiddleware(
        RequestDelegate next,
        GlobalErrorHandlingOptions? options = null)
    {
        this.options = options ?? new GlobalErrorHandlingOptions();

        this.next = next;
    }

    /// <summary>
    /// Invokes the middleware for processing HTTP requests.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or request was canceled - no error response needed
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
    private Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        // Can't modify response if it has already started
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        var statusCode = GetHttpStatusCodeByExceptionType(exception);
        context.Response.ContentType = MediaTypeNames.Application.Json;
        context.Response.StatusCode = (int)statusCode;

        var exceptionResult = options.UseProblemDetailsAsResponseBody
            ? JsonSerializer.Serialize(CreateProblemDetails(context, exception, statusCode))
            : CreateMessage(context, exception, statusCode);

        return context.Response.WriteAsync(exceptionResult, context.RequestAborted);
    }

    /// <summary>
    /// Determines the appropriate HTTP status code based on the exception type.
    /// </summary>
    /// <param name="exception">The exception to evaluate.</param>
    /// <returns>The corresponding HTTP status code.</returns>
    private static HttpStatusCode GetHttpStatusCodeByExceptionType(
        Exception exception)
        => exception switch
        {
            FluentValidation.ValidationException => HttpStatusCode.BadRequest,
            System.ComponentModel.DataAnnotations.ValidationException => HttpStatusCode.BadRequest,
            BadHttpRequestException => HttpStatusCode.BadRequest,
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            InvalidOperationException => HttpStatusCode.Conflict,
            NotImplementedException => HttpStatusCode.NotImplemented,
            TimeoutException => HttpStatusCode.GatewayTimeout,
            _ => HttpStatusCode.InternalServerError,
        };

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
        var result = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = statusCode.ToNormalizedString(),
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
            ["title"] = statusCode.ToNormalizedString(),
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
            dictionary["correlationId"] = correlationId;
        }

        var requestId = context.GetRequestId();
        if (!string.IsNullOrEmpty(requestId))
        {
            dictionary["requestId"] = requestId;
        }

        var traceId = context.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
        {
            dictionary["traceId"] = traceId;
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