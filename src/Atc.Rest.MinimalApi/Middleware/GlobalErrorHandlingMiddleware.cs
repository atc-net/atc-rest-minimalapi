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
    private Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
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
    {
        var statusCode = HttpStatusCode.InternalServerError;

        var exceptionType = exception.GetType();
        if (exceptionType == typeof(FluentValidation.ValidationException) ||
            exceptionType == typeof(System.ComponentModel.DataAnnotations.ValidationException) ||
            exceptionType == typeof(BadHttpRequestException))
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
    private ProblemDetails CreateProblemDetails(
        HttpContext context,
        Exception exception,
        HttpStatusCode statusCode)
    {
        var result = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = statusCode.ToNormalizedString(),
        };

        if (exception is not null)
        {
            if (UseSimpleMessage(exception))
            {
                result.Detail = exception.GetMessage();
            }
            else
            {
                result.Detail = options.IncludeException
                    ? exception.GetMessage(includeInnerMessage: true, includeExceptionName: true)
                    : exception.GetMessage();
            }
        }

        SetExtensionFields(result, context);

        return result;
    }

    /// <summary>
    /// Creates a message like a problem details object to include in the error response.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="exception">The exception to include in the problem details.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <returns>A <see cref="ProblemDetails"/> object representing the error details.</returns>
    private string CreateMessage(
        HttpContext context,
        Exception exception,
        HttpStatusCode statusCode)
    {
        var sb = new StringBuilder();

        sb.AppendLine("{");
        sb.Append(2, "status: ");
        sb.AppendLine(((int)statusCode).ToString(GlobalizationConstants.EnglishCultureInfo));
        sb.Append(2, "title: ");
        sb.AppendLine(statusCode.ToNormalizedString());

        if (exception is not null)
        {
            if (UseSimpleMessage(exception))
            {
                sb.Append(2, "detail: ");
                sb.AppendLine(exception.GetMessage());
            }
            else
            {
                sb.Append(2, "detail: ");
                sb.AppendLine(options.IncludeException
                    ? exception.GetMessage(includeInnerMessage: true, includeExceptionName: true)
                    : exception.GetMessage());
            }
        }

        var correlationId = context.GetCorrelationId();
        if (!string.IsNullOrEmpty(correlationId))
        {
            sb.Append(2, "correlationId: ");
            sb.AppendLine(correlationId);
        }

        var requestId = context.GetRequestId();
        if (!string.IsNullOrEmpty(requestId))
        {
            sb.Append(2, "requestId: ");
            sb.AppendLine(requestId);
        }

        var traceId = context.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
        {
            sb.Append(2, "traceId: ");
            sb.AppendLine(traceId);
        }

        sb.Append('}');

        return sb.ToString();
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
    /// Determines whether a simple message should be used based on the type of the exception.
    /// </summary>
    /// <param name="exception">The exception to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> if a simple message should be used for the specified exception types; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool UseSimpleMessage(
        Exception exception)
    {
        var exceptionType = exception.GetType();
        return exceptionType == typeof(BadHttpRequestException) ||
               exceptionType == typeof(UnauthorizedAccessException) ||
               exceptionType == typeof(NotImplementedException);
    }
}