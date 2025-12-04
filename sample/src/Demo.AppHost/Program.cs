var builder = DistributedApplication.CreateBuilder(args);

var api = builder
    .AddProject<Projects.Demo_Api>("api")
    .WithHttpHealthCheck("/health")
    .WithUrls(context =>
    {
        foreach (var url in context.Urls)
        {
            url.DisplayLocation = UrlDisplayLocation.DetailsOnly;
        }

        var endpoint = context.GetEndpoint("https");

        context.Urls.Add(new ResourceUrlAnnotation
        {
            Url = $"{endpoint!.Url}/scalar/v1",
            DisplayText = "Scalar",
            Endpoint = endpoint,
        });

        context.Urls.Add(new ResourceUrlAnnotation
        {
            Url = $"{endpoint.Url}/swagger",
            DisplayText = "Swagger",
            Endpoint = endpoint,
        });
    });

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