namespace Atc.Rest.MinimalApi.Middleware;

/// <summary>
/// Gets the field errors a <c>ValidationException</c> carries, keyed like the ValidationFilter keys a body
/// (<see cref="Filters.Endpoints.ValidationKeyStyle.WireNames"/>), so a handler that validates by itself answers like the filter.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>FluentValidation: one entry per <c>ValidationFailure</c> (<c>PropertyName</c> / <c>ErrorMessage</c>).</item>
///   <item>DataAnnotations: every name in <c>ValidationResult.MemberNames</c> gets the result's message; a result without
///   member names goes under the empty key <c>""</c>, as an error about the request as a whole.</item>
///   <item>Keys: each segment of the C# member path through the app's JSON naming policy (<c>Lines[2].Sku</c> →
///   <c>lines[2].sku</c>). The validated type is not known here, so a <c>[JsonPropertyName]</c> that differs from the
///   policy is not honoured.</item>
/// </list>
/// </remarks>
internal static class ValidationExceptionErrors
{
    /// <summary>
    /// Gets the field errors of a validation exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="namingPolicy">The app's JSON naming policy; <see langword="null"/> keeps the C# names.</param>
    /// <returns>
    /// The errors keyed by wire names, or <see langword="null"/> when the exception is not a validation exception or
    /// carries no failures (e.g. a FluentValidation <c>ValidationException</c> constructed with a message only).
    /// </returns>
    public static Dictionary<string, string[]>? TryCreate(
        Exception exception,
        JsonNamingPolicy? namingPolicy)
    {
        var failures = exception switch
        {
            FluentValidation.ValidationException fluentValidation => fluentValidation.Errors?
                .Select(f => (Key: f.PropertyName ?? string.Empty, Message: f.ErrorMessage))
                .ToList(),
            System.ComponentModel.DataAnnotations.ValidationException dataAnnotations => GetFailures(dataAnnotations),
            _ => null,
        };

        if (failures is null || failures.Count == 0)
        {
            return null;
        }

        return failures
            .GroupBy(f => MapKey(f.Key, namingPolicy), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.Message).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    private static List<(string Key, string Message)> GetFailures(
        System.ComponentModel.DataAnnotations.ValidationException exception)
    {
        var result = exception.ValidationResult;
        var message = result.ErrorMessage ?? exception.Message;
        var memberNames = result.MemberNames.Where(n => !string.IsNullOrEmpty(n)).ToList();

        return memberNames.Count == 0
            ? [(string.Empty, message)]
            : memberNames.Select(n => (n, message)).ToList();
    }

    private static string MapKey(
        string key,
        JsonNamingPolicy? namingPolicy)
    {
        if (string.IsNullOrEmpty(key) || namingPolicy is null)
        {
            return key;
        }

        return string.Join(
            '.',
            key
                .Split('.')
                .Select(segment =>
                {
                    var indexerStart = segment.IndexOf('[', StringComparison.Ordinal);
                    return indexerStart switch
                    {
                        < 0 => namingPolicy.ConvertName(segment),
                        0 => segment,
                        _ => namingPolicy.ConvertName(segment[..indexerStart]) + segment[indexerStart..],
                    };
                }));
    }
}