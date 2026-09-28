namespace Atc.Rest.MinimalApi.Filters.Endpoints;

/// <summary>
/// Maps validation error keys from C# member paths (as produced by DataAnnotations/MiniValidation and FluentValidation)
/// to the names the client sent (<see cref="ValidationKeyStyle.WireNames"/>).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>A parameters wrapper (e.g. an <c>[AsParameters]</c> record): the <c>[FromBody]</c> member's segment is dropped
///   (the body is the root), route/query/header members use their binding <c>Name</c>, other members keep their name.</item>
///   <item>Body segments use <c>[JsonPropertyName]</c> when present, otherwise the app's JSON naming policy.</item>
///   <item>Collection indexers are kept (<c>lines[2].sku</c>). Segments that don't resolve to a property are kept as they are.</item>
///   <item>Keys that map to the same wire name (e.g. route <c>id</c> and body <c>id</c>) are merged.</item>
///   <item>An error about the <c>[FromBody]</c> member itself (the body is missing or <c>null</c>) goes under the empty key
///   <c>""</c>, and its <c>[Required]</c> message becomes <see cref="MissingBodyMessage"/>, as in ASP.NET Core MVC.
///   Other messages are not rewritten.</item>
/// </list>
/// </remarks>
internal static class WireNameValidationKeyMapper
{
    /// <summary>
    /// Maps all error keys and merges errors whose keys map to the same wire name.
    /// </summary>
    /// <param name="validatedType">The type validated by the filter.</param>
    /// <param name="isParametersWrapper">Whether <paramref name="validatedType"/> is a parameters wrapper (e.g. <c>[AsParameters]</c>).</param>
    /// <param name="errors">The errors keyed by C# member paths.</param>
    /// <param name="namingPolicy">The app's JSON naming policy; <see langword="null"/> keeps the C# names.</param>
    /// <returns>The errors keyed by wire names.</returns>
    public static Dictionary<string, string[]> Map(
        Type validatedType,
        bool isParametersWrapper,
        IDictionary<string, string[]> errors,
        JsonNamingPolicy? namingPolicy)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, messages) in errors)
        {
            var wireKey = MapKey(validatedType, isParametersWrapper, key, namingPolicy);
            var wireMessages = IsFromBodyMember(validatedType, isParametersWrapper, key)
                ? messages.Select(m => IsMissingValueMessage(m, key) ? MissingBodyMessage : m).ToArray()
                : messages;

            result[wireKey] = result.TryGetValue(wireKey, out var existing)
                ? existing.Union(wireMessages, StringComparer.Ordinal).ToArray()
                : wireMessages;
        }

        return result;
    }

    /// <summary>
    /// The message for a missing (or <c>null</c>) request body, as ASP.NET Core MVC words it.
    /// </summary>
    internal const string MissingBodyMessage = "A non-empty request body is required.";

    /// <summary>
    /// Whether a message is a validator's "missing value" message for the member: DataAnnotations' [Required]
    /// ("The Request field is required.") or FluentValidation's NotNull / NotEmpty ("'Request' must not be empty.").
    /// </summary>
    private static bool IsMissingValueMessage(
        string message,
        string memberName)
    {
        var displayName = Regex.Replace(memberName, "(?<=[a-z0-9])(?=[A-Z])", " ", RegexOptions.None, TimeSpan.FromSeconds(1));
        return string.Equals(message, $"The {memberName} field is required.", StringComparison.Ordinal) ||
               string.Equals(message, $"'{displayName}' must not be empty.", StringComparison.Ordinal) ||
               string.Equals(message, $"'{displayName}' must not be null.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether the key is a parameters wrapper's <c>[FromBody]</c> member itself: an error about the whole body.
    /// </summary>
    private static bool IsFromBodyMember(
        Type validatedType,
        bool isParametersWrapper,
        string key)
        => isParametersWrapper &&
           !key.Contains('.', StringComparison.Ordinal) &&
           !key.Contains('[', StringComparison.Ordinal) &&
           validatedType.GetProperty(key) is { } member &&
           GetAttributes(member).OfType<IFromBodyMetadata>().Any();

    /// <summary>
    /// Determines whether the type looks like a parameters wrapper, i.e. has at least one member with a binding attribute.
    /// Used when the endpoint's handler metadata is not available.
    /// </summary>
    /// <param name="type">The validated type.</param>
    /// <returns><see langword="true"/> if any member has a binding source attribute.</returns>
    public static bool HasBindingSourceMembers(Type type)
        => type
            .GetProperties()
            .Any(p => GetAttributes(p).Any(a => a is IFromBodyMetadata or IFromQueryMetadata or IFromRouteMetadata or IFromHeaderMetadata or IFromFormMetadata));

    /// <summary>
    /// Gets the members of the type that are bound from the request body.
    /// </summary>
    /// <param name="type">The validated type.</param>
    /// <returns>The <c>[FromBody]</c> properties (the attribute may be on the property or the constructor parameter).</returns>
    public static IEnumerable<PropertyInfo> GetFromBodyProperties(Type type)
        => type
            .GetProperties()
            .Where(p => GetAttributes(p).OfType<IFromBodyMetadata>().Any());

    private static string MapKey(
        Type validatedType,
        bool isParametersWrapper,
        string key,
        JsonNamingPolicy? namingPolicy)
    {
        if (string.IsNullOrEmpty(key))
        {
            return key;
        }

        var segments = key.Split('.');
        if (!isParametersWrapper)
        {
            // The validated type is the body itself: every segment is a JSON member.
            return MapBodySegments(validatedType, segments, startIndex: 0, namingPolicy);
        }

        var (memberName, indexer) = SplitIndexer(segments[0]);
        var member = validatedType.GetProperty(memberName);
        if (member is null)
        {
            return key;
        }

        var attributes = GetAttributes(member);
        if (attributes.OfType<IFromBodyMetadata>().Any())
        {
            // The body is the root of the request: drop the member's own segment. An error about the body as a
            // whole (e.g. it is missing) has nothing left and goes under the empty key, as in ASP.NET Core MVC.
            if (segments.Length == 1)
            {
                return indexer.Length == 0 ? string.Empty : key;
            }

            return MapBodySegments(UnwrapElementType(member.PropertyType), segments, startIndex: 1, namingPolicy);
        }

        var bindingName = GetBindingName(attributes) ?? memberName;
        if (segments.Length == 1)
        {
            return bindingName + indexer;
        }

        return bindingName + indexer + "." + MapBodySegments(UnwrapElementType(member.PropertyType), segments, startIndex: 1, namingPolicy);
    }

    private static string MapBodySegments(
        Type type,
        string[] segments,
        int startIndex,
        JsonNamingPolicy? namingPolicy)
    {
        var parts = new List<string>(segments.Length - startIndex);
        Type? currentType = type;

        for (var i = startIndex; i < segments.Length; i++)
        {
            var (name, indexer) = SplitIndexer(segments[i]);
            var property = currentType?.GetProperty(name);
            if (property is null)
            {
                // Not a property (e.g. a FluentValidation OverridePropertyName): keep this and the following segments as they are.
                parts.Add(segments[i]);
                currentType = null;
                continue;
            }

            parts.Add(GetJsonName(property, namingPolicy) + indexer);
            currentType = UnwrapElementType(property.PropertyType);
        }

        return string.Join('.', parts);
    }

    private static string GetJsonName(
        PropertyInfo property,
        JsonNamingPolicy? namingPolicy)
    {
        var jsonPropertyName = GetAttributes(property).OfType<JsonPropertyNameAttribute>().FirstOrDefault();
        if (jsonPropertyName is not null)
        {
            return jsonPropertyName.Name;
        }

        return namingPolicy?.ConvertName(property.Name) ?? property.Name;
    }

    private static string? GetBindingName(
        IReadOnlyCollection<object> attributes)
    {
        var name = attributes.OfType<IFromRouteMetadata>().Select(a => a.Name).FirstOrDefault()
                   ?? attributes.OfType<IFromQueryMetadata>().Select(a => a.Name).FirstOrDefault()
                   ?? attributes.OfType<IFromHeaderMetadata>().Select(a => a.Name).FirstOrDefault()
                   ?? attributes.OfType<IFromFormMetadata>().Select(a => a.Name).FirstOrDefault();

        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>
    /// Gets the attributes of a property, including those on the matching constructor parameter
    /// (positional records without the <c>property:</c> target put them there).
    /// </summary>
    private static IReadOnlyCollection<object> GetAttributes(
        PropertyInfo property)
    {
        var attributes = property.GetCustomAttributes(inherit: true).ToList();

        var declaringType = property.DeclaringType;
        if (declaringType is null)
        {
            return attributes;
        }

        foreach (var constructor in declaringType.GetConstructors())
        {
            var parameter = constructor
                .GetParameters()
                .FirstOrDefault(p => string.Equals(p.Name, property.Name, StringComparison.Ordinal));

            if (parameter is not null)
            {
                attributes.AddRange(parameter.GetCustomAttributes(inherit: true));
            }
        }

        return attributes;
    }

    private static (string Name, string Indexer) SplitIndexer(string segment)
    {
        var indexerStart = segment.IndexOf('[', StringComparison.Ordinal);
        return indexerStart < 0
            ? (segment, string.Empty)
            : (segment[..indexerStart], segment[indexerStart..]);
    }

    /// <summary>
    /// Unwraps nullable, array, dictionary and enumerable types to the type whose properties the next segment refers to.
    /// </summary>
    private static Type UnwrapElementType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string))
        {
            return underlying;
        }

        if (underlying.IsArray)
        {
            return underlying.GetElementType()!;
        }

        var dictionary = underlying
            .GetInterfaces()
            .Append(underlying)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        if (dictionary is not null)
        {
            return dictionary.GetGenericArguments()[1];
        }

        var enumerable = underlying
            .GetInterfaces()
            .Append(underlying)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0] ?? underlying;
    }
}