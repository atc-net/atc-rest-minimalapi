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

    [Theory]
    [InlineData(null, "", "")] // default: WireNames - no body at all
    [InlineData(null, "null", "")] // default: WireNames - a JSON null body
    [InlineData(ValidationKeyStyle.MemberPath, "", "Request")]
    [InlineData(ValidationKeyStyle.MemberPath, "null", "Request")]
    public async Task Host_GeneratorShapedEndpoint_MissingBody_IsReportedUnderTheEmptyKey(
        ValidationKeyStyle? keyStyleFromDi,
        string requestBody,
        string expectedKey)
    {
        // Arrange - the generator's [AsParameters] record with a required [FromBody] member, and no body sent
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(
            webApp => webApp.MapPut("/set/{mode}", ([AsParameters] SetParameters parameters) => TypedResults.Ok(parameters.Mode))
                .AddEndpointFilter<ValidationFilter<SetParameters>>(),
            cancellationToken,
            services =>
            {
                if (keyStyleFromDi.HasValue)
                {
                    services.AddSingleton(new ValidationFilterOptions { KeyStyle = keyStyleFromDi.Value });
                }
            });

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

        // Act
        using var response = await httpClient.PutAsync(new Uri("/set/x?page-size=5", UriKind.Relative), content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var body = JsonDocument.Parse(json);

        // Assert
        ((int)response.StatusCode).Should().Be(StatusCodes.Status400BadRequest, json);
        var errors = body.RootElement.GetProperty("errors");
        errors.EnumerateObject().Select(p => p.Name).Should().Equal([expectedKey], json);
        var messages = errors.GetProperty(expectedKey).EnumerateArray().Select(m => m.GetString());
        if (keyStyleFromDi is null)
        {
            messages.Should().Equal(["A non-empty request body is required."], json);
        }
        else
        {
            messages.Should().Equal(["The Request field is required."], json);
        }

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_GeneratorShapedEndpoint_FluentValidationRootRule_IsReportedUnderTheEmptyKey()
    {
        // Arrange - a body validator with a rule on the body as a whole (no property name)
        var cancellationToken = TestContext.Current.CancellationToken;
        var validator = new InlineValidator<SetRequest>();
        validator.RuleFor(x => x).Must(r => r.Reason != "root").WithMessage("The request as a whole is rejected.");

        await using var app = await StartAppAsync(
            webApp => webApp.MapPut("/set/{mode}", ([AsParameters] SetParameters parameters) => TypedResults.Ok(parameters.Mode))
                .AddEndpointFilter<ValidationFilter<SetParameters>>(),
            cancellationToken,
            services => services.AddSingleton<IValidator<SetRequest>>(validator));

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var content = new StringContent("""{"reason":"root","firstName":"Ann","lines":[]}""", System.Text.Encoding.UTF8, "application/json");

        // Act
        using var response = await httpClient.PutAsync(new Uri("/set/x?page-size=5", UriKind.Relative), content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var body = JsonDocument.Parse(json);

        // Assert
        ((int)response.StatusCode).Should().Be(StatusCodes.Status400BadRequest, json);
        var errors = body.RootElement.GetProperty("errors");
        errors.EnumerateObject().Select(p => p.Name).Should().Equal([string.Empty], json);
        errors.GetProperty(string.Empty).EnumerateArray().Select(m => m.GetString()).Should().Equal(["The request as a whole is rejected."], json);

        await app.StopAsync(cancellationToken);
    }

    [Theory]
    [InlineData("application/json", """{"name":"a name that is much too long","lastName":"too-long"}""", new[] { "name", "lastName" })]
    [InlineData("application/xml", "<pet><name>a name that is much too long</name><surname>too-long</surname></pet>", new[] { "name", "surname" })]
    [InlineData("text/xml; charset=utf-8", "<pet><name>a name that is much too long</name><surname>too-long</surname></pet>", new[] { "name", "surname" })]
    public async Task Host_BindAsyncBody_KeysFollowTheNamesOfTheRequestsMediaType(
        string contentType,
        string requestBody,
        string[] expectedKeys)
    {
        // Arrange - the generator's shape for a body offered as XML: no [FromBody], the body type binds itself
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(
            webApp => webApp.MapPost("/pets", ([AsParameters] BindAsyncBodyParameters parameters) => TypedResults.Ok(parameters.Request.Name))
                .AddEndpointFilter<ValidationFilter<BindAsyncBodyParameters>>(),
            cancellationToken);

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var content = new StringContent(requestBody);
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);

        // Act
        using var response = await httpClient.PostAsync(new Uri("/pets", UriKind.Relative), content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var body = JsonDocument.Parse(json);

        // Assert - the body is the root of the request, and an XML request is answered with its XML names
        ((int)response.StatusCode).Should().Be(StatusCodes.Status400BadRequest, json);
        body.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(expectedKeys, json);

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_BindAsyncBody_MissingBody_IsReportedUnderTheEmptyKey()
    {
        // Arrange - the generator's non-nullable body member; BindAsync returns null when no body is sent
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(
            webApp => webApp.MapPost("/pets", ([AsParameters] BindAsyncBodyParameters parameters) => TypedResults.Ok(parameters.Request.Name))
                .AddEndpointFilter<ValidationFilter<BindAsyncBodyParameters>>(),
            cancellationToken);

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        using var response = await httpClient.PostAsync(new Uri("/pets", UriKind.Relative), content: null, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var body = JsonDocument.Parse(json);

        // Assert - as for a missing [FromBody] member (ISSUE-12)
        ((int)response.StatusCode).Should().Be(StatusCodes.Status400BadRequest, json);
        var errors = body.RootElement.GetProperty("errors");
        errors.EnumerateObject().Select(p => p.Name).Should().Equal([string.Empty], json);
        errors.GetProperty(string.Empty).EnumerateArray().Select(m => m.GetString()).Should().Equal(["A non-empty request body is required."], json);

        await app.StopAsync(cancellationToken);
    }

    [Theory]
    [InlineData("application/json", """{"name":"Rex"}""", "name")]
    [InlineData("application/xml", "<pet><name>Rex</name></pet>", "name")]
    public async Task Host_BindAsyncBody_NestedFluentValidationValidatorRuns(
        string contentType,
        string requestBody,
        string expectedKey)
    {
        // Arrange - a validator for the body type, not for the parameters record
        var cancellationToken = TestContext.Current.CancellationToken;
        var validator = new InlineValidator<BindAsyncPet>();
        validator.RuleFor(x => x.Name).Must(n => n != "Rex").WithMessage("Rex is taken.");

        await using var app = await StartAppAsync(
            webApp => webApp.MapPost("/pets", ([AsParameters] BindAsyncBodyParameters parameters) => TypedResults.Ok(parameters.Request.Name))
                .AddEndpointFilter<ValidationFilter<BindAsyncBodyParameters>>(),
            cancellationToken,
            services => services.AddSingleton<IValidator<BindAsyncPet>>(validator));

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var content = new StringContent(requestBody);
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);

        // Act
        using var response = await httpClient.PostAsync(new Uri("/pets", UriKind.Relative), content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var body = JsonDocument.Parse(json);

        // Assert
        ((int)response.StatusCode).Should().Be(StatusCodes.Status400BadRequest, json);
        var errors = body.RootElement.GetProperty("errors");
        errors.EnumerateObject().Select(p => p.Name).Should().Equal([expectedKey], json);
        errors.GetProperty(expectedKey).EnumerateArray().Select(m => m.GetString()).Should().Equal(["Rex is taken."], json);

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_BindAsyncBody_ListBody_KeepsTheRowIndex()
    {
        // Arrange - the generator's RequestBodyList<T>: a List<T> subclass that binds itself; row 1 is invalid
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = await StartAppAsync(
            webApp => webApp.MapPost("/pets/import", ([AsParameters] BindAsyncListBodyParameters parameters) => TypedResults.Ok(parameters.Request.Count))
                .AddEndpointFilter<ValidationFilter<BindAsyncListBodyParameters>>(),
            cancellationToken);

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var content = new StringContent(
            """[{"name":"Rex"},{"name":"a name that is much too long"}]""",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        using var response = await httpClient.PostAsync(new Uri("/pets/import", UriKind.Relative), content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var body = JsonDocument.Parse(json);

        // Assert - as for a [FromBody] List<T>: the member segment is dropped, the row index stays
        ((int)response.StatusCode).Should().Be(StatusCodes.Status400BadRequest, json);
        body.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().Equal(["[1].name"], json);

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