namespace Demo.Web.Models;

public record Address(
    string? StreetName,
    string? StreetNumber,
    string? PostalCode,
    [property: JsonPropertyName("cityName")] string? CityName,
    Country? Country);
