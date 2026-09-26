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
    /// The <c>[FromBody]</c> members of <typeparamref name="T"/> (on the property or the constructor parameter of a positional record),
    /// found once per type instead of by reflection on every request.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2743:Static fields should not be used in generic types", Justification = "One value per T is intended: the members of T.")]
    private static readonly FromBodyMember[] FromBodyMembers =
    [
        .. WireNameValidationKeyMapper
            .GetFromBodyProperties(typeof(T))
            .Select(FromBodyMember.Create),
    ];

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

        var errors = ValidateUsingDataAnnotations(argToValidate)
                        .MergeErrors(
                            await ValidateUsingFluentValidation(context, argToValidate, resolveJsonPropertyNames: KeyStyle == ValidationKeyStyle.MemberPath));

        if (errors.Count > 0)
        {
            return KeyStyle == ValidationKeyStyle.MemberPath
                ? TypedResults
                    .ValidationProblem(errors)
                    .ResolveSerializationTypeNames<T>(validationFilterOptions?.SkipFirstLevelOnValidationKeys ?? false)
                : TypedResults.ValidationProblem(
                    WireNameValidationKeyMapper.Map(
                        typeof(T),
                        IsParametersWrapper(context),
                        errors,
                        GetJsonNamingPolicy(context)));
        }

        // Otherwise invoke the next filter in the pipeline
        return await next.Invoke(context);
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
    /// <param name="context">The context of the endpoint filter invocation.</param>
    /// <returns>
    /// From the handler metadata: whether the <typeparamref name="T"/> parameter has <c>[AsParameters]</c>.
    /// Without handler metadata: whether <typeparamref name="T"/> has members with binding attributes.
    /// </returns>
    private static bool IsParametersWrapper(
        EndpointFilterInvocationContext context)
    {
        var parameter = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<MethodInfo>()?
            .GetParameters()
            .FirstOrDefault(p => typeof(T).IsAssignableFrom(p.ParameterType));

        return parameter is not null
            ? parameter.GetCustomAttribute<AsParametersAttribute>() is not null
            : WireNameValidationKeyMapper.HasBindingSourceMembers(typeof(T));
    }

    /// <summary>
    /// Gets the JSON naming policy the request body was deserialized with (the app's minimal API JSON options).
    /// </summary>
    /// <param name="context">The context of the endpoint filter invocation.</param>
    /// <returns>The naming policy; ASP.NET Core's default (camelCase) when the options are not registered.</returns>
    private static JsonNamingPolicy? GetJsonNamingPolicy(
        EndpointFilterInvocationContext context)
    {
        var jsonOptions = context.HttpContext.RequestServices
            .GetService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();

        return jsonOptions is null
            ? JsonNamingPolicy.CamelCase
            : jsonOptions.Value.SerializerOptions.PropertyNamingPolicy;
    }

    private static Dictionary<string, string[]> ValidateUsingDataAnnotations(
        object objectToValidate)
        => MiniValidator.TryValidate(objectToValidate, out var errors)
            ? new Dictionary<string, string[]>(StringComparer.Ordinal)
            : new Dictionary<string, string[]>(errors, StringComparer.Ordinal);

    private static async Task<Dictionary<string, string[]>> ValidateUsingFluentValidation(
        EndpointFilterInvocationContext context,
        object objectToValidate,
        bool resolveJsonPropertyNames)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        // First, try to validate the main object (existing behavior)
        var validator = context.HttpContext.RequestServices.GetService<FluentValidation.IValidator<T>>();
        if (validator is not null)
        {
            var validationResult = await validator.ValidateAsync(
                (T)objectToValidate,
                context.HttpContext.RequestAborted);

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.ToDictionary())
                {
                    result.Add(error.Key, error.Value);
                }
            }
        }

        // Then, look for [FromBody] properties and validate them with their own validators
        await ValidateNestedFromBodyProperties(context, objectToValidate, result, resolveJsonPropertyNames);

        return result;
    }

    private static async Task ValidateNestedFromBodyProperties(
        EndpointFilterInvocationContext context,
        object objectToValidate,
        Dictionary<string, string[]> errors,
        bool resolveJsonPropertyNames)
    {
        foreach (var fromBodyMember in FromBodyMembers)
        {
            var property = fromBodyMember.Property;
            var propertyValue = property.GetValue(objectToValidate);
            if (propertyValue is null)
            {
                continue;
            }

            var propertyType = property.PropertyType;

            // Cast to non-generic IValidator interface
            if (context.HttpContext.RequestServices.GetService(fromBodyMember.ValidatorType) is not FluentValidation.IValidator validator)
            {
                continue;
            }

            // Use the non-generic IValidator.ValidateAsync directly
            var validationResult = await validator.ValidateAsync(
                fromBodyMember.CreateValidationContext(propertyValue),
                context.HttpContext.RequestAborted);

            if (validationResult.IsValid)
            {
                continue;
            }

            // Resolve JSON property names and prefix with [FromBody] property name
            // This ensures proper first-level stripping when SkipFirstLevelOnValidationKeys=true
            foreach (var error in validationResult.ToDictionary())
            {
                // MemberPath style resolves [JsonPropertyName] here; WireNames maps the raw C# path later, in one place.
                var resolvedKey = resolveJsonPropertyNames
                    ? ResolveJsonPropertyName(propertyType, error.Key)
                    : error.Key;

                // Prefix with the [FromBody] property name for proper first-level stripping
                // e.g., "WorkAddress.cityName" becomes "Request.WorkAddress.cityName"
                var prefixedKey = $"{property.Name}.{resolvedKey}";

                // Avoid duplicate keys - first error wins
                if (!errors.ContainsKey(prefixedKey))
                {
                    errors.Add(prefixedKey, error.Value);
                }
            }
        }
    }

    /// <summary>
    /// A <c>[FromBody]</c> member with what is needed to validate it: the validator type to resolve from DI,
    /// and a factory for FluentValidation's <c>ValidationContext&lt;TProperty&gt;</c>.
    /// </summary>
    /// <param name="Property">The <c>[FromBody]</c> property.</param>
    /// <param name="ValidatorType">The closed <c>IValidator&lt;TProperty&gt;</c> type.</param>
    /// <param name="CreateValidationContext">Creates the validation context for a property value.</param>
    private sealed record FromBodyMember(
        PropertyInfo Property,
        Type ValidatorType,
        Func<object, FluentValidation.IValidationContext> CreateValidationContext)
    {
        public static FromBodyMember Create(PropertyInfo property)
        {
            var createValidationContext = (Func<object, FluentValidation.IValidationContext>)typeof(FromBodyMember)
                .GetMethod(nameof(CreateValidationContextFactory), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(property.PropertyType)
                .Invoke(obj: null, parameters: null)!;

            return new FromBodyMember(
                property,
                typeof(FluentValidation.IValidator<>).MakeGenericType(property.PropertyType),
                createValidationContext);
        }

        public static Func<object, FluentValidation.IValidationContext> CreateValidationContextFactory<TProperty>()
            => value => new FluentValidation.ValidationContext<TProperty>((TProperty)value);
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