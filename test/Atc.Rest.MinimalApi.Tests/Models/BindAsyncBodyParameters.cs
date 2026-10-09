namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// Shaped like a parameters record from atc-rest-api-source-generator for a body offered as XML: the body member has
/// no binding attribute, because <c>[FromBody]</c> would make Minimal APIs read JSON only; its type binds itself.
/// </summary>
public sealed record BindAsyncBodyParameters(
    [property: Required] BindAsyncPet Request);