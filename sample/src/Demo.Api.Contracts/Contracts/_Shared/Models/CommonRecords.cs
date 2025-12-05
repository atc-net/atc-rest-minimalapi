// ReSharper disable CheckNamespace
namespace Demo.Api.Contracts.Contracts;

public sealed record Address(
    [property: StringLength(255)] string? StreetName,
    string? StreetNumber,
    string? PostalCode,
    [property: JsonPropertyName("cityName")] string? CityName,
    Country? Country);

public record Country(
    string? Name,
    [property: MinLength(2), MaxLength(2), RegularExpression("^[A-Za-z]{2}$")] string? Alpha2Code,
    [property: MinLength(3), MaxLength(3), RegularExpression("^[A-Za-z]{3}$")] string? Alpha3Code);