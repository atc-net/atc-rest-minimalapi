namespace Atc.Rest.MinimalApi.Validation;

/// <summary>
/// Validates an object graph with <see cref="System.ComponentModel.DataAnnotations"/>: the validation attributes of each
/// property (also those on the matching constructor parameter of a positional record), type-level validation attributes
/// and <see cref="IValidatableObject"/>, recursing into nested objects, collections and dictionaries.
/// </summary>
/// <remarks>
/// <para>
/// Inspired by Damian Edwards' MiniValidation (https://github.com/DamianEdwards/MiniValidation), whose behaviour it keeps:
/// keys are C# member paths (<c>Lines[2].Sku</c>, a dictionary entry by position: <c>Map[1].Value.Sku</c>), messages are
/// and the attributes' own messages.
/// </para>
/// <para>
/// Nested values are walked up to a maximum depth (<see cref="DefaultMaxDepth"/>, the validated object is at depth 0).
/// A value with validation rules below that depth is not left unvalidated: the request is rejected as a whole, with
/// <see cref="TooDeepMessage"/> under the empty key <c>""</c>. (MiniValidation stopped there without an error, so a too
/// deep body passed.)
/// </para>
/// <para>
/// Type-level attributes and <see cref="IValidatableObject.Validate"/> of an object run only when no error was found in
/// its members, nested objects included; <c>Validate</c> also not when a type-level attribute failed. That is the order of
/// <c>Microsoft.Extensions.Validation</c> (.NET 10), and <see cref="Validator"/> skips them too. MiniValidation ran both
/// whatever its members' result.
/// </para>
/// <para>
/// Unlike MiniValidation 0.10, objects are tracked by reference: two equal records in one request (records compare by
/// value) are both validated.
/// </para>
/// <para>
/// Only the runtime type of a value decides whether it is walked, from the declared types reachable from it: a value
/// whose type has no validation rules anywhere below it (e.g. <see cref="HttpContext"/> or a service on a parameters
/// record) is not read.
/// </para>
/// </remarks>
internal static class DataAnnotationsGraphValidator
{
    /// <summary>
    /// The default maximum depth: values are walked into from objects at this depth or less (the validated object is at
    /// depth 0).
    /// </summary>
    public const int DefaultMaxDepth = 32;

    /// <summary>
    /// The message for a request that has validation rules deeper than the maximum depth; <c>{0}</c> is the depth.
    /// </summary>
    internal const string TooDeepMessage = "The request body is nested too deeply to validate (more than {0} levels).";

    private const string SkipRecursionAttributeFullName = "MiniValidation.SkipRecursionAttribute";

    private static readonly ConcurrentDictionary<Type, TypeDetails> TypeDetailsCache = new();
    private static readonly ConcurrentDictionary<Type, bool> RequiresValidationCache = new();

    /// <summary>
    /// Validates the object and everything below it.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="errors">The errors, keyed by C# member path; empty when the object is valid.</param>
    /// <returns><see langword="true"/> if the object is valid.</returns>
    public static bool TryValidate(
        object target,
        out Dictionary<string, string[]> errors)
        => TryValidate(target, DefaultMaxDepth, out errors);

    /// <summary>
    /// Validates the object and everything below it, up to the given depth.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="maxDepth">The maximum depth (see <see cref="DefaultMaxDepth"/>).</param>
    /// <param name="errors">The errors, keyed by C# member path; empty when the object is valid.</param>
    /// <returns><see langword="true"/> if the object is valid.</returns>
    public static bool TryValidate(
        object target,
        int maxDepth,
        out Dictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var run = new ValidationRun(maxDepth);

        // The validated object itself: its own members, and its items when it is a collection.
        ValidateObject(target, prefix: string.Empty, depth: 0, run);
        if (target is IEnumerable rootItems and not string)
        {
            ValidateItems(rootItems, key: string.Empty, depth: 1, run);
        }

        errors = run.Errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
        return errors.Count == 0;
    }

    /// <summary>
    /// Validates a member's value: a collection by its items, any other object by its members.
    /// </summary>
    private static void ValidateValue(
        object value,
        string key,
        int depth,
        ValidationRun run)
    {
        if (value is IEnumerable items and not string)
        {
            ValidateItems(items, key, depth, run);
            return;
        }

        ValidateObject(value, key, depth, run);
    }

    private static void ValidateItems(
        IEnumerable items,
        string key,
        int depth,
        ValidationRun run)
    {
        var index = 0;
        foreach (var item in items)
        {
            if (item is not null && RequiresValidation(item.GetType()))
            {
                ValidateObject(item, $"{key}[{index}]", depth, run);
            }

            index++;
        }
    }

    /// <summary>
    /// Whether a member's value is walked into: an object whose type has rules below it, or a collection whose element
    /// type has them. A collection whose element type says nothing (<see cref="object"/>, an interface, an abstract
    /// class, a non-generic collection) is walked, and each item is then checked by its own type.
    /// </summary>
    /// <remarks>
    /// The "unknown element type" rule applies only to a value being walked, never in <see cref="RequiresValidation"/>:
    /// there it would mark e.g. <see cref="HttpContext"/> as validatable (<c>Request.Form.Files</c> is a collection of an
    /// interface), and reading <c>Request.Form</c> throws for a request that is not a form.
    /// </remarks>
    private static bool ShouldWalk(object value)
    {
        var type = value.GetType();
        if (value is IEnumerable and not string &&
            GetElementType(type) is { } elementType &&
            (elementType == typeof(object) || elementType.IsInterface || elementType.IsAbstract))
        {
            return true;
        }

        return RequiresValidation(type);
    }

    private static void ValidateObject(
        object target,
        string prefix,
        int depth,
        ValidationRun run)
    {
        // By reference: a cycle stops here, but an equal record elsewhere in the graph is still validated.
        if (!run.Visited.Add(target))
        {
            return;
        }

        var details = GetTypeDetails(target.GetType());
        var errorCountBefore = run.ErrorCount;

        foreach (var property in details.Properties)
        {
            var value = property.Info.GetValue(target);
            var key = AppendMember(prefix, property.Info.Name);

            if (property.Attributes.Length > 0)
            {
                var context = new ValidationContext(target)
                {
                    MemberName = property.Info.Name,
                    DisplayName = property.DisplayName,
                };

                var results = new List<ValidationResult>();
                if (!Validator.TryValidateValue(value!, context, results, property.Attributes))
                {
                    AddErrors(run, key, results);
                }
            }

            if (value is null ||
                property.SkipRecursion ||
                !ShouldWalk(value))
            {
                continue;
            }

            if (depth <= run.MaxDepth)
            {
                ValidateValue(value, key, depth + 1, run);
            }
            else if (!run.TooDeep)
            {
                // Never a silent pass: what cannot be validated rejects the request as a whole.
                run.TooDeep = true;
                AddError(run, string.Empty, string.Format(System.Globalization.CultureInfo.InvariantCulture, TooDeepMessage, run.MaxDepth));
            }
        }

        // As Microsoft.Extensions.Validation: the object as a whole is checked only when its members (and everything below
        // them) passed, and Validate only when the type-level attributes passed too.
        if (run.ErrorCount > errorCountBefore)
        {
            return;
        }

        if (details.TypeAttributes.Length > 0)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateValue(target, new ValidationContext(target), results, details.TypeAttributes))
            {
                AddErrors(run, prefix, results);
                return;
            }
        }

        if (target is IValidatableObject validatable)
        {
            foreach (var result in validatable.Validate(new ValidationContext(target)))
            {
                if (result is null || result == ValidationResult.Success)
                {
                    continue;
                }

                var memberNames = result.MemberNames.Where(n => !string.IsNullOrEmpty(n)).ToList();
                if (memberNames.Count == 0)
                {
                    AddError(run, prefix, result.ErrorMessage);
                    continue;
                }

                foreach (var memberName in memberNames)
                {
                    AddError(run, AppendMember(prefix, memberName), result.ErrorMessage);
                }
            }
        }
    }

    private static void AddErrors(
        ValidationRun run,
        string key,
        List<ValidationResult> results)
    {
        foreach (var result in results)
        {
            AddError(run, key, result.ErrorMessage);
        }
    }

    private static void AddError(
        ValidationRun run,
        string key,
        string? message)
    {
        if (!run.Errors.TryGetValue(key, out var messages))
        {
            messages = [];
            run.Errors[key] = messages;
        }

        messages.Add(message ?? string.Empty);
        run.ErrorCount++;
    }

    private static string AppendMember(
        string prefix,
        string memberName)
        => prefix.Length == 0 ? memberName : prefix + "." + memberName;

    /// <summary>
    /// Whether a value of the type can have validation errors: the type, or a type reachable from its properties
    /// (collection elements included), has validation attributes or implements <see cref="IValidatableObject"/>.
    /// </summary>
    private static bool RequiresValidation(Type type)
        => RequiresValidationCache.GetOrAdd(type, ComputeRequiresValidation);

    private static bool ComputeRequiresValidation(Type type)
    {
        if (IsSimple(type))
        {
            return false;
        }

        var seen = new HashSet<Type> { type };
        var queue = new Queue<Type>();
        queue.Enqueue(type);

        while (queue.TryDequeue(out var current))
        {
            var elementType = GetElementType(current);
            if (elementType is not null)
            {
                if (!IsSimple(elementType) && seen.Add(elementType))
                {
                    queue.Enqueue(elementType);
                }

                continue;
            }

            var details = GetTypeDetails(current);
            if (details.HasRules)
            {
                return true;
            }

            foreach (var childType in details.ChildTypes)
            {
                if (seen.Add(childType))
                {
                    queue.Enqueue(childType);
                }
            }
        }

        return false;
    }

    private static TypeDetails GetTypeDetails(Type type)
        => TypeDetailsCache.GetOrAdd(type, CreateTypeDetails);

    private static TypeDetails CreateTypeDetails(Type type)
    {
        var constructorParameters = type
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .ToList();

        var properties = new List<PropertyDetails>();
        var childTypes = new List<Type>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            // A positional record without the property: target puts the attributes on the constructor parameter.
            var parameter = constructorParameters.FirstOrDefault(p =>
                string.Equals(p.Name, property.Name, StringComparison.Ordinal) &&
                p.ParameterType == property.PropertyType);
            var attributes = (parameter?.GetCustomAttributes() ?? [])
                .Concat(property.GetCustomAttributes(inherit: true))
                .ToList();

            var validationAttributes = attributes.OfType<ValidationAttribute>().ToArray();
            var display = attributes.OfType<System.ComponentModel.DataAnnotations.DisplayAttribute>().FirstOrDefault();
            var skipRecursion = attributes.Any(a => string.Equals(a.GetType().FullName, SkipRecursionAttributeFullName, StringComparison.Ordinal));
            var propertyIsSimple = IsSimple(property.PropertyType);

            if (validationAttributes.Length == 0 && (propertyIsSimple || skipRecursion))
            {
                // Nothing to check on it and nothing to walk into: the value is never read.
                continue;
            }

            properties.Add(new PropertyDetails(
                property,
                validationAttributes,
                display?.GetName() ?? property.Name,
                skipRecursion));

            if (!propertyIsSimple && !skipRecursion)
            {
                childTypes.Add(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
            }
        }

        var typeAttributes = type.GetCustomAttributes<ValidationAttribute>(inherit: true).ToArray();
        var hasRules = typeof(IValidatableObject).IsAssignableFrom(type) ||
                       typeAttributes.Length > 0 ||
                       properties.Any(p => p.Attributes.Length > 0);

        return new TypeDetails([.. properties], typeAttributes, hasRules, [.. childTypes]);
    }

    /// <summary>
    /// The element type of a collection (<c>T</c> of <see cref="IEnumerable{T}"/>; a dictionary's is its
    /// <see cref="KeyValuePair{TKey, TValue}"/>), or <see langword="null"/> for other types.
    /// </summary>
    private static Type? GetElementType(Type type)
    {
        if (!typeof(IEnumerable).IsAssignableFrom(type))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        var enumerable = type
            .GetInterfaces()
            .Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0] ?? typeof(object);
    }

    /// <summary>
    /// Whether the type is a value without members to validate (numbers, text, dates, identifiers, enums).
    /// </summary>
    private static bool IsSimple(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying.IsPrimitive ||
               underlying.IsEnum ||
               underlying == typeof(string) ||
               underlying == typeof(decimal) ||
               underlying == typeof(DateTime) ||
               underlying == typeof(DateTimeOffset) ||
               underlying == typeof(DateOnly) ||
               underlying == typeof(TimeOnly) ||
               underlying == typeof(TimeSpan) ||
               underlying == typeof(Guid) ||
               underlying == typeof(Uri) ||
               underlying == typeof(Half) ||
               underlying == typeof(Int128) ||
               underlying == typeof(UInt128) ||
               underlying == typeof(Type);
    }

    /// <param name="Info">The property.</param>
    /// <param name="Attributes">Its validation attributes (property and constructor parameter).</param>
    /// <param name="DisplayName">The name used in messages: <c>[Display(Name)]</c>, else the property name.</param>
    /// <param name="SkipRecursion">Whether the value is not walked into (MiniValidation's <c>[SkipRecursion]</c>).</param>
    private sealed record PropertyDetails(
        PropertyInfo Info,
        ValidationAttribute[] Attributes,
        string DisplayName,
        bool SkipRecursion);

    /// <param name="Properties">The properties to check or walk into.</param>
    /// <param name="TypeAttributes">The type-level validation attributes.</param>
    /// <param name="HasRules">Whether the type itself has validation rules.</param>
    /// <param name="ChildTypes">The declared types of the properties that can be walked into.</param>
    private sealed record TypeDetails(
        PropertyDetails[] Properties,
        ValidationAttribute[] TypeAttributes,
        bool HasRules,
        Type[] ChildTypes);

    /// <summary>
    /// The state of one validation: the maximum depth, the errors so far and their count, and the objects already
    /// validated (by reference).
    /// </summary>
    private sealed class ValidationRun(int maxDepth)
    {
        public int MaxDepth { get; } = maxDepth;

        public bool TooDeep { get; set; }

        public Dictionary<string, List<string>> Errors { get; } = new(StringComparer.Ordinal);

        public HashSet<object> Visited { get; } = new(ReferenceEqualityComparer.Instance);

        public int ErrorCount { get; set; }
    }
}