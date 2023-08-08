namespace Demo.Domain.Storage;

public class AddressEntity
{
    public string StreetName { get; set; } = string.Empty;

    public string StreetNumber { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string CityName { get; set; } = string.Empty;

    public CountryEntity? Country { get; set; }

    public override string ToString()
        => $"{nameof(StreetName)}: {StreetName}, {nameof(StreetNumber)}: {StreetNumber}, {nameof(PostalCode)}: {PostalCode}, {nameof(CityName)}: {CityName}, {nameof(Country)}: {Country}";
}