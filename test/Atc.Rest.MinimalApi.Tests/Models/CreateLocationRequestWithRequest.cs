namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record CreateLocationRequestWithRequest(
    string LocationId,
    [property: FromBody] CreateLocationRequestWithJsonPropertyNames Request);