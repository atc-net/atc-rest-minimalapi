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

await builder
    .Build()
    .RunAsync();