namespace Demo.Api.Contracts.EndpointDefinitions;

/// <summary>
/// Test endpoints for demonstrating custom exception mapping.
/// </summary>
public sealed class TestEndpointDefinition : IEndpointDefinition
{
    internal const string ApiRouteBase = "/api/test";

    public void DefineEndpoints(WebApplication app)
    {
        var test = app.NewVersionedApi(ApiExplorerGroupNames.Test);

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

        // Error-handling simulations: call these and watch the logs, traces and metrics (e.g. in the Aspire dashboard).
        var errors = testV1.MapGroup("/errors");

        errors
            .MapGet("/server", SimulateServerError)
            .WithName("SimulateServerError")
            .WithDescription("Throws an InvalidDataException. Expected: 500, one Error log (event 10000) with the exception.")
            .WithSummary("Simulates a server error (500)");

        errors
            .MapGet("/client", SimulateClientError)
            .WithName("SimulateClientError")
            .WithDescription("Throws an ArgumentException. Expected: 400, one Debug log (event 10001) without the exception.")
            .WithSummary("Simulates a client error (400)");

        errors
            .MapGet("/stream-then-throw", SimulateStreamThenThrow)
            .WithName("SimulateStreamThenThrow")
            .WithDescription("Streams part of the body, then throws. Expected: the response is aborted, one Warning log (event 10003) and exactly one Error log (from the server, with the exception).")
            .WithSummary("Simulates an exception after the response has started");

        errors
            .MapGet("/slow-stream", SimulateSlowStream)
            .WithName("SimulateSlowStream")
            .WithDescription("Streams one chunk per second for 30 seconds. Cancel the request (e.g. close the browser tab) to simulate a client disconnect. Expected: one Debug log (event 10002), no Error.")
            .WithSummary("Simulates a client disconnect during a slow stream");
    }

    internal static Ok ThrowTeapotException()
        => throw new TeapotException("I'm a teapot! This demonstrates custom exception mapping.");

    internal static Ok SimulateServerError()
        => throw new InvalidDataException("Simulated server error.");

    internal static Ok SimulateClientError(string? value = null)
        => throw new ArgumentException($"Simulated client error: the value '{value}' is not valid.", nameof(value));

    internal static async Task SimulateStreamThenThrow(HttpContext context)
    {
        for (var i = 1; i <= 3; i++)
        {
            await context.Response.WriteAsync($"chunk {i}\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }

        throw new InvalidDataException("Simulated failure after the response has started.");
    }

    internal static async Task SimulateSlowStream(HttpContext context)
    {
        for (var i = 1; i <= 30; i++)
        {
            await context.Response.WriteAsync($"chunk {i}\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(TimeSpan.FromSeconds(1), context.RequestAborted);
        }
    }
}