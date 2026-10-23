namespace Atc.Rest.MinimalApi.Tests.Middleware;

/// <summary>
/// Pins the behavior documented in the README section on empty error responses:
/// only <c>AddProblemDetails()</c> + <c>UseStatusCodePages()</c> together give framework errors a ProblemDetails body,
/// and the pair does not touch the bodies written by <see cref="GlobalErrorHandlingMiddleware"/>.
/// </summary>
public sealed class ProblemDetailsSetupHostTests
{
    [Theory]
    [InlineData(false, false, null)] // nothing registered -> empty body
    [InlineData(true, false, null)] // AddProblemDetails alone -> still empty
    [InlineData(false, true, "text/plain")] // UseStatusCodePages alone -> "Status Code: 404; Not Found"
    [InlineData(true, true, "application/problem+json")] // the pair -> ProblemDetails
    public async Task Host_UnknownRoute_BodyDependsOnProblemDetailsAndStatusCodePages(
        bool addProblemDetails,
        bool useStatusCodePages,
        string? expectedMediaType)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(addProblemDetails, useStatusCodePages, cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        using var response = await httpClient.GetAsync(new Uri("/does-not-exist", UriKind.Relative), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        ((int)response.StatusCode).Should().Be(StatusCodes.Status404NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be(expectedMediaType);
        if (expectedMediaType is null)
        {
            body.Should().BeEmpty();
        }

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_WithBoth_EmptyNotFoundResultGetsProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(addProblemDetails: true, useStatusCodePages: true, cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act - the handler returns TypedResults.NotFound() without a body
        using var response = await httpClient.GetAsync(new Uri("/not-found", UriKind.Relative), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        ((int)response.StatusCode).Should().Be(StatusCodes.Status404NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var problem = JsonDocument.Parse(body);
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status404NotFound);
        problem.RootElement.TryGetProperty("type", out _).Should().BeTrue();
        problem.RootElement.TryGetProperty("title", out _).Should().BeTrue();
        problem.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_WithBoth_GlobalErrorHandlerBodyIsNotReplaced()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(addProblemDetails: true, useStatusCodePages: true, cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act - the handler throws; GlobalErrorHandlingMiddleware writes the error body
        using var response = await httpClient.GetAsync(new Uri("/throw", UriKind.Relative), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Assert - status code pages only fill empty bodies, so the middleware's body (with its instance) is kept
        ((int)response.StatusCode).Should().Be(StatusCodes.Status500InternalServerError);
        body.Should().Contain("\"instance\":\"/throw\"");

        await app.StopAsync(cancellationToken);
    }

    private static async Task<WebApplication> StartAppAsync(
        bool addProblemDetails,
        bool useStatusCodePages,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        if (addProblemDetails)
        {
            builder.Services.AddProblemDetails();
        }

        var app = builder.Build();

        app.UseGlobalErrorHandler();

        if (useStatusCodePages)
        {
            app.UseStatusCodePages();
        }

        app.MapGet("/not-found", () => TypedResults.NotFound());
        app.MapGet("/throw", IResult () => throw new InvalidDataException("boom"));

        await app.StartAsync(cancellationToken);
        return app;
    }
}