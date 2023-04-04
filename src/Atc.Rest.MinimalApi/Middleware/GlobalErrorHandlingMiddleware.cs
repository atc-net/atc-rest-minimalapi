namespace Atc.Rest.MinimalApi.Middleware;

public sealed class GlobalErrorHandlingMiddleware
{
    // TODO: Re-write as a source-generated Regex
    private static readonly Regex EnsurePascalCaseAndSpacesBetweenWordsRegex =
        new("(?<=[a-z])([A-Z])", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private readonly RequestDelegate next;

    public GlobalErrorHandlingMiddleware(
        RequestDelegate next)
    {
        this.next = next;
    }

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

    private static ProblemDetails CreateProblemDetails(
        HttpContext context,
        Exception exception,
        HttpStatusCode statusCode)
    {
        var result = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = EnsurePascalCaseAndSpacesBetweenWordsRegex.Replace(statusCode.ToString(), " $1"),
            Detail = exception.GetMessage(includeInnerMessage: true, includeExceptionName: true),
        };

        SetExtensionFields(result, context);

        return result;
    }

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
}