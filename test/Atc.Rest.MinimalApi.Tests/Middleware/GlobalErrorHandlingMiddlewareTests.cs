namespace Atc.Rest.MinimalApi.Tests.Middleware;

public sealed class GlobalErrorHandlingMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenExceptionThrown_ReturnsValidJson()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = false,
            IncludeException = true,
        };

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new InvalidOperationException("Test error message"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        // This should be valid JSON - currently it fails because the output is not valid JSON
        var parseAction = () => JsonDocument.Parse(responseBody);
        parseAction.Should().NotThrow<JsonException>(
            because: "the error response should be valid JSON, but got: {0}", responseBody);
    }

    [Fact]
    public async Task Invoke_WhenExceptionThrown_WithProblemDetails_ReturnsValidJson()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = true,
            IncludeException = true,
        };

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new ArgumentException("Invalid argument"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        // ProblemDetails path - this should work as it uses JsonSerializer
        var parseAction = () => JsonDocument.Parse(responseBody);
        parseAction.Should().NotThrow<JsonException>(
            because: "the ProblemDetails response should be valid JSON");
    }

    [Fact]
    public async Task Invoke_WhenExceptionThrown_ResponseContainsExpectedFields()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = false,
            IncludeException = true,
        };

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new InvalidOperationException("Test error"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        // Parse as JSON and verify fields exist
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        root
            .TryGetProperty("status", out var status)
            .Should()
            .BeTrue();

        status
            .GetInt32()
            .Should()
            .Be(409); // InvalidOperationException -> Conflict

        root
            .TryGetProperty("title", out var title)
            .Should()
            .BeTrue();

        title
            .GetString()
            .Should()
            .Be("Conflict");
    }
}