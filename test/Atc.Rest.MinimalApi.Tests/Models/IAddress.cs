namespace Atc.Rest.MinimalApi.Tests.Models;

public interface IAddress
{
    string CountryCodeA3 { get; init; }

    string Place { get; init; }

    string Street { get; init; }

    string PostalCode { get; init; }

    string City { get; init; }
}