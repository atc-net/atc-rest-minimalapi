namespace Atc.Rest.MinimalApi.Tests.Middleware;

/// <summary>
/// GlobalErrorHandlingMiddleware writes through <see cref="IProblemDetailsService"/> when the host registers it
/// (<c>AddProblemDetails()</c>), and falls back to its own writer otherwise.
/// </summary>
public sealed class ProblemDetailsServiceHostTests
{
    [Fact]
    public async Task Host_WithAddProblemDetails_BodyIncludesCustomizeProblemDetailsAndOwnExtensions()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(useProblemDetailsAsResponseBody: true, cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/throw", UriKind.Relative));
        request.Headers.Add("x-correlation-id", "corr-123");

        // Act
        using var response = await httpClient.SendAsync(request, cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        // Assert - the framework applied CustomizeProblemDetails; the middleware's own fields are kept
        ((int)response.StatusCode).Should().Be(StatusCodes.Status500InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        body.RootElement.GetProperty("errorCode").GetString().Should().Be("X42");
        body.RootElement.GetProperty("correlationId").GetString().Should().Be("corr-123");
        body.RootElement.GetProperty("instance").GetString().Should().Be("/throw");
        body.RootElement.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.6.1");
        body.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_WithAddProblemDetails_WhenServiceRefusesToWrite_FallsBackToOwnWriter()
    {
        // Arrange - IProblemDetailsService does not write for Accept: text/html
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(useProblemDetailsAsResponseBody: true, cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/throw", UriKind.Relative));
        request.Headers.Add("Accept", "text/html");

        // Act
        using var response = await httpClient.SendAsync(request, cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        // Assert - still a ProblemDetails body, written by the middleware itself (no CustomizeProblemDetails)
        ((int)response.StatusCode).Should().Be(StatusCodes.Status500InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        body.RootElement.GetProperty("instance").GetString().Should().Be("/throw");
        body.RootElement.TryGetProperty("errorCode", out _).Should().BeFalse();

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_WithAddProblemDetails_DictionaryBodyDoesNotUseTheService()
    {
        // Arrange - the plain dictionary body is not ProblemDetails
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(useProblemDetailsAsResponseBody: false, cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        using var response = await httpClient.GetAsync(new Uri("/throw", UriKind.Relative), cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        body.RootElement.TryGetProperty("errorCode", out _).Should().BeFalse();

        await app.StopAsync(cancellationToken);
    }

    private static async Task<WebApplication> StartAppAsync(
        bool useProblemDetailsAsResponseBody,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions["errorCode"] = "X42");

        var app = builder.Build();

        app.UseGlobalErrorHandler(options => options.UseProblemDetailsAsResponseBody = useProblemDetailsAsResponseBody);
        app.MapGet("/throw", IResult () => throw new InvalidDataException("boom"));

        await app.StartAsync(cancellationToken);
        return app;
    }
}