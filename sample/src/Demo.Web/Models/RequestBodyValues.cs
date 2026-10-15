namespace Demo.Web.Models;

/// <summary>
/// Turns the form's blank values into null, so a field the user left empty is not sent: the API then validates
/// exactly what the user filled in (as the React sample does).
/// </summary>
internal static class RequestBodyValues
{
    public static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    public static Address? NullIfEmpty(Address? address)
    {
        if (address is null)
        {
            return null;
        }

        var result = new Address(
            NullIfBlank(address.StreetName),
            NullIfBlank(address.StreetNumber),
            NullIfBlank(address.PostalCode),
            NullIfBlank(address.CityName),
            NullIfEmpty(address.Country));

        return result == new Address(null, null, null, null, null) ? null : result;
    }

    public static Country? NullIfEmpty(Country? country)
    {
        if (country is null)
        {
            return null;
        }

        var result = new Country(
            NullIfBlank(country.Name),
            NullIfBlank(country.Alpha2Code),
            NullIfBlank(country.Alpha3Code));

        return result == new Country(null, null, null) ? null : result;
    }
}