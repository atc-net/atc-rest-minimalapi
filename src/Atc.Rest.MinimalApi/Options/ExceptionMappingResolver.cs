namespace Atc.Rest.MinimalApi.Options;

/// <summary>
/// Provides immutable, thread-safe resolution of exception types to HTTP status codes.
/// </summary>
internal sealed class ExceptionMappingResolver
{
    /// <summary>
    /// The default mappings used when no custom mapping is found.
    /// Uses FrozenDictionary for optimal read performance.
    /// </summary>
    private static readonly FrozenDictionary<Type, HttpStatusCode> DefaultMappings = new Dictionary<Type, HttpStatusCode>
    {
        [typeof(FluentValidation.ValidationException)] = HttpStatusCode.BadRequest,
        [typeof(System.ComponentModel.DataAnnotations.ValidationException)] = HttpStatusCode.BadRequest,
        [typeof(ArgumentException)] = HttpStatusCode.BadRequest,

        // ArgumentException subclasses that almost always signal a coding error (e.g. ThrowIfNull, an index out of range)
        // rather than bad client input, which is caught earlier by model binding and the ValidationFilter.
        [typeof(ArgumentNullException)] = HttpStatusCode.InternalServerError,
        [typeof(ArgumentOutOfRangeException)] = HttpStatusCode.InternalServerError,
        [typeof(NotImplementedException)] = HttpStatusCode.NotImplemented,
        [typeof(TimeoutException)] = HttpStatusCode.GatewayTimeout,
        [typeof(OperationCanceledException)] = HttpStatusCode.GatewayTimeout,
    }.ToFrozenDictionary();

    /// <summary>
    /// Custom mappings dictionary.
    /// </summary>
    private readonly FrozenDictionary<Type, HttpStatusCode> customMappings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionMappingResolver"/> class.
    /// </summary>
    /// <param name="mappings">The custom exception mappings (one per exception type).</param>
    public ExceptionMappingResolver(
        IEnumerable<(Type ExceptionType, HttpStatusCode StatusCode)> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);

        customMappings = mappings
            .ToDictionary(m => m.ExceptionType, m => m.StatusCode)
            .ToFrozenDictionary();
    }

    /// <summary>
    /// Resolves the HTTP status code for the given exception.
    /// </summary>
    /// <param name="exception">The exception to resolve.</param>
    /// <returns>The HTTP status code.</returns>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><description>Custom mappings: the most-derived mapped type in the exception's type hierarchy (an exact match first),
    ///   whatever order the mappings were added in</description></item>
    ///   <item><description><see cref="BadHttpRequestException"/>: its own <see cref="BadHttpRequestException.StatusCode"/>
    ///   (e.g. 413 when Kestrel rejects a body that is too large), as ASP.NET Core's exception handler does</description></item>
    ///   <item><description>Default mappings: the most-derived mapped type, as for custom mappings</description></item>
    ///   <item><description>Fallback to 500 Internal Server Error</description></item>
    /// </list>
    /// A custom mapping of a base type wins over a default mapping of a more-derived type.
    /// </remarks>
    public HttpStatusCode ResolveStatusCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var exceptionType = exception.GetType();

        return FindMostDerivedMapping(exceptionType, customMappings)
               ?? (exception is BadHttpRequestException badHttpRequestException
                   ? (HttpStatusCode?)badHttpRequestException.StatusCode
                   : null)
               ?? FindMostDerivedMapping(exceptionType, DefaultMappings)
               ?? HttpStatusCode.InternalServerError;
    }

    /// <summary>
    /// Walks up the exception's type hierarchy, so the most-derived mapped type wins
    /// (e.g. ArgumentOutOfRangeException -> 500 before its base ArgumentException -> 400).
    /// </summary>
    /// <param name="exceptionType">The type of the thrown exception.</param>
    /// <param name="mappings">The mappings to search.</param>
    /// <returns>The status code of the most-derived mapped type, or <see langword="null"/> when no type in the hierarchy is mapped.</returns>
    private static HttpStatusCode? FindMostDerivedMapping(
        Type exceptionType,
        FrozenDictionary<Type, HttpStatusCode> mappings)
    {
        for (var type = exceptionType; type is not null; type = type.BaseType)
        {
            if (mappings.TryGetValue(type, out var statusCode))
            {
                return statusCode;
            }
        }

        return null;
    }
}