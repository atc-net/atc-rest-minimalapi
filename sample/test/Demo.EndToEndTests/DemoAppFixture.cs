namespace Demo.EndToEndTests;

/// <summary>
/// Starts the Demo AppHost (api, web, react) once and provides a headless Chromium browser.
/// Nothing is started until the first end-to-end test calls <see cref="NewPageAsync"/>, because the tests are
/// explicit (opt-in) and a normal <c>dotnet test</c> must not pay for Aspire, Node and a browser.
/// </summary>
/// <remarks>
/// Prerequisites: Node.js (for Demo.React) and the Playwright browser, installed once after building:
/// <c>pwsh sample/test/Demo.EndToEndTests/bin/Debug/net10.0/playwright.ps1 install chromium</c>.
/// </remarks>
public sealed class DemoAppFixture : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    private readonly SemaphoreSlim startLock = new(1, 1);
    private DistributedApplication? app;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private HttpClient? apiClient;

    public Uri ReactUrl { get; private set; } = null!;

    public Uri WebUrl { get; private set; } = null!;

    /// <summary>
    /// The shared HttpClient for calling Demo.Api directly. The fixture owns and disposes it.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A client whose base address is the API.</returns>
    public async Task<HttpClient> GetApiClientAsync(
        CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);

        return apiClient!;
    }

    public async Task<IPage> NewPageAsync(CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);

        var context = await browser!.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        return await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
        apiClient?.Dispose();

        if (app is not null)
        {
            await app.DisposeAsync();
        }

        startLock.Dispose();
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        await startLock.WaitAsync(cancellationToken);
        try
        {
            if (app is not null)
            {
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(StartupTimeout);

            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Demo_AppHost>(timeout.Token);
            app = await builder.BuildAsync(timeout.Token);
            await app.StartAsync(timeout.Token);

            foreach (var resource in new[] { "api", "web", "react" })
            {
                await app.ResourceNotifications.WaitForResourceHealthyAsync(resource, timeout.Token);
            }

            ReactUrl = app.GetEndpoint("react", "http");
            WebUrl = app.GetEndpoint("web", "https");

            // Aspire's default HttpClient accepts the local ASP.NET Core development certificate.
            apiClient = app.CreateHttpClient("api", "https");

            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }
        finally
        {
            startLock.Release();
        }
    }
}