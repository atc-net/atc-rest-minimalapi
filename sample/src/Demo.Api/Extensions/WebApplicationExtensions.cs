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

    public static IApplicationBuilder AddGlobalErrorHandler(
        this WebApplication app)
        => app.UseMiddleware<GlobalErrorHandlingMiddleware>();

    public static IApplicationBuilder ConfigureSwaggerUI(
        this WebApplication app,
        string applicationName)
    {
        app.UseSwagger();
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
}