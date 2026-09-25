namespace Demo.AppHost.Extensions;

internal static class ResourceBuilderExtensions
{
    public static IResourceBuilder<ProjectResource> WithApiExplorerUrl(
        this IResourceBuilder<ProjectResource> project)
        => project.WithUrls(context =>
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
        });

    public static IResourceBuilder<ProjectResource> WithHealthCheckUrls(
        this IResourceBuilder<ProjectResource> project)
        => project.WithUrls(context =>
        {
            var endpoint = context.GetEndpoint("https");

            context.Urls.Add(new ResourceUrlAnnotation
            {
                Url = $"{endpoint!.Url}/health",
                DisplayText = "Health",
                DisplayLocation = UrlDisplayLocation.DetailsOnly,
                Endpoint = endpoint,
            });

            context.Urls.Add(new ResourceUrlAnnotation
            {
                Url = $"{endpoint.Url}/alive",
                DisplayText = "Alive",
                DisplayLocation = UrlDisplayLocation.DetailsOnly,
                Endpoint = endpoint,
            });

            context.Urls.Add(new ResourceUrlAnnotation
            {
                Url = $"{endpoint.Url}/health-extended",
                DisplayText = "Health-Extended",
                DisplayLocation = UrlDisplayLocation.DetailsOnly,
                Endpoint = endpoint,
            });
        });
}