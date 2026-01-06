namespace Atc.Rest.MinimalApi.Options;

/// <summary>
/// Provides configuration settings for the global error handling middleware.
/// </summary>
public sealed class GlobalErrorHandlingOptions
{
    private readonly List<(Type ExceptionType, HttpStatusCode StatusCode)> exceptionMappings = [];

    /// <summary>
    /// Gets or sets a value indicating whether the exception details should be included in the error response.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if exception details should be included; otherwise, <see langword="false"/>.
    /// </value>
    public bool IncludeException { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use problem details for the response body when handling errors.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if problem details should be used as the response body; otherwise, <see langword="false"/>.
    /// </value>
    public bool UseProblemDetailsAsResponseBody { get; set; } = true;

    /// <summary>
    /// Maps an exception type to a specific HTTP status code.
    /// Custom mappings take precedence over built-in default mappings.
    /// Inheritance is supported: derived exception types are checked before base types.
    /// </summary>
    /// <typeparam name="TException">The type of exception to map.</typeparam>
    /// <param name="statusCode">The HTTP status code to return when this exception type is thrown.</param>
    /// <returns>The current options instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// options.MapException&lt;OrderNotFoundException&gt;(HttpStatusCode.NotFound);
    /// options.MapException&lt;DuplicateResourceException&gt;(HttpStatusCode.Conflict);
    /// </code>
    /// </example>
    public GlobalErrorHandlingOptions MapException<TException>(
        HttpStatusCode statusCode)
        where TException : Exception
    {
        var exceptionType = typeof(TException);

        // Remove existing mapping if present (allows overwriting)
        exceptionMappings.RemoveAll(m => m.ExceptionType == exceptionType);

        // Add to the beginning for precedence (most recently added = highest priority)
        exceptionMappings.Insert(0, (exceptionType, statusCode));

        return this;
    }

    /// <summary>
    /// Maps an exception type to a specific HTTP status code using a non-generic overload.
    /// </summary>
    /// <param name="exceptionType">The type of exception to map. Must derive from <see cref="Exception"/>.</param>
    /// <param name="statusCode">The HTTP status code to return when this exception type is thrown.</param>
    /// <returns>The current options instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exceptionType"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="exceptionType"/> does not derive from <see cref="Exception"/>.</exception>
    public GlobalErrorHandlingOptions MapException(
        Type exceptionType,
        HttpStatusCode statusCode)
    {
        ArgumentNullException.ThrowIfNull(exceptionType);

        if (!typeof(Exception).IsAssignableFrom(exceptionType))
        {
            throw new ArgumentException(
                $"Type '{exceptionType.FullName}' must derive from System.Exception.",
                nameof(exceptionType));
        }

        // Remove existing mapping if present
        exceptionMappings.RemoveAll(m => m.ExceptionType == exceptionType);

        // Add to the beginning for precedence
        exceptionMappings.Insert(0, (exceptionType, statusCode));

        return this;
    }

    /// <summary>
    /// Builds an immutable exception mapping resolver from the configured mappings.
    /// This method should be called once after all mappings have been configured.
    /// </summary>
    /// <returns>An immutable resolver that can be used by the middleware.</returns>
    internal ExceptionMappingResolver BuildResolver()
        => new(exceptionMappings);

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(IncludeException)}: {IncludeException}, " +
           $"{nameof(UseProblemDetailsAsResponseBody)}: {UseProblemDetailsAsResponseBody}, " +
           $"CustomMappings: {exceptionMappings.Count}";
}