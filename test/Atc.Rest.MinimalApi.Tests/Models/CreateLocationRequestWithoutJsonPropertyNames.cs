namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record CreateLocationRequestWithoutJsonPropertyNames(
    AddressWithoutJsonPropertyNames Address,
    string Telephone);