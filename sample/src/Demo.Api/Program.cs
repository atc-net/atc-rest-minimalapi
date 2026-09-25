// For OpenTelemetry, some Azure libraries like EventHubs still require feature flag to enable instrumentation.
// Setting this flag later than at the start of the Program.cs does not have any effect.
AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", isEnabled: true);

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Configure API services
services
    .ConfigureObservability(builder.Configuration, builder.Environment.IsDevelopment())
    .ConfigureSecurity(builder.Configuration)
    .ConfigureRequestHandling();

var apiVersioningBuilder = builder.Services.ConfigureApiVersioning();

// Configure Domain services
services
    .AddSingleton(_ => JsonSerializerOptionsFactory.Create())
    .ConfigureDomainServices(builder.Configuration)
    .ConfigureHealthChecks(builder.Configuration);

services.AddMemoryCache();

if (!builder.Environment.IsProduction())
{
    services.ConfigureScalar(apiVersioningBuilder);
}

var app = builder.Build();
app.UseCustomGlobalErrorHandler();
app.UseStatusCodePages();
app.UseHsts();
app.UseHttpsRedirection();
app.UseCors(PolicyConstants.Cors);

if (!builder.Environment.IsProduction())
{
    app.ConfigureScalar();
    app.UseHttpLogging();
}

app.UseEndpointDefinitions();

app.MapRootEndpoint();
app.MapHealthChecks();

app.InitializeDatabase();

await app.RunAsync();