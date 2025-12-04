var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(Atc.Serialization.JsonSerializerOptionsFactory.Create());

// Add MudBlazor services
builder.Services.AddMudServices();

// Add Razor components
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure HttpClient for API with service discovery
builder.Services.AddHttpClient<IUserService, UserService>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
})
.AddServiceDiscovery()
.AddStandardResilienceHandler();

// Add service discovery
builder.Services.AddServiceDiscovery();

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });

var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics.AddOtlpExporter())
        .WithTracing(tracing => tracing.AddOtlpExporter());

    builder.Logging.AddOpenTelemetry(logging => logging.AddOtlpExporter());
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();