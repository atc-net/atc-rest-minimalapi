namespace Atc.Rest.MinimalApi.Options;

/// <summary>
/// Provides configuration settings for the global error handling middleware.
/// </summary>
public class GlobalErrorHandlingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the exception details should be included in the error response.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if exception details should be included; otherwise, <see langword="false"/>.
    /// </value>
    public bool IncludeException { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use problem details for the response body when handling errors.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if problem details should be used as the response body; otherwise, <see langword="false"/>.
    /// </value>
    public bool UseProblemDetailsAsResponseBody { get; set; } = true;

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(IncludeException)}: {IncludeException}, {nameof(UseProblemDetailsAsResponseBody)}: {UseProblemDetailsAsResponseBody}";
}