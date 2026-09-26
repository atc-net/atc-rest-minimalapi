namespace Atc.Rest.MinimalApi.Tests.Middleware;

/// <summary>
/// Metrics for a handled exception: the middleware reports the same as ASP.NET Core's own exception handler
/// (<c>UseExceptionHandler</c>), so dashboards and alerts show the exception type instead of only "500".
/// </summary>
public sealed class GlobalErrorHandlingTelemetryHostTests
{
    [Theory]
    [InlineData(StatusCodes.Status500InternalServerError)]
    [InlineData(StatusCodes.Status404NotFound)]
    public async Task Host_WhenUnhandledException_MetricsMatchTheFrameworkExceptionHandler(
        int statusCode)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var framework = await CaptureMetricsAsync(useFrameworkHandler: true, $"/telemetry-framework-{statusCode}", statusCode, cancellationToken);
        var middleware = await CaptureMetricsAsync(useFrameworkHandler: false, $"/telemetry-middleware-{statusCode}", statusCode, cancellationToken);

        TestContext.Current.TestOutputHelper!.WriteLine($"framework:  {framework}");
        TestContext.Current.TestOutputHelper!.WriteLine($"middleware: {middleware}");

        // Assert
        using var scope = new AssertionScope();
        middleware.RequestErrorType.Should().Be(framework.RequestErrorType);
        middleware.ExceptionsCounted.Should().Be(framework.ExceptionsCounted);
    }

    private static async Task<MetricsCapture> CaptureMetricsAsync(
        bool useFrameworkHandler,
        string route,
        int statusCode,
        CancellationToken cancellationToken)
    {
        string? requestErrorType = null;
        var exceptionsCounted = 0;

        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name is "http.server.request.duration" or "aspnetcore.diagnostics.exceptions")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            var tagList = tags.ToArray();
            if (instrument.Name == "http.server.request.duration" &&
                tagList.Any(t => t.Key == "http.route" && Equals(t.Value, route)))
            {
                requestErrorType = tagList.FirstOrDefault(t => t.Key == "error.type").Value?.ToString();
            }
        });

        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "aspnetcore.diagnostics.exceptions" &&
                tags.ToArray().Any(t => t.Key == "error.type" && Equals(t.Value, typeof(CustomFileNotFoundTestException).FullName)))
            {
                Interlocked.Increment(ref exceptionsCounted);
            }
        });

        listener.Start();

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddProblemDetails();

        await using var app = builder.Build();
        if (useFrameworkHandler)
        {
            app.UseExceptionHandler(new ExceptionHandlerOptions { StatusCodeSelector = _ => statusCode });
        }
        else
        {
            app.UseGlobalErrorHandler(o => o.MapException<CustomFileNotFoundTestException>((HttpStatusCode)statusCode));
        }

        app.MapGet(route, IResult () => throw new CustomFileNotFoundTestException("missing"));

        await app.StartAsync(cancellationToken);
        using (var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) })
        using (var response = await httpClient.GetAsync(new Uri(route, UriKind.Relative), cancellationToken))
        {
            ((int)response.StatusCode).Should().Be(statusCode);
        }

        await app.StopAsync(cancellationToken);
        listener.RecordObservableInstruments();

        return new MetricsCapture(requestErrorType, exceptionsCounted);
    }

    private sealed record MetricsCapture(
        string? RequestErrorType,
        int ExceptionsCounted);
}