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
        [typeof(BadHttpRequestException)] = HttpStatusCode.BadRequest,
        [typeof(ArgumentException)] = HttpStatusCode.BadRequest,
        [typeof(UnauthorizedAccessException)] = HttpStatusCode.Unauthorized,
        [typeof(InvalidOperationException)] = HttpStatusCode.Conflict,
        [typeof(NotImplementedException)] = HttpStatusCode.NotImplemented,
        [typeof(TimeoutException)] = HttpStatusCode.GatewayTimeout,
        [typeof(OperationCanceledException)] = HttpStatusCode.GatewayTimeout,
    }.ToFrozenDictionary();

    /// <summary>
    /// Custom mappings dictionary.
    /// </summary>
    private readonly FrozenDictionary<Type, HttpStatusCode> customMappings;

    /// <summary>
    /// Ordered list of custom exception types for inheritance checking.
    /// Order matters: more specific types should be checked first.
    /// </summary>
    private readonly IReadOnlyList<Type> orderedCustomTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionMappingResolver"/> class.
    /// </summary>
    /// <param name="mappings">The custom exception mappings in priority order.</param>
    public ExceptionMappingResolver(
        IEnumerable<(Type ExceptionType, HttpStatusCode StatusCode)> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);

        var mappingsList = mappings.ToList();

        customMappings = mappingsList
            .ToDictionary(m => m.ExceptionType, m => m.StatusCode)
            .ToFrozenDictionary();

        // Preserve order for inheritance checking
        orderedCustomTypes = mappingsList
            .Select(m => m.ExceptionType)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Resolves the HTTP status code for the given exception.
    /// </summary>
    /// <param name="exception">The exception to resolve.</param>
    /// <returns>The HTTP status code.</returns>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><description>Exact match in custom mappings</description></item>
    ///   <item><description>Inheritance check in custom mappings (ordered, most specific first)</description></item>
    ///   <item><description>Exact match in default mappings</description></item>
    ///   <item><description>Inheritance check in default mappings</description></item>
    ///   <item><description>Fallback to 500 Internal Server Error</description></item>
    /// </list>
    /// </remarks>
    public HttpStatusCode ResolveStatusCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var exceptionType = exception.GetType();

        // 1. Try exact match in custom mappings first
        if (customMappings.TryGetValue(exceptionType, out var customStatusCode))
        {
            return customStatusCode;
        }

        // 2. Try inheritance check in custom mappings (ordered)
        foreach (var mappedType in orderedCustomTypes)
        {
            if (mappedType.IsAssignableFrom(exceptionType))
            {
                return customMappings[mappedType];
            }
        }

        // 3. Try exact match in default mappings
        if (DefaultMappings.TryGetValue(exceptionType, out var defaultStatusCode))
        {
            return defaultStatusCode;
        }

        // 4. Try inheritance check in default mappings
        foreach (var (mappedType, statusCode) in DefaultMappings)
        {
            if (mappedType.IsAssignableFrom(exceptionType))
            {
                return statusCode;
            }
        }

        // 5. Fallback
        return HttpStatusCode.InternalServerError;
    }
}