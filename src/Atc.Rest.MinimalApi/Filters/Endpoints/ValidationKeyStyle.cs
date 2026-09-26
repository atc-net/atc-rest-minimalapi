namespace Atc.Rest.MinimalApi.Filters.Endpoints;

/// <summary>
/// How <see cref="ValidationFilter{T}"/> names the keys in the ValidationProblem <c>errors</c> object.
/// </summary>
public enum ValidationKeyStyle
{
    /// <summary>
    /// The names the client sent (the default), so form libraries can map each error to its field:
    /// body values by their JSON path relative to the body, using the app's JSON naming policy
    /// (e.g. <c>reason</c>, <c>lines[2].sku</c>); route/query/header values by their binding name
    /// (e.g. <c>page-size</c>).
    /// </summary>
    WireNames = 0,

    /// <summary>
    /// The C# member paths of the validated type (the 2.x behavior, also what ASP.NET Core's built-in
    /// validation produces), e.g. <c>Request.Reason</c>, <c>PageSize</c>.
    /// <see cref="ValidationFilterOptions.SkipFirstLevelOnValidationKeys"/> only applies to this style.
    /// </summary>
    MemberPath = 1,
}