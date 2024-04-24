namespace Atc.Rest.MinimalApi.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="WebApplication"/> class to enhance its functionality.
/// This class specifically includes methods for adding global error handling capabilities to the application.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Adds a global error handler middleware to the specified web application.
    /// This middleware is designed to catch and process unhandled exceptions globally.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> to which the error handling middleware is added.</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="GlobalErrorHandlingOptions"/>.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> with the error handling middleware configured.</returns>
    public static IApplicationBuilder UseGlobalErrorHandler(
        this WebApplication app,
        Action<GlobalErrorHandlingOptions>? configureOptions = null)
    {
        var options = new GlobalErrorHandlingOptions();
        configureOptions?.Invoke(options);
        return app.UseMiddleware<GlobalErrorHandlingMiddleware>(options);
    }
}