namespace Atc.Rest.MinimalApi.Filters.Endpoints;

// ValidationFilter LoggerMessages (the type is documented in ValidationFilter.cs).
[SuppressMessage("Design", "MA0048:File name must match type name", Justification = "OK - By Design")]
public partial class ValidationFilter<T>
{
    private readonly ILogger<ValidationFilter<T>> logger;

    [LoggerMessage(
        EventId = LoggingEventIdConstants.ValidationFilter.MisconfiguredEndpoint,
        Level = LogLevel.Error,
        Message = "ValidationFilter found no argument of type {ArgumentType} for {RequestMethod} {RequestPath} - the endpoint is misconfigured, responding with status code 500")]
    private partial void LogMisconfiguredEndpoint(
        string argumentType,
        string requestMethod,
        string requestPath);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.ValidationFilter.SkipFirstLevelHasNoEffect,
        Level = LogLevel.Warning,
        Message = "SkipFirstLevelOnValidationKeys only applies to KeyStyle = MemberPath; with {KeyStyle} the keys are the names the client sent. Remove the option, or set KeyStyle = MemberPath to keep the 2.x keys")]
    private partial void LogSkipFirstLevelHasNoEffect(
        ValidationKeyStyle keyStyle);
}