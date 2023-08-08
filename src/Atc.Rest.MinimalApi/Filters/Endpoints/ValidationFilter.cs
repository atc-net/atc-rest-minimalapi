namespace Atc.Rest.MinimalApi.Filters.Endpoints;

public class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    private readonly ValidationFilterOptions? validationFilterOptions;

    public ValidationFilter(
        ValidationFilterOptions? validationFilterOptions = null)
    {
        this.validationFilterOptions = validationFilterOptions;
    }

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