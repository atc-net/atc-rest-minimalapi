namespace Demo.Domain.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureDomainServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        MapsterConfig.Register();

        services.DefineHandlersAndServices();

        services.SetupStorage();

        return services;
    }

    public static void DefineHandlersAndServices(this IServiceCollection services)
    {
        services.AddSingleton<IGetUsersHandler, GetUsersHandler>();
        services.AddSingleton<ICreateUserHandler, CreateUserHandler>();
        services.AddSingleton<IGetUserByIdHandler, GetUserByIdHandler>();
        services.AddSingleton<IGetUserByEmailHandler, GetUserByEmailHandler>();
        services.AddSingleton<IUpdateUserByIdHandler, UpdateUserByIdHandler>();
        services.AddSingleton<IDeleteUserByIdHandler, DeleteUserByIdHandler>();
    }

    private static void SetupStorage(this IServiceCollection services)
    {
        // Add DbContext with In-Memory Database
        services.AddDbContext<DemoDbContext>(
            options => options.UseInMemoryDatabase(
                Guid.NewGuid().ToString()),
            ServiceLifetime.Singleton);
    }
}