namespace Atc.Rest.MinimalApi.Filters.Endpoints;

/// <summary>
/// Provides options for configuring validation filter behavior in Minimal API.
/// </summary>
public class ValidationFilterOptions
{
    /// <summary>
    /// Gets or initializes how the keys in the ValidationProblem <c>errors</c> object are named.
    /// Defaults to <see cref="ValidationKeyStyle.WireNames"/>: the names the client sent.
    /// </summary>
    /// <value>
    /// The key style. Use <see cref="ValidationKeyStyle.MemberPath"/> to keep the 2.x keys (C# member paths).
    /// </value>
    public ValidationKeyStyle KeyStyle { get; init; } = ValidationKeyStyle.WireNames;

    /// <summary>
    /// Gets or initializes a value indicating whether the first level should be skipped on validation keys.
    /// Only applies to <see cref="ValidationKeyStyle.MemberPath"/>; with another key style, the filter logs a warning once.
    /// </summary>
    /// <value>
    /// <c>true</c> if the first level should be skipped on validation keys; otherwise, <c>false</c>.
    /// </value>
    /// <example>
    /// If set to <c>true</c>, the validation mechanism will skip the first level on validation keys,
    /// which might be useful in certain scenarios where the hierarchy of the validated object needs to be flattened.
    /// </example>
    public bool SkipFirstLevelOnValidationKeys { get; init; }

    /// <summary>
    /// Gets or initializes how deep DataAnnotations validation walks into nested objects and collections.
    /// A request with validation rules deeper than this is rejected as a whole (a 400 with the error under the empty key
    /// <c>""</c>), never accepted unvalidated.
    /// </summary>
    /// <value>The maximum depth; 32 by default. Must not be negative.</value>
    public int MaxValidationDepth
    {
        get => maxValidationDepth;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            maxValidationDepth = value;
        }
    }

    private readonly int maxValidationDepth = DataAnnotationsGraphValidator.DefaultMaxDepth;

    private int skipFirstLevelWarningLogged;

    /// <summary>
    /// Marks that the "SkipFirstLevelOnValidationKeys has no effect" warning was logged for these options.
    /// </summary>
    /// <returns><see langword="true"/> the first time only.</returns>
    internal bool TryMarkSkipFirstLevelWarningLogged()
        => Interlocked.Exchange(ref skipFirstLevelWarningLogged, 1) == 0;
}