namespace Atc.Rest.MinimalApi.Filters.Endpoints;

/// <summary>
/// Validates a request value that an endpoint builds itself, as <see cref="ValidationFilter{T}"/> validates its argument.
/// </summary>
/// <remarks>
/// <para>
/// For an endpoint that has no argument of the validated type, e.g. a form bound field by field
/// (<c>[FromForm(Name = "...")]</c> parameters) whose parameters record is built inside the endpoint.
/// It runs the same DataAnnotations and FluentValidation as the filter, with the <see cref="ValidationFilterOptions"/>
/// registered in DI and the app's JSON naming policy, and answers the same 400.
/// </para>
/// <para>
/// In a parameters record, the member without a binding-source attribute (e.g. <c>Request</c>) is the body, as a
/// <c>[FromBody]</c> member is for the filter: its errors are keyed as for a JSON body of the same model, and a registered
/// <c>IValidator</c> for its type runs. Members with <c>[FromRoute]</c>, <c>[FromQuery]</c> or <c>[FromHeader]</c> use
/// their binding names.
/// </para>
/// <code>
/// var parameters = new CreateShipmentParameters(shipmentId, request);
/// if (await RequestValidation.ValidateAsync(parameters, httpContext) is { } invalid)
/// {
///     return invalid;
/// }
/// </code>
/// </remarks>
public static class RequestValidation
{
    /// <summary>
    /// Validates a request value as <see cref="ValidationFilter{T}"/> validates its argument.
    /// </summary>
    /// <typeparam name="T">The type of the value to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="httpContext">The request: its services (validators, options, JSON options) and content type.</param>
    /// <param name="isParametersWrapper">
    /// <see langword="true"/> (default) if <paramref name="value"/> is a parameters record that holds the body;
    /// <see langword="false"/> if it is the body itself.
    /// </param>
    /// <returns>The 400 ValidationProblem, or <see langword="null"/> when the value is valid.</returns>
    public static ValueTask<ValidationProblem?> ValidateAsync<T>(
        T value,
        HttpContext httpContext,
        bool isParametersWrapper = true)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(httpContext);

        return ValidationFilter<T>.ValidateAsync(
            value,
            httpContext,
            httpContext.RequestServices.GetService<ValidationFilterOptions>(),
            isParametersWrapper ? static _ => true : static _ => false,
            inferBody: isParametersWrapper);
    }
}