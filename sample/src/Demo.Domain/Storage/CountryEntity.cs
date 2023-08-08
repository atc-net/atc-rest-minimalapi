namespace Demo.Domain.Storage;

public class CountryEntity
{
    public string Name { get; set; } = string.Empty;

    public string Alpha2Code { get; set; } = string.Empty;

    public string Alpha3Code { get; set; } = string.Empty;

    public override string ToString()
        => $"{nameof(Name)}: {Name}, {nameof(Alpha2Code)}: {Alpha2Code}, {nameof(Alpha3Code)}: {Alpha3Code}";
}