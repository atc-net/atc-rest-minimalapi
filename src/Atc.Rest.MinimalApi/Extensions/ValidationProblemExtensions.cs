// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable MemberCanBePrivate.Global
namespace Atc.Rest.MinimalApi.Extensions;

/// <summary>
/// Represents a validation filter that integrates with Minimal API endpoints.
/// This class provides the following functionality:
/// 1. Utilizes both data annotation validation and Fluent Validation to validate the incoming request against a specific model.
/// 2. Merges validation errors from both methods, ensuring that no duplicates exist, and returns a validation problem if errors are detected.
/// 3. Automatically resolves the proper serialization names for the validation keys/values using the provided object's properties (e.g., JsonPropertyName attributes).
/// </summary>
public static class ValidationProblemExtensions
{
    /// <summary>
    /// Resolves the serialization type names in the provided validation problem.
    /// </summary>
    /// <param name="validationProblem">The validation problem whose serialization type names need to be resolved.</param>
    /// <param name="skipFirstLevelOnValidationKeys">A boolean value indicating whether to skip the first level when resolving serialization type names. Default value is false.</param>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <returns>A validation problem with resolved serialization type names.</returns>
    public static ValidationProblem ResolveSerializationTypeNames<T>(
        this ValidationProblem validationProblem,
        bool skipFirstLevelOnValidationKeys = false)
        where T : class
        => ResolveSerializationTypeNames<T>(
            validationProblem.ProblemDetails.Errors,
            skipFirstLevelOnValidationKeys);

    /// <summary>
    /// Resolves the serialization type names in the provided dictionary of errors.
    /// </summary>
    /// <param name="errors">The dictionary of errors whose serialization type names need to be resolved.</param>
    /// <param name="skipFirstLevelOnValidationKeys">A boolean value indicating whether to skip the first level when resolving serialization type names. Default value is false.</param>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <returns>A validation problem with resolved serialization type names.</returns>
    public static ValidationProblem ResolveSerializationTypeNames<T>(
        this IDictionary<string, string[]> errors,
        bool skipFirstLevelOnValidationKeys = false)
        where T : class
    {
        var type = typeof(T);
        var propertyInfos = type.GetProperties();
        var newErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, values) in errors)
        {
            if (key.Contains('.', StringComparison.Ordinal))
            {
                DeepResolveSerializationNames(type, key, newErrors, values, skipFirstLevelOnValidationKeys);
            }
            else
            {
                var propertyInfo = propertyInfos.FirstOrDefault(x => x.Name.Equals(key, StringComparison.Ordinal));
                if (propertyInfo is null)
                {
                    AddOrMergeErrors(newErrors, key, values);
                    continue;
                }

                var jsonPropertyNameAttribute = propertyInfo
                    .GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false)
                    .FirstOrDefault() as JsonPropertyNameAttribute;

                var newKey = jsonPropertyNameAttribute?.Name ?? key;
                AddOrMergeErrors(newErrors, newKey, values);

                if (key != newKey)
                {
                    ReplaceSerializationTypeName(values, key, newKey);
                }
            }
        }

        return TypedResults.ValidationProblem(newErrors);
    }

    private static void DeepResolveSerializationNames(
        Type type,
        string key,
        IDictionary<string, string[]> newErrors,
        string[] values,
        bool skipFirstLevelOnValidationKeys = false)
    {
        var subType = type;
        var depth = key.Split('.');

        var newKey = new StringBuilder();
        string? name = null;
        var hasContent = false;

        while (depth.Any())
        {
            var errorName = depth[0];
            var propertyName = RemoveCollectionIndexer(errorName);
            var propertyInfo = subType.GetProperty(propertyName);

            if (propertyInfo is null)
            {
                depth = [.. depth.Skip(1)];

                if (hasContent)
                {
                    newKey.Append('.');
                }

                newKey.Append(propertyName);
                hasContent = true;
                continue;
            }

            subType = propertyInfo.PropertyType.IsGenericType
                ? propertyInfo.PropertyType.GenericTypeArguments[0]
                : propertyInfo.PropertyType;

            var jsonPropertyNameAttribute = propertyInfo
                .GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false)
                .FirstOrDefault() as JsonPropertyNameAttribute;

            depth = [.. depth.Skip(1)];
            name = jsonPropertyNameAttribute?.Name ?? propertyName;

            if (hasContent)
            {
                newKey.Append('.');
            }

            newKey.Append(name);
            hasContent = true;

            if (errorName.Contains('[', StringComparison.Ordinal) &&
                errorName.Contains(']', StringComparison.Ordinal))
            {
                var startIndex = errorName.IndexOf('[', StringComparison.Ordinal);
                var indexOf = errorName.IndexOf(']', StringComparison.Ordinal);
                newKey.Append(errorName.AsSpan(startIndex, indexOf - startIndex + 1));
            }
        }

        FormatAndAddValidationErrors(
            newErrors,
            newKey.ToString(),
            values,
            skipFirstLevelOnValidationKeys);

        var splitKey = key.Split('.');
        if (name is not null &&
            splitKey.Length > 0 && name != splitKey[^1])
        {
            ReplaceSerializationTypeName(values, splitKey[^1], name);
        }
    }

    /// <summary>
    /// Formats and adds validation errors to the provided dictionary, optionally skipping the first level of validation keys.
    /// </summary>
    /// <param name="newErrors">The dictionary to which the formatted validation errors will be added.</param>
    /// <param name="key">The key representing the specific validation error or group of errors.</param>
    /// <param name="values">The array of validation error messages associated with the key.</param>
    /// <param name="skipFirstLevelOnValidationKeys">A boolean flag that, if set to true, will cause the method to skip the first validation key level in the key, if present.</param>
    private static void FormatAndAddValidationErrors(
        IDictionary<string, string[]> newErrors,
        string key,
        string[] values,
        bool skipFirstLevelOnValidationKeys)
    {
        if (skipFirstLevelOnValidationKeys && key.Contains('.', StringComparison.Ordinal))
        {
            var depthKeyToSkip = key[..key.IndexOf('.', StringComparison.Ordinal)];

            var newValues = new List<string>();
            foreach (var value in values)
            {
                var startKeyToReplace = $"'{depthKeyToSkip} "; // FluentValidation adds 'Request <property_name>' around properties if no message specified

                newValues.Add(value.StartsWith(startKeyToReplace, StringComparison.Ordinal)
                    ? value.Replace(startKeyToReplace, "'", StringComparison.Ordinal)
                    : value);
            }

            AddOrMergeErrors(
                newErrors,
                key[(key.IndexOf('.', StringComparison.Ordinal) + 1)..],
                newValues.ToArray());
        }
        else
        {
            AddOrMergeErrors(newErrors, key, values);
        }
    }

    private static void AddOrMergeErrors(
        IDictionary<string, string[]> errors,
        string key,
        string[] values)
    {
        if (errors.TryGetValue(key, out var existingValues))
        {
            var merged = existingValues
                .Union(values, StringComparer.Ordinal)
                .ToArray();

            errors[key] = merged;
        }
        else
        {
            errors.Add(key, values);
        }
    }

    [SuppressMessage("Warning", "MA0009", Justification = "Regex is safe")]
    private static string RemoveCollectionIndexer(string errorName)
        => Regex.Replace(errorName, @"\[.*\]", string.Empty);

    private static void ReplaceSerializationTypeName(
        IList<string> values,
        string originalName,
        string serializedName)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i].Contains(originalName, StringComparison.Ordinal))
            {
                values[i] = values[i].Replace(originalName, serializedName, StringComparison.Ordinal);
            }
        }
    }
}