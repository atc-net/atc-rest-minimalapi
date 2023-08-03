namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record CreateLocationRequestWithJsonPropertyNames(
    [property: JsonPropertyName("address")]
    AddressWithJsonPropertyNames Address,
    [property: JsonPropertyName("telephone")]
    string Telephone);