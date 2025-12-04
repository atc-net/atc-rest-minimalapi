// ReSharper disable InconsistentNaming
namespace Demo.Api.Extensions;

public static class WebApplicationExtensions
{
    private static readonly string[] PatchHttpMethods = { "patch" };

    public static RouteHandlerBuilder MapPatch(
        this WebApplication app,
        string pattern,
        Delegate handler)
        => app.MapMethods(
            pattern,
            PatchHttpMethods,
            handler);

    public static IApplicationBuilder AddGlobalErrorHandler(this WebApplication app)
        => app.UseMiddleware<GlobalErrorHandlingMiddleware>();

    public static IApplicationBuilder ConfigureSwaggerUI(
        this WebApplication app,
        string applicationName)
    {
        app.UseSwagger(options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        });

        app.UseSwaggerUI(options =>
        {
            options.EnableTryItOutByDefault();
            options.InjectStylesheet("/swagger-ui/SwaggerDark.css");
            options.InjectJavascript("/swagger-ui/main.js");

            var descriptions = app.DescribeApiVersions();

            foreach (var description in descriptions)
            {
                var url = $"/swagger/{description.GroupName}/swagger.json";
                var name = description.GroupName.ToUpperInvariant();
                options.SwaggerEndpoint(url, $"{applicationName} {name}");
            }
        });

        return app;
    }

    public static IApplicationBuilder ConfigureScalarUI(this WebApplication app)
    {
        // Root endpoint redirects to Scalar API reference
        app
            .MapGet("/", () => Results.Redirect("/scalar/v1"))
            .ExcludeFromDescription();

        // Scalar uses native .NET OpenAPI
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });

        return app;
    }
}