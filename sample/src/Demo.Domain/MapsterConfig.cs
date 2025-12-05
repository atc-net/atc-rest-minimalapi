namespace Demo.Domain;

public static class MapsterConfig
{
    public static void Register()
    {
        // Map Country -> CountryEntity
        TypeAdapterConfig<Country, CountryEntity>
            .NewConfig();

        // Map Address -> AddressEntity
        TypeAdapterConfig<Address, AddressEntity>
            .NewConfig();

        // Map CreateUserRequest -> UserEntity with safe Uri parsing
        TypeAdapterConfig<CreateUserRequest, UserEntity>
            .NewConfig()
            .Map(dest => dest.HomePage, src => TryCreateUri(src.HomePage));

        TypeAdapterConfig.GlobalSettings.Compile();
    }

    private static Uri? TryCreateUri(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
    }
}