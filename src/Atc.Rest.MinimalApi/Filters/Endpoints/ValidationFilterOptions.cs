namespace Atc.Rest.MinimalApi.Filters.Endpoints;

/// <summary>
/// Provides options for configuring validation filter behavior in Minimal API.
/// </summary>
public class ValidationFilterOptions
{
    /// <summary>
    /// Gets or initializes a value indicating whether the first level should be skipped on validation keys.
    /// </summary>
    /// <value>
    /// <c>true</c> if the first level should be skipped on validation keys; otherwise, <c>false</c>.
    /// </value>
    /// <example>
    /// If set to <c>true</c>, the validation mechanism will skip the first level on validation keys,
    /// which might be useful in certain scenarios where the hierarchy of the validated object needs to be flattened.
    /// </example>
    public bool SkipFirstLevelOnValidationKeys { get; init; }
}