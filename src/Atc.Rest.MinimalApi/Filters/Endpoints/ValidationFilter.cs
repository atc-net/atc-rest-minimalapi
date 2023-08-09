namespace Atc.Rest.MinimalApi.Filters.Endpoints;

/// <summary>
/// Represents a validation filter that integrates with Minimal API endpoints.
/// This class provides the following functionality:
/// 1. Utilizes both DataAnnotations validation and FluentValidation to validate the incoming request against a specific model.
/// 2. Merges validation errors from both methods, ensuring that no duplicates exist, and returns a validation problem if errors are detected.
/// 3. Automatically resolves the proper serialization names for the validation keys/values using the provided object's properties (e.g., JsonPropertyName attributes).
/// </summary>
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
    {
        this.validationFilterOptions = validationFilterOptions;
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
        var argToValidate = context.Arguments.SingleOrDefault(x => x?.GetType() == typeof(T));
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

        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
        {
            return result;
        }

        var validationResult = await validator.ValidateAsync((T)objectToValidate, context.HttpContext.RequestAborted);
        if (validationResult.IsValid)
        {
            return result;
        }

        foreach (var error in validationResult.ToDictionary())
        {
            result.Add(error.Key, error.Value);
        }

        return result;
    }
}