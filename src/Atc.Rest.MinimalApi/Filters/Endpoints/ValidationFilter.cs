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
public class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    private readonly ValidationFilterOptions? validationFilterOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationFilter{T}"/> class with optional validation filter options.
    /// </summary>
    /// <param name="validationFilterOptions">An optional object containing options to configure the validation behavior.</param>
    public ValidationFilter(
        ValidationFilterOptions? validationFilterOptions = null)
        => this.validationFilterOptions = validationFilterOptions;

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
        var argToValidate = context.Arguments.FirstOrDefault(x => x?.GetType() == typeof(T));
        if (argToValidate is null)
        {
            return TypedResults.BadRequest("The request is invalid - Could not find argument to validate from EndpointFilterInvocationContext.");
        }

        var errors = ValidateUsingDataAnnotations(argToValidate)
                        .MergeErrors(
                            await ValidateUsingFluentValidation(context, argToValidate));

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors)
                .ResolveSerializationTypeNames<T>(validationFilterOptions?.SkipFirstLevelOnValidationKeys ?? false);
        }

        // Otherwise invoke the next filter in the pipeline
        return await next.Invoke(context);
    }

    private static Dictionary<string, string[]> ValidateUsingDataAnnotations(
        object objectToValidate)
        => MiniValidator.TryValidate(objectToValidate, out var errors)
            ? new Dictionary<string, string[]>(StringComparer.Ordinal)
            : new Dictionary<string, string[]>(errors, StringComparer.Ordinal);

    private static async Task<Dictionary<string, string[]>> ValidateUsingFluentValidation(
        EndpointFilterInvocationContext context,
        object objectToValidate)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        // First, try to validate the main object (existing behavior)
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
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
        await ValidateNestedFromBodyProperties(context, objectToValidate, result);

        return result;
    }

    private static async Task ValidateNestedFromBodyProperties(
        EndpointFilterInvocationContext context,
        object objectToValidate,
        Dictionary<string, string[]> errors)
    {
        var properties = typeof(T)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<FromBodyAttribute>() is not null);

        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(objectToValidate);
            if (propertyValue is null)
            {
                continue;
            }

            var propertyType = property.PropertyType;
            var validatorType = typeof(IValidator<>).MakeGenericType(propertyType);

            // Cast to non-generic IValidator interface
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            // Create ValidationContext dynamically
            var contextType = typeof(ValidationContext<>).MakeGenericType(propertyType);
            if (Activator.CreateInstance(contextType, propertyValue) is not FluentValidation.IValidationContext validationContext)
            {
                continue;
            }

            // Use the non-generic IValidator.ValidateAsync directly
            var validationResult = await validator.ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted);

            if (validationResult.IsValid)
            {
                continue;
            }

            // Resolve JSON property names and prefix with [FromBody] property name
            // This ensures proper first-level stripping when SkipFirstLevelOnValidationKeys=true
            foreach (var error in validationResult.ToDictionary())
            {
                var resolvedKey = ResolveJsonPropertyName(propertyType, error.Key);

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