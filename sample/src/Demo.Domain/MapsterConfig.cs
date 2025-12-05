namespace Demo.Domain;

public static class MapsterConfig
{
    public static void Register()
    {
        TypeAdapterConfig<CreateUserRequest, UserEntity>
            .NewConfig()
            .Map(dest => dest.HomePage, src => src.HomePage != null ? new Uri(src.HomePage) : null);

        TypeAdapterConfig.GlobalSettings.Compile();
    }
}