namespace Atc.Rest.MinimalApi.Filters.Endpoints;

/// <summary>
/// Represents a validation filter that integrates with Minimal API endpoints.
/// This class provides the following functionality:
/// <list type="number">
/// <item><description>Utilizes both DataAnnotations validation (via MiniValidation) and FluentValidation to validate the incoming request against a specific model.</description></item>
/// <item><description>Merges validation errors from both methods, ensuring that no duplicates exist, and returns a validation problem if errors are detected.</description></item>
/// <item><description>Automatically resolves the proper serialization names for the validation keys/values using the provided object's properties (e.g., JsonPropertyName attributes).</description></item>
/// <item><description>Supports nested validation for <c>[FromBody]</c> properties - validators registered for nested types will be automatically discovered and executed.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// This filter uses MiniValidation for DataAnnotations validation across all .NET versions (8, 9, and 10) to ensure consistent behavior and enable error merging.
/// </para>
/// <para>
/// <strong>Key Features:</strong>
/// <list type="bullet">
/// <item><description>Unified error responses (merges DataAnnotations + FluentValidation errors)</description></item>
/// <item><description>Serialization name resolution (respects JsonPropertyName attributes)</description></item>
/// <item><description>Consistent validation behavior across .NET 8/9/10</description></item>
/// <item><description>No additional configuration required (unlike .NET 10's native validation which requires InterceptorsNamespaces)</description></item>
/// <item><description>Nested [FromBody] validator discovery - use <c>ValidationFilter&lt;CreateUserParameters&gt;</c> with <c>IValidator&lt;CreateUserRequest&gt;</c></description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Example - Nested Validation:</strong>
/// <code>
/// // Parameters wrapper with [FromBody] property
/// public record CreateUserParameters([property: FromBody] CreateUserRequest Request);
///
/// // Validator for the nested request type
/// public class CreateUserRequestValidator : AbstractValidator&lt;CreateUserRequest&gt; { ... }
///
/// // The filter will automatically find and execute CreateUserRequestValidator
/// .AddEndpointFilter&lt;ValidationFilter&lt;CreateUserParameters&gt;&gt;()
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="T">The type of object to validate.</typeparam>
public partial class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    /// <summary>
    /// The body members of <typeparamref name="T"/>: <c>[FromBody]</c> (on the property or the constructor parameter of a positional record),
    /// or a member without a binding-source attribute whose type binds itself with <c>BindAsync</c>.
    /// Found once per type instead of by reflection on every request.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2743:Static fields should not be used in generic types", Justification = "One value per T is intended: the members of T.")]
    private static readonly FromBodyMember[] FromBodyMembers =
    [
        .. WireNameValidationKeyMapper
            .GetBodyProperties(typeof(T))
            .Select(FromBodyMember.Create),
    ];

    /// <summary>
    /// The body members of <typeparamref name="T"/> when a member without a binding-source attribute is also the body
    /// (see <see cref="RequestValidation"/>); found on first use.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2743:Static fields should not be used in generic types", Justification = "One value per T is intended: the members of T.")]
    private static FromBodyMember[]? inferredBodyMembers;

    private readonly ValidationFilterOptions? validationFilterOptions;

    private ValidationKeyStyle KeyStyle
        => validationFilterOptions?.KeyStyle ?? ValidationKeyStyle.WireNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationFilter{T}"/> class with optional validation filter options.
    /// </summary>
    /// <param name="validationFilterOptions">An optional object containing options to configure the validation behavior.</param>
    /// <param name="loggerFactory">The logger factory used to log a misconfigured endpoint. When <see langword="null"/>, nothing is logged.</param>
    public ValidationFilter(
        ValidationFilterOptions? validationFilterOptions = null,
        ILoggerFactory? loggerFactory = null)
    {
        this.validationFilterOptions = validationFilterOptions;
        logger = loggerFactory is null
            ? NullLogger<ValidationFilter<T>>.Instance
            : loggerFactory.CreateLogger<ValidationFilter<T>>();

        // A 2.x option kept with the 3.x default key style compiles but does nothing; say so once per options instance.
        if (validationFilterOptions is { SkipFirstLevelOnValidationKeys: true } &&
            KeyStyle != ValidationKeyStyle.MemberPath &&
            validationFilterOptions.TryMarkSkipFirstLevelWarningLogged())
        {
            LogSkipFirstLevelHasNoEffect(KeyStyle);
        }
    }

    /// <summary>
    /// Asynchronously invokes the validation filter.
    /// </summary>
    /// <param name="context">The context of the endpoint filter invocation.</param>
    /// <param name="next">A delegate representing the next filter in the pipeline.</param>
    /// <returns>A value task representing the result of the invocation.</returns>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var argToValidate = context.Arguments
            .OfType<T>()
            .FirstOrDefault();

        if (argToValidate is null)
        {
            // The handler declares a T parameter, but the value is null (e.g. an optional body that was not sent):
            // there is nothing to validate, and the endpoint allows the value to be missing.
            if (HandlerDeclaresParameterOfType(context))
            {
                return await next.Invoke(context);
            }

            // No argument of type T: the filter is attached to the wrong endpoint (a server misconfiguration).
            // The type name goes to the log only; the response stays generic (no internal type names for clients).
            // The filter returns a result instead of throwing, so this log is the only server-side record of the 500.
            LogMisconfiguredEndpoint(
                typeof(T).Name,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.ToString());

            return TypedResults.Problem(
                detail: "The endpoint is misconfigured.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var validationProblem = await ValidateAsync(
            argToValidate,
            context.HttpContext,
            validationFilterOptions,
            IsParametersWrapper,
            inferBody: false);

        // Otherwise invoke the next filter in the pipeline
        return validationProblem ?? await next.Invoke(context);
    }

    /// <summary>
    /// Validates a value with DataAnnotations and FluentValidation and keys the errors by the configured key style.
    /// Shared by the filter and <see cref="RequestValidation"/>, so both answer alike.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="httpContext">The request: its services, JSON options and content type.</param>
    /// <param name="options">The validation options; <see langword="null"/> for the defaults.</param>
    /// <param name="isParametersWrapper">Whether <typeparamref name="T"/> is a parameters wrapper; only asked when there are errors.</param>
    /// <param name="inferBody">Whether a wrapper member without a binding-source attribute is also the body.</param>
    /// <returns>The 400 ValidationProblem, or <see langword="null"/> when the value is valid.</returns>
    internal static async ValueTask<ValidationProblem?> ValidateAsync(
        T value,
        HttpContext httpContext,
        ValidationFilterOptions? options,
        Func<HttpContext, bool> isParametersWrapper,
        bool inferBody)
    {
        var keyStyle = options?.KeyStyle ?? ValidationKeyStyle.WireNames;
        var bodyMembers = inferBody
            ? inferredBodyMembers ??= [.. WireNameValidationKeyMapper.GetBodyProperties(typeof(T), inferBody: true).Select(FromBodyMember.Create)]
            : FromBodyMembers;

        var errors = ValidateUsingDataAnnotations(value, options?.MaxValidationDepth ?? DataAnnotationsGraphValidator.DefaultMaxDepth)
                        .MergeErrors(
                            await ValidateUsingFluentValidation(httpContext, value, bodyMembers, resolveJsonPropertyNames: keyStyle == ValidationKeyStyle.MemberPath));

        if (errors.Count == 0)
        {
            return null;
        }

        return keyStyle == ValidationKeyStyle.MemberPath
            ? TypedResults
                .ValidationProblem(errors)
                .ResolveSerializationTypeNames<T>(options?.SkipFirstLevelOnValidationKeys ?? false)
            : TypedResults.ValidationProblem(
                WireNameValidationKeyMapper.Map(
                    typeof(T),
                    isParametersWrapper(httpContext),
                    errors,
                    GetJsonNamingPolicy(httpContext),
                    IsXmlRequest(httpContext),
                    inferBody));
    }

    /// <summary>
    /// Determines whether the endpoint's handler declares a parameter of type <typeparamref name="T"/>,
    /// using the handler's <see cref="MethodInfo"/> from the endpoint metadata.
    /// </summary>
    /// <param name="context">The context of the endpoint filter invocation.</param>
    /// <returns>
    /// <see langword="true"/> if the handler has a parameter of type <typeparamref name="T"/> (or a derived type);
    /// <see langword="false"/> if it has none, or if no handler metadata is available.
    /// </returns>
    private static bool HandlerDeclaresParameterOfType(
        EndpointFilterInvocationContext context)
    {
        var handler = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<MethodInfo>();
        return handler is not null &&
               handler
                   .GetParameters()
                   .Any(p => typeof(T).IsAssignableFrom(p.ParameterType));
    }

    /// <summary>
    /// Determines whether <typeparamref name="T"/> is a parameters wrapper (e.g. an <c>[AsParameters]</c> record)
    /// rather than the request body itself.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>
    /// From the handler metadata: whether the <typeparamref name="T"/> parameter has <c>[AsParameters]</c>.
    /// Without handler metadata: whether <typeparamref name="T"/> has members with binding attributes.
    /// </returns>
    private static bool IsParametersWrapper(HttpContext httpContext)
    {
        var parameter = httpContext.GetEndpoint()?.Metadata.GetMetadata<MethodInfo>()?
            .GetParameters()
            .FirstOrDefault(p => typeof(T).IsAssignableFrom(p.ParameterType));

        return parameter is not null
            ? parameter.GetCustomAttribute<AsParametersAttribute>() is not null
            : WireNameValidationKeyMapper.HasBindingSourceMembers(typeof(T));
    }

    /// <summary>
    /// Gets the JSON naming policy the request body was deserialized with (the app's minimal API JSON options).
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>The naming policy; ASP.NET Core's default (camelCase) when the options are not registered.</returns>
    private static JsonNamingPolicy? GetJsonNamingPolicy(
        HttpContext httpContext)
    {
        var jsonOptions = httpContext.RequestServices
            .GetService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();

        return jsonOptions is null
            ? JsonNamingPolicy.CamelCase
            : jsonOptions.Value.SerializerOptions.PropertyNamingPolicy;
    }

    /// <summary>
    /// Determines whether the request body is XML (<c>application/xml</c>, <c>text/xml</c> or a <c>+xml</c> media type),
    /// so a validation error is keyed by the XML names the client sent.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns><see langword="true"/> if the request's <c>Content-Type</c> is XML.</returns>
    private static bool IsXmlRequest(HttpContext httpContext)
        => Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var mediaType) &&
           (mediaType.MediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
            mediaType.MediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Suffix.Equals("xml", StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, string[]> ValidateUsingDataAnnotations(
        object objectToValidate,
        int maxDepth)
    {
        DataAnnotationsGraphValidator.TryValidate(objectToValidate, maxDepth, out var errors);
        return errors;
    }

    private static async Task<Dictionary<string, string[]>> ValidateUsingFluentValidation(
        HttpContext httpContext,
        object objectToValidate,
        FromBodyMember[] bodyMembers,
        bool resolveJsonPropertyNames)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        // First, try to validate the main object (existing behavior)
        var validator = httpContext.RequestServices.GetService<FluentValidation.IValidator<T>>();
        if (validator is not null)
        {
            var validationResult = await validator.ValidateAsync(
                (T)objectToValidate,
                httpContext.RequestAborted);

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.ToDictionary())
                {
                    result.Add(error.Key, error.Value);
                }
            }
        }

        // Then, look for [FromBody] properties and validate them with their own validators
        await ValidateNestedFromBodyProperties(httpContext, objectToValidate, bodyMembers, result, resolveJsonPropertyNames);

        return result;
    }

    private static async Task ValidateNestedFromBodyProperties(
        HttpContext httpContext,
        object objectToValidate,
        FromBodyMember[] bodyMembers,
        Dictionary<string, string[]> errors,
        bool resolveJsonPropertyNames)
    {
        foreach (var fromBodyMember in bodyMembers)
        {
            var property = fromBodyMember.Property;
            var propertyValue = property.GetValue(objectToValidate);
            if (propertyValue is null)
            {
                continue;
            }

            // A validator for the body: for its own type, else for a base type (a list body is often a List<T> subclass
            // that binds itself, while the domain writes its validator for List<T>)
            var bodyValidator = fromBodyMember.Validators
                .Select(v => (Validator: httpContext.RequestServices.GetService(v.ValidatorType) as FluentValidation.IValidator, v.CreateValidationContext))
                .FirstOrDefault(v => v.Validator is not null);
            if (bodyValidator.Validator is not null)
            {
                var validationResult = await bodyValidator.Validator.ValidateAsync(
                    bodyValidator.CreateValidationContext(propertyValue),
                    httpContext.RequestAborted);

                foreach (var error in validationResult.ToDictionary())
                {
                    // A collection validator's rule on the items (RuleForEach(x => x)) is named after the lambda's
                    // parameter (x[0].Name): the items have no member name, the index is the key's first segment
                    var key = fromBodyMember.ItemValidator is not null
                        ? StripRootCollectionName(error.Key)
                        : error.Key;
                    AddError(errors, PrefixWithBodyMember(property.Name, ResolveKey(property.PropertyType, key, resolveJsonPropertyNames)), error.Value);
                }

                continue;
            }

            // A list body without a validator of its own: each item by the item type's validator - what a domain
            // registers for a model - keyed by the item's index, as the DataAnnotations of the item are
            if (fromBodyMember.ItemValidator is not { } itemValidatorInfo ||
                httpContext.RequestServices.GetService(itemValidatorInfo.ValidatorType) is not FluentValidation.IValidator itemValidator ||
                propertyValue is not IEnumerable items)
            {
                continue;
            }

            var index = 0;
            foreach (var item in items)
            {
                if (item is not null)
                {
                    var validationResult = await itemValidator.ValidateAsync(
                        itemValidatorInfo.CreateValidationContext(item),
                        httpContext.RequestAborted);

                    foreach (var error in validationResult.ToDictionary())
                    {
                        var key = ResolveKey(fromBodyMember.ItemType!, error.Key, resolveJsonPropertyNames);
                        AddError(errors, $"{property.Name}[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}].{key}", error.Value);
                    }
                }

                index++;
            }
        }
    }

    // MemberPath style resolves [JsonPropertyName] here; WireNames maps the raw C# path later, in one place.
    private static string ResolveKey(
        Type type,
        string key,
        bool resolveJsonPropertyNames)
        => resolveJsonPropertyNames && !key.StartsWith('[')
            ? ResolveJsonPropertyName(type, key)
            : key;

    // Prefixed with the [FromBody] property name for proper first-level stripping: "WorkAddress.cityName" becomes
    // "Request.WorkAddress.cityName", an item's "[0].Name" becomes "Request[0].Name"
    private static string PrefixWithBodyMember(
        string propertyName,
        string key)
        => key.Length == 0
            ? propertyName
            : key.StartsWith('[')
                ? propertyName + key
                : $"{propertyName}.{key}";

    // "x[0].Name" -> "[0].Name": a rule on the collection's items, named after the lambda's parameter
    private static string StripRootCollectionName(string key)
    {
        var bracket = key.IndexOf('[', StringComparison.Ordinal);
        var dot = key.IndexOf('.', StringComparison.Ordinal);
        return bracket > 0 && (dot < 0 || bracket < dot)
            ? key[bracket..]
            : key;
    }

    // Avoid duplicate keys - first error wins
    private static void AddError(
        Dictionary<string, string[]> errors,
        string key,
        string[] messages)
        => errors.TryAdd(key, messages);

    /// <summary>
    /// A <c>[FromBody]</c> member with what is needed to validate it: the validators to look for in DI - for its own
    /// type, then for its base types - and, for a collection, the validator of its items.
    /// </summary>
    /// <param name="Property">The <c>[FromBody]</c> property.</param>
    /// <param name="Validators">The closed <c>IValidator&lt;T&gt;</c> types to look for, the property's type first, each with a factory for its <c>ValidationContext&lt;T&gt;</c>.</param>
    /// <param name="ItemType">The item type of a collection body; <see langword="null"/> otherwise.</param>
    /// <param name="ItemValidator">The item type's validator to look for; <see langword="null"/> for a body that is no collection.</param>
    private sealed record FromBodyMember(
        PropertyInfo Property,
        ValidatorLookup[] Validators,
        Type? ItemType,
        ValidatorLookup? ItemValidator)
    {
        public static FromBodyMember Create(PropertyInfo property)
        {
            var itemType = ItemTypeOf(property.PropertyType);
            return new FromBodyMember(
                property,
                [.. SelfAndBaseTypes(property.PropertyType).Select(ValidatorLookup.For)],
                itemType,
                itemType is null ? null : ValidatorLookup.For(itemType));
        }

        // The type and its base classes up to (not including) object: List<T> for a List<T> subclass
        private static IEnumerable<Type> SelfAndBaseTypes(Type type)
        {
            for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
            {
                yield return current;
            }
        }

        // T of a collection body (IEnumerable<T>), not of a string or a dictionary
        private static Type? ItemTypeOf(Type type)
        {
            if (type == typeof(string) ||
                type.GetInterfaces().Append(type).Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
            {
                return null;
            }

            return type.GetInterfaces()
                .Append(type)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?
                .GetGenericArguments()[0];
        }
    }

    /// <summary>
    /// A validator type to resolve from DI, with a factory for FluentValidation's <c>ValidationContext&lt;T&gt;</c>.
    /// </summary>
    /// <param name="ValidatorType">The closed <c>IValidator&lt;T&gt;</c> type.</param>
    /// <param name="CreateValidationContext">Creates the validation context for a value of the validated type.</param>
    private sealed record ValidatorLookup(
        Type ValidatorType,
        Func<object, FluentValidation.IValidationContext> CreateValidationContext)
    {
        public static ValidatorLookup For(Type type)
        {
            var createValidationContext = (Func<object, FluentValidation.IValidationContext>)typeof(ValidatorLookup)
                .GetMethod(nameof(CreateValidationContextFactory), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(type)
                .Invoke(obj: null, parameters: null)!;

            return new ValidatorLookup(typeof(FluentValidation.IValidator<>).MakeGenericType(type), createValidationContext);
        }

        public static Func<object, FluentValidation.IValidationContext> CreateValidationContextFactory<TValue>()
            => value => new FluentValidation.ValidationContext<TValue>((TValue)value);
    }

    private static string ResolveJsonPropertyName(
        Type type,
        string propertyName)
    {
        // Handle nested properties like "WorkAddress.CityName"
        if (propertyName.Contains('.', StringComparison.Ordinal))
        {
            var parts = propertyName.Split('.');
            var resolvedParts = new List<string>();
            var currentType = type;

            foreach (var part in parts)
            {
                var propInfo = currentType.GetProperty(part);
                if (propInfo is null)
                {
                    resolvedParts.Add(part);
                    continue;
                }

                var jsonAttr = propInfo.GetCustomAttribute<JsonPropertyNameAttribute>();
                resolvedParts.Add(jsonAttr?.Name ?? part);
                currentType = propInfo.PropertyType;
            }

            return string.Join('.', resolvedParts);
        }

        // Simple property name
        var property = type.GetProperty(propertyName);
        if (property is null)
        {
            return propertyName;
        }

        var jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        return jsonPropertyName?.Name ?? propertyName;
    }
}