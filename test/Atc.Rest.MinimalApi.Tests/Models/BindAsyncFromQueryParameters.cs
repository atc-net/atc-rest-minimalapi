namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// A parameters record whose member type has a <c>BindAsync</c>, but whose member is bound from the query explicitly.
/// </summary>
public sealed record BindAsyncFromQueryParameters(
    [property: FromQuery(Name = "filter")] BindAsyncPet Filter);