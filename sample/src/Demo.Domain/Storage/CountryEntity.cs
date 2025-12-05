namespace Demo.Domain.Storage;

public class CountryEntity
{
    public string? Name { get; set; }

    public string? Alpha2Code { get; set; }

    public string? Alpha3Code { get; set; }

    public override string ToString()
        => $"{nameof(Name)}: {Name}, {nameof(Alpha2Code)}: {Alpha2Code}, {nameof(Alpha3Code)}: {Alpha3Code}";
}