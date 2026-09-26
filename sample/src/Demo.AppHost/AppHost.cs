#pragma warning disable ASPIREPROBES001
#pragma warning disable MA0048
var builder = DistributedApplication.CreateBuilder(args);

var api = builder
    .AddProject<Projects.Demo_Api>("api")
    .WithHttpProbe(ProbeType.Liveness, HealthCheckEndpoints.Aliveness)
    .WithHttpProbe(ProbeType.Readiness, HealthCheckEndpoints.Health)
    .WithApiExplorerUrl()
    .WithHealthCheckUrls();

builder
    .AddProject<Projects.Demo_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api)
    .WithUrls(context =>
    {
        foreach (var url in context.Urls)
        {
            url.DisplayLocation = UrlDisplayLocation.DetailsOnly;
        }

        context.Urls.Add(new ResourceUrlAnnotation
        {
            Url = "/",
            DisplayText = "Web",
            Endpoint = context.GetEndpoint("https"),
        });
    });

// React (Vite) sample: maps the API's validation error keys straight onto its form fields.
// Vite gets PORT and the API address (API_HTTPS / API_HTTP) from Aspire, and proxies /api to Demo.Api.
builder
    .AddViteApp("react", "../Demo.React")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

await builder
    .Build()
    .RunAsync();