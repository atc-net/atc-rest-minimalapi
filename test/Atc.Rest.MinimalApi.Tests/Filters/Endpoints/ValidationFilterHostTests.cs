namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints;

public sealed class ValidationFilterHostTests
{
    [Theory]
    [InlineData("/optional-body", StatusCodes.Status400BadRequest)] // name missing -> validation ran on the record
    [InlineData("/optional-body?name=joe", StatusCodes.Status200OK)] // valid record, no body -> handler reached
    public async Task Host_AsParametersWithOptionalBodyNotSent_ValidatesParametersRecord(
        string path,
        int expectedStatusCode)
    {
        // Arrange - the generator's shape: ValidationFilter<TParameters> on an [AsParameters] record
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(
            webApp => webApp.MapPost("/optional-body", ([AsParameters] OptionalBodyParameters parameters) => TypedResults.Ok(parameters.Name))
                .AddEndpointFilter<ValidationFilter<OptionalBodyParameters>>(),
            cancellationToken);

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act - no body is sent
        using var response = await httpClient.PostAsync(new Uri(path, UriKind.Relative), content: null, cancellationToken);

        // Assert - never the misconfiguration branch (500), never skipped when the record is invalid
        ((int)response.StatusCode).Should().Be(expectedStatusCode);

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_MisconfiguredEndpoint_Returns500ProblemDetailsAndLogsErrorThroughHostLoggerFactory()
    {
        // Arrange - the handler has no PersonRequest parameter; its optional query value arrives as null
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(
            webApp => webApp.MapGet("/misconfigured", (string? q) => TypedResults.Ok(q))
                .AddEndpointFilter<ValidationFilter<PersonRequest>>(),
            cancellationToken);

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        using var response = await httpClient.GetAsync(new Uri("/misconfigured", UriKind.Relative), cancellationToken);

        // Assert - a null argument that is NOT of type T must not be skipped
        ((int)response.StatusCode).Should().Be(StatusCodes.Status500InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        body.Should().NotContain(nameof(PersonRequest), because: "internal type names are not returned to clients");

        // AddEndpointFilter<T>() injected the host's ILoggerFactory
        app.Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Where(x => x.Level == LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Id.Id.Should().Be(10_100);

        await app.StopAsync(cancellationToken);
    }

    private static async Task<WebApplication> StartAppAsync(
        Action<WebApplication> mapEndpoints,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddFakeLogging();

        var app = builder.Build();
        mapEndpoints(app);

        await app.StartAsync(cancellationToken);
        return app;
    }
}