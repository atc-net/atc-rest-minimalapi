namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record AddressWithJsonPropertyNames(
    [MinLength(3)]
    [MaxLength(3)]
    [property: JsonPropertyName("country_code_a3")]
    string CountryCodeA3,
    string Place,
    string Street,
    string PostalCode,
    string City) : IAddress;