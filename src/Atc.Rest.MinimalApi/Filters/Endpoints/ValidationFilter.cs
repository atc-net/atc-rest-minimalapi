namespace Atc.Rest.MinimalApi.Filters.Endpoints;

public class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var argToValidate = context.Arguments.SingleOrDefault(x => x?.GetType() == typeof(T));
        if (argToValidate is null)
        {
            return Results.BadRequest("The request is invalid.");
        }

        var errors = MergeErrors(
            ValidateUsingDataAnnotations(argToValidate),
            await ValidateUsingFluentValidation(context, argToValidate));

        if (errors.Any())
        {
            return TypedResults.ValidationProblem(errors)
                .ResolveSerializationTypeNames<T>();
        }

        // Otherwise invoke the next filter in the pipeline
        return await next.Invoke(context);
    }

    private static IDictionary<string, string[]> MergeErrors(
        Dictionary<string, string[]> errorsA,
        Dictionary<string, string[]> errorsB)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!errorsA.Any() &&
            !errorsB.Any())
        {
            return result;
        }

        return errorsA
            .Concat(errorsB)
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                .SelectMany(y => y.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, string[]> ValidateUsingDataAnnotations(
        object objectToValidate)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (MiniValidator.TryValidate(objectToValidate, out var errors))
        {
            return result;
        }

        foreach (var error in errors)
        {
            result.Add(error.Key, error.Value);
        }

        return result;
    }

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