namespace Demo.Domain.Storage;

public class AddressEntity
{
    public string? StreetName { get; set; }

    public string? StreetNumber { get; set; }

    public string? PostalCode { get; set; }

    public string? CityName { get; set; }

    public CountryEntity? Country { get; set; }

    public override string ToString()
        => $"{nameof(StreetName)}: {StreetName}, {nameof(StreetNumber)}: {StreetNumber}, {nameof(PostalCode)}: {PostalCode}, {nameof(CityName)}: {CityName}, {nameof(Country)}: {Country}";
}