namespace Demo.Domain;

public static class MapsterConfig
{
    public static void Register()
    {
        TypeAdapterConfig<CreateUserRequest, UserEntity>
            .NewConfig()
            .Map(dest => dest.HomePage, src => new Uri(src.HomePage));

        TypeAdapterConfig.GlobalSettings.Compile();
    }
}