namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

/// <summary>
/// Hosts for the validation behaviour suite: one Kestrel host per key configuration, each mapping the same endpoints.
/// The suite pins what a client receives, so a switch of the validation engine is proven by the same tests passing
/// unchanged.
/// </summary>
public sealed class ValidationBehaviourFixture : IAsyncLifetime
{
    private readonly List<WebApplication> apps = [];
    private readonly List<HttpClient> clients = [];

    /// <summary>Gets the client of the default host (<see cref="ValidationKeyStyle.WireNames"/>, camelCase).</summary>
    public HttpClient Default { get; private set; } = null!;

    /// <summary>Gets the client of a host with <see cref="ValidationKeyStyle.MemberPath"/>.</summary>
    public HttpClient MemberPath { get; private set; } = null!;

    /// <summary>Gets the client of a host with <see cref="ValidationKeyStyle.MemberPath"/> and <c>SkipFirstLevelOnValidationKeys</c>.</summary>
    public HttpClient MemberPathSkipFirstLevel { get; private set; } = null!;

    /// <summary>Gets the client of a host whose JSON naming policy is snake_case.</summary>
    public HttpClient SnakeCase { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Default = await StartAsync(configureServices: null);
        MemberPath = await StartAsync(services => services.AddSingleton(new ValidationFilterOptions { KeyStyle = ValidationKeyStyle.MemberPath }));
        MemberPathSkipFirstLevel = await StartAsync(services => services.AddSingleton(new ValidationFilterOptions
        {
            KeyStyle = ValidationKeyStyle.MemberPath,
            SkipFirstLevelOnValidationKeys = true,
        }));
        SnakeCase = await StartAsync(services => services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in clients)
        {
            client.Dispose();
        }

        foreach (var app in apps)
        {
            await app.DisposeAsync();
        }
    }

    private async Task<HttpClient> StartAsync(
        Action<IServiceCollection>? configureServices)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        ValidationBehaviourEndpoints.AddValidators(builder.Services);
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();

        // Messages format numbers and dates with the current culture (e.g. Range): pin it, so the suite does not
        // depend on the machine's culture.
        app.Use((context, next) =>
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
            return next(context);
        });

        ValidationBehaviourEndpoints.Map(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        apps.Add(app);

        var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        clients.Add(client);
        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ValidationBehaviourGroup : ICollectionFixture<ValidationBehaviourFixture>
{
    public const string Name = "Validation behaviour";
}