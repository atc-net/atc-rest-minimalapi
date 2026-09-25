// ReSharper disable InconsistentNaming
namespace Demo.Api.Extensions;

public static class WebApplicationExtensions
{
    private static readonly string[] PatchHttpMethods = ["patch"];

    public static IEndpointConventionBuilder MapRootEndpoint(
        this WebApplication app)
        => app
            .MapGet("/", () => TypedResults.Text("OK", "text/plain"))
            .ShortCircuit()
            .ExcludeFromDescription();

    public static RouteHandlerBuilder MapPatch(
        this WebApplication app,
        string pattern,
        Delegate handler)
        => app.MapMethods(
            pattern,
            PatchHttpMethods,
            handler);

    public static IApplicationBuilder UseCustomGlobalErrorHandler(
        this WebApplication app)
        => app.UseGlobalErrorHandler(options =>
        {
            // Demonstrate custom exception mapping: TeapotException -> HTTP 418 I'm a teapot
            // Note: 418 is not in HttpStatusCode enum, so we cast from StatusCodes
            options.MapException<TeapotException>((HttpStatusCode)StatusCodes.Status418ImATeapot);
        });

    public static IApplicationBuilder ConfigureScalar(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi().WithDocumentPerVersion();
        app.MapScalarApiReference("/scalar", options =>
        {
            options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });

        return app;
    }
}