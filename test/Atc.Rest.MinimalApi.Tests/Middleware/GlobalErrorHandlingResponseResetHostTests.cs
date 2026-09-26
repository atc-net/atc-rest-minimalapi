namespace Atc.Rest.MinimalApi.Tests.Middleware;

/// <summary>
/// Real Kestrel host tests for what the error response carries over from the failed request:
/// headers the handler set before it threw, the status code of BadHttpRequestException, and the path base.
/// </summary>
public sealed class GlobalErrorHandlingResponseResetHostTests
{
    [Fact]
    public async Task Host_WhenHandlerSetHeadersBeforeThrowing_ErrorResponseDoesNotCarryThem()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = CreateApp();

        app.MapGet("/throw", IResult (HttpContext context) =>
        {
            context.Response.Headers.ETag = "\"v1\"";
            context.Response.Headers.CacheControl = "public, max-age=3600";
            context.Response.Headers.Location = "/somewhere";
            context.Response.Headers["x-custom"] = "from-handler";
            throw new InvalidDataException("boom");
        });

        await app.StartAsync(cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        using var response = await httpClient.GetAsync(new Uri("/throw", UriKind.Relative), cancellationToken);

        // Assert - the error must not look like the cacheable success response the handler started to build
        using (new AssertionScope())
        {
            ((int)response.StatusCode).Should().Be(StatusCodes.Status500InternalServerError);
            response.Headers.ETag.Should().BeNull();
            response.Headers.Location.Should().BeNull();
            response.Headers.Contains("x-custom").Should().BeFalse();
            response.Headers.CacheControl?.NoStore.Should().BeTrue();
            response.Headers.CacheControl?.NoCache.Should().BeTrue();
            response.Headers.CacheControl?.Public.Should().BeFalse();
        }

        await app.StopAsync(cancellationToken);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Host_WhenHstsSetsItsHeaderDirectly_TheErrorResponseDropsItLikeTheFramework(
        bool useFrameworkHandler,
        bool hstsBeforeErrorHandler)
    {
        // Arrange - HSTS sets its header directly (not in OnStarting) and runs inside the error handler
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddProblemDetails();
        builder.Services.AddHsts(o => o.ExcludedHosts.Clear());

        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Request.Scheme = "https"; // HSTS only applies to HTTPS requests
            return next(context);
        });

        if (hstsBeforeErrorHandler)
        {
            app.UseHsts();
        }

        if (useFrameworkHandler)
        {
            app.UseExceptionHandler();
        }
        else
        {
            app.UseGlobalErrorHandler();
        }

        if (!hstsBeforeErrorHandler)
        {
            app.UseHsts();
        }

        app.MapGet("/ok", () => TypedResults.Ok());
        app.MapGet("/throw", IResult () => throw new InvalidDataException("boom"));

        await app.StartAsync(cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        using var ok = await httpClient.GetAsync(new Uri("/ok", UriKind.Relative), cancellationToken);
        using var error = await httpClient.GetAsync(new Uri("/throw", UriKind.Relative), cancellationToken);

        TestContext.Current.TestOutputHelper!.WriteLine(
            $"{(useFrameworkHandler ? "framework" : "middleware")}, HSTS {(hstsBeforeErrorHandler ? "before" : "after")}: ok has HSTS = {ok.Headers.Contains("Strict-Transport-Security")}, error has HSTS = {error.Headers.Contains("Strict-Transport-Security")}");

        // Assert - the same as the framework handler: the reset (Response.Clear) drops headers that were set directly,
        // wherever the middleware is registered. Only headers added in Response.OnStarting (e.g. CORS) survive.
        ok.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
        error.Headers.Contains("Strict-Transport-Security").Should().BeFalse();

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_WhenHandlerSetContentLengthBeforeThrowing_WritesTheCompleteErrorBody()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = CreateApp();

        app.MapGet("/throw", IResult (HttpContext context) =>
        {
            context.Response.ContentLength = 2;
            throw new InvalidDataException("boom");
        });

        await app.StartAsync(cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        using var response = await httpClient.GetAsync(new Uri("/throw", UriKind.Relative), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        ((int)response.StatusCode).Should().Be(StatusCodes.Status500InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status500InternalServerError);

        await app.StopAsync(cancellationToken);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status408RequestTimeout)]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType)]
    public async Task Host_WhenBadHttpRequestException_UsesItsStatusCode(
        int statusCode)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = CreateApp();

        app.MapGet("/bad", IResult () => throw new BadHttpRequestException("Bad request.", statusCode));

        await app.StartAsync(cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        using var response = await httpClient.GetAsync(new Uri("/bad", UriKind.Relative), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        ((int)response.StatusCode).Should().Be(statusCode);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().Should().Be(statusCode);

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_WhenHandlerReadsABodyThatIsTooLarge_Returns413()
    {
        // Arrange - Kestrel throws BadHttpRequestException(413) from the body stream, e.g. for a large upload
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = CreateApp(configureServices: services => services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = 10));

        app.UseGlobalErrorHandler();
        app.MapPost("/upload", async (HttpContext context) =>
        {
            var person = await context.Request.ReadFromJsonAsync<PersonRequest>(context.RequestAborted);
            return TypedResults.Ok(person);
        });

        await app.StartAsync(cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var content = new StringContent("{\"name\":\"a name that is longer than ten bytes\"}", System.Text.Encoding.UTF8, "application/json");

        // Act
        using var response = await httpClient.PostAsync(new Uri("/upload", UriKind.Relative), content, cancellationToken);

        // Assert
        ((int)response.StatusCode).Should().Be(StatusCodes.Status413PayloadTooLarge);

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_WhenMinimalApiRejectsTheContentType_Returns415()
    {
        // Arrange - Development: minimal APIs throw BadHttpRequestException(415) instead of writing the status themselves
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = CreateApp(Microsoft.Extensions.Hosting.Environments.Development);

        app.MapPost("/items", (PersonRequest request) => TypedResults.Ok(request));

        await app.StartAsync(cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var content = new StringContent("hello", System.Text.Encoding.UTF8, "text/plain");

        // Act
        using var response = await httpClient.PostAsync(new Uri("/items", UriKind.Relative), content, cancellationToken);

        // Assert - the same status as outside Development
        ((int)response.StatusCode).Should().Be(StatusCodes.Status415UnsupportedMediaType);

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Host_WithPathBase_InstanceIncludesThePathBase()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = CreateApp(configureServices: services => services.AddProblemDetails());

        app.UsePathBase("/base");
        app.UseGlobalErrorHandler();
        app.UseRouting();
        app.MapGet("/throw", IResult () => throw new InvalidDataException("boom"));
        app.MapGet("/problem", () => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict));

        await app.StartAsync(cancellationToken);
        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        var middlewareInstance = await GetInstanceAsync(httpClient, "/base/throw", cancellationToken);
        var frameworkInstance = await GetInstanceAsync(httpClient, "/base/problem", cancellationToken);

        TestContext.Current.TestOutputHelper!.WriteLine($"framework instance: {frameworkInstance ?? "<none>"}");

        // Assert - RFC 9457: "instance" identifies this occurrence; it must include the path base
        middlewareInstance.Should().Be("/base/throw");
        frameworkInstance.Should().BeOneOf(null, "/base/problem", "GET /base/problem");

        await app.StopAsync(cancellationToken);
    }

    private static async Task<string?> GetInstanceAsync(
        HttpClient httpClient,
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("instance", out var instance)
            ? instance.GetString()
            : null;
    }

    private static WebApplication CreateApp(
        string? environment = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = environment ?? Microsoft.Extensions.Hosting.Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        if (configureServices is null)
        {
            app.UseGlobalErrorHandler();
        }

        return app;
    }
}