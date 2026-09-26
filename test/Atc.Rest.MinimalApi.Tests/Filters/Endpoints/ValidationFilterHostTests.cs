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

    [Theory]
    [InlineData(null, new[] { "page-size", "reason", "lines[2].sku", "firstName" })] // default: WireNames
    [InlineData(ValidationKeyStyle.MemberPath, new[] { "PageSize", "Request.Reason", "Request.Lines[2].Sku", "Request.FirstName" })]
    public async Task Host_GeneratorShapedEndpoint_ValidationKeysFollowTheKeyStyle(
        ValidationKeyStyle? keyStyleFromDi,
        string[] expectedKeys)
    {
        // Arrange - an [AsParameters] record like the generator emits, route + query + JSON body
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(
            webApp => webApp.MapPut("/set/{mode}", ([AsParameters] SetParameters parameters) => TypedResults.Ok(parameters.Mode))
                .AddEndpointFilter<ValidationFilter<SetParameters>>(),
            cancellationToken,
            services =>
            {
                services.AddSingleton<IValidator<SetRequest>, SetRequestValidator>();
                if (keyStyleFromDi.HasValue)
                {
                    services.AddSingleton(new ValidationFilterOptions { KeyStyle = keyStyleFromDi.Value });
                }
            });

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var content = new StringContent(
            """{"reason":"far-too-long","firstName":"","lines":[{"sku":"ok"},{"sku":"ok"},{"sku":null}]}""",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        using var response = await httpClient.PutAsync(new Uri("/set/x?page-size=500", UriKind.Relative), content, cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        // Assert
        ((int)response.StatusCode).Should().Be(StatusCodes.Status400BadRequest);
        body.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(expectedKeys);

        await app.StopAsync(cancellationToken);
    }

    private static async Task<WebApplication> StartAppAsync(
        Action<WebApplication> mapEndpoints,
        CancellationToken cancellationToken,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddFakeLogging();
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        mapEndpoints(app);

        await app.StartAsync(cancellationToken);
        return app;
    }
}