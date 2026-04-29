namespace Demo.Api.Contracts.EndpointDefinitions;

/// <summary>
/// Test endpoints for demonstrating custom exception mapping.
/// </summary>
public sealed class TestEndpointDefinition : IEndpointDefinition
{
    internal const string ApiRouteBase = "/api/test";

    public void DefineEndpoints(WebApplication app)
    {
        var test = app.NewVersionedApi(SwaggerGroupNames.Test);

        DefineEndpointsV1(test);
    }

    private static void DefineEndpointsV1(IEndpointRouteBuilder app)
    {
        var testV1 = app
            .MapGroup(ApiRouteBase)
            .HasApiVersion(1.0);

        testV1
            .MapGet("/teapot", ThrowTeapotException)
            .WithName("ThrowTeapotException")
            .WithDescription("Throws a TeapotException to demonstrate custom exception mapping to HTTP 418.")
            .WithSummary("Throws HTTP 418 I'm a teapot");
    }

    internal static Ok ThrowTeapotException()
        => throw new TeapotException("I'm a teapot! This demonstrates custom exception mapping.");
}