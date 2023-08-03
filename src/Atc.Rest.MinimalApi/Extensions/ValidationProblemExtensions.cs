// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
namespace Atc.Rest.MinimalApi.Extensions;

/// <summary>
/// Provides extension methods for handling validation problems and managing serialization types.
/// </summary>
public static class ValidationProblemExtensions
{
    /// <summary>
    /// Resolves the serialization type names in the provided validation problem.
    /// </summary>
    /// <param name="validationProblem">The validation problem whose serialization type names need to be resolved.</param>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <returns>A validation problem with resolved serialization type names.</returns>
    public static ValidationProblem ResolveSerializationTypeNames<T>(
        this ValidationProblem validationProblem)
        where T : class
        => ResolveSerializationTypeNames<T>(
            validationProblem.ProblemDetails.Errors);

    /// <summary>
    /// Resolves the serialization type names in the provided dictionary of errors.
    /// </summary>
    /// <param name="errors">The dictionary of errors whose serialization type names need to be resolved.</param>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <returns>A validation problem with resolved serialization type names.</returns>
    public static ValidationProblem ResolveSerializationTypeNames<T>(
        this IDictionary<string, string[]> errors)
        where T : class
    {
        var type = typeof(T);
        var newErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, values) in errors)
        {
            if (key.Contains('.', StringComparison.Ordinal))
            {
                DeepResolveSerializationNames(type, key, newErrors, values);
            }
            else
            {
                var jsonPropertyNameAttribute = type.GetProperty(key)!
                    .GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false)
                    .FirstOrDefault() as JsonPropertyNameAttribute;

                var newKey = jsonPropertyNameAttribute?.Name ?? key;
                newErrors.Add(newKey, values);

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
        string[] values)
    {
        var subType = type;
        var depth = key.Split('.');

        var newKey = new StringBuilder();
        string? name = null;

        while (depth.Any())
        {
            var errorName = depth[0];
            var propertyName = RemoveCollectionIndexer(errorName);
            var propertyInfo = subType.GetProperty(propertyName)!;

            if (propertyInfo is null)
            {
                depth = depth.Skip(1).ToArray();
                continue;
            }

            subType = propertyInfo.PropertyType.IsGenericType
                ? propertyInfo.PropertyType.GenericTypeArguments[0]
                : propertyInfo.PropertyType;

            var jsonPropertyNameAttribute = propertyInfo
                .GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false)
                .FirstOrDefault() as JsonPropertyNameAttribute;

            depth = depth.Skip(1).ToArray();
            name = jsonPropertyNameAttribute?.Name ?? propertyName;
            newKey.Append(name);

            if (errorName.Contains('[', StringComparison.Ordinal) &&
                errorName.Contains(']', StringComparison.Ordinal))
            {
                var startIndex = errorName.IndexOf("[", StringComparison.Ordinal);
                var indexOf = errorName.IndexOf("]", StringComparison.Ordinal);
                newKey.Append(errorName.AsSpan(startIndex, indexOf - startIndex + 1));
            }

            if (depth.Any())
            {
                newKey.Append('.');
            }
        }

        newErrors.Add(newKey.ToString(), values);

        var splitKey = key.Split('.');
        if (name is not null &&
            splitKey.Length > 0 && name != splitKey[^1])
        {
            ReplaceSerializationTypeName(values, splitKey[^1], name);
        }
    }

    [SuppressMessage("Warning", "MA0009", Justification = "Regex is safe")]
    private static string RemoveCollectionIndexer(
        string errorName)
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