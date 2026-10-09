namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// Shaped like a parameters record from atc-rest-api-source-generator for a list body offered as XML: the body binds
/// into a <see cref="List{T}"/> subclass that declares <c>BindAsync</c>.
/// </summary>
public sealed record BindAsyncListBodyParameters(
    [property: Required] BindAsyncList<BindAsyncPet> Request);