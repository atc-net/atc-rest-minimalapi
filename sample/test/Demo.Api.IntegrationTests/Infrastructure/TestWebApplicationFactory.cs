namespace Demo.Api.IntegrationTests.Infrastructure;

public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    protected override IHost CreateHost(
        IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DemoDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<DemoDbContext>(
                options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()),
                ServiceLifetime.Singleton);
        });

        return base.CreateHost(builder);
    }
}