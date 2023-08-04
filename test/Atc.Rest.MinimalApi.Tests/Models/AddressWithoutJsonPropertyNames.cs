namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record AddressWithoutJsonPropertyNames(
    [MinLength(3)]
    [MaxLength(3)]
    string CountryCodeA3,
    string Place,
    string Street,
    string PostalCode,
    string City) : IAddress;