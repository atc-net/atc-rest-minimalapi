namespace Atc.Rest.MinimalApi.Tests.Middleware;

public sealed class GlobalErrorHandlingMiddlewareHostTests
{
    public enum Registration
    {
        UseGlobalErrorHandler,
        UseMiddlewareWithoutOptions,
        UseMiddlewareWithOptions,
    }

    [Theory]
    [InlineData(Registration.UseGlobalErrorHandler)]
    [InlineData(Registration.UseMiddlewareWithoutOptions)]
    [InlineData(Registration.UseMiddlewareWithOptions)]
    public async Task Host_WhenUnhandledException_LogsErrorThroughHostLoggerFactory(
        Registration registration)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddFakeLogging();

        await using var app = builder.Build();

        switch (registration)
        {
            case Registration.UseGlobalErrorHandler:
                app.UseGlobalErrorHandler();
                break;
            case Registration.UseMiddlewareWithoutOptions:
                app.UseMiddleware<GlobalErrorHandlingMiddleware>();
                break;
            case Registration.UseMiddlewareWithOptions:
                app.UseMiddleware<GlobalErrorHandlingMiddleware>(new GlobalErrorHandlingOptions());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(registration), registration, message: null);
        }

        app.MapGet("/throw", IResult () => throw new InvalidDataException("boom"));

        await app.StartAsync(cancellationToken);

        using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        using var response = await httpClient.GetAsync(new Uri("/throw", UriKind.Relative), cancellationToken);

        // Assert
        ((int)response.StatusCode).Should().Be(StatusCodes.Status500InternalServerError);

        var errorLogs = GetMiddlewareErrorLogs(app.Services);
        errorLogs.Should().ContainSingle();
        errorLogs[0].Exception.Should().BeOfType<InvalidDataException>();

        await app.StopAsync(cancellationToken);
    }

    private static List<FakeLogRecord> GetMiddlewareErrorLogs(
        IServiceProvider services)
        => services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Where(x => x.Level == LogLevel.Error &&
                        x.Category == typeof(GlobalErrorHandlingMiddleware).FullName)
            .ToList();
}