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

        var endpointReference = context.GetEndpoint("https");

        ////context.Urls.Add(new ResourceUrlAnnotation
        ////{
        ////    Url = string.Empty,
        ////    DisplayText = "Scalar",
        ////    Endpoint = endpointReference,
        ////});

        context.Urls.Add(new ResourceUrlAnnotation
        {
            Url = $"{endpointReference!.Url}/swagger",
            DisplayText = "Swagger",
            Endpoint = endpointReference,
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