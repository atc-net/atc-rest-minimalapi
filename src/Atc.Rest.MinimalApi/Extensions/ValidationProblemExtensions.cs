namespace Atc.Rest.MinimalApi.Extensions;

public static class ValidationProblemExtensions
{
    public static ValidationProblem ResolveSerializationTypeNames<T>(
        this ValidationProblem validationProblem)
        where T : class
        => ResolveSerializationTypeNames<T>(validationProblem.ProblemDetails.Errors);

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
                ResolveSerializationTypeName(values, key, newKey);
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

            subType = propertyInfo.PropertyType.IsGenericType
                ? propertyInfo.PropertyType.GenericTypeArguments[0]
                : propertyInfo.PropertyType;

            var jsonPropertyNameAttribute = propertyInfo
                .GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false)
                .FirstOrDefault() as JsonPropertyNameAttribute;

            depth = depth.Skip(1).ToArray();
            name = jsonPropertyNameAttribute?.Name;
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

        if (name != null)
        {
            ResolveSerializationTypeName(values, key.Split('.').Last(), name);
        }
    }

    [SuppressMessage("Warning", "MA0009", Justification = "Regex is safe")]
    private static string RemoveCollectionIndexer(
        string errorName)
        => Regex.Replace(errorName, @"\[.*\]", string.Empty);

    private static void ResolveSerializationTypeName(
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