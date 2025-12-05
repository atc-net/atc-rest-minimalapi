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
        services.AddScoped<IGetUsersHandler, GetUsersHandler>();
        services.AddScoped<ICreateUserHandler, CreateUserHandler>();
        services.AddScoped<IGetUserByIdHandler, GetUserByIdHandler>();
        services.AddScoped<IGetUserByEmailHandler, GetUserByEmailHandler>();
        services.AddScoped<IUpdateUserByIdHandler, UpdateUserByIdHandler>();
        services.AddScoped<IDeleteUserByIdHandler, DeleteUserByIdHandler>();
    }

    private static void SetupStorage(this IServiceCollection services)
    {
        // Add DbContext with In-Memory Database
        // Note: Using a fixed database name so data persists across requests
        // contextLifetime: Scoped for thread-safety, optionsLifetime: Singleton for test compatibility
        services.AddDbContext<DemoDbContext>(
            options => options.UseInMemoryDatabase("DemoDatabase"),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);
    }
}