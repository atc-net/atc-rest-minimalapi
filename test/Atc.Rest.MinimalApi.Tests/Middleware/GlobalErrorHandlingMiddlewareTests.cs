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

    [Fact]
    public async Task Invoke_WithCustomMapping_ReturnsCustomStatusCode()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = false,
        };

        options.MapException<KeyNotFoundException>(HttpStatusCode.NotFound);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new KeyNotFoundException("Resource not found"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(responseBody);
        doc.RootElement
            .GetProperty("status")
            .GetInt32()
            .Should()
            .Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Invoke_WithInheritedCustomMapping_ReturnsBaseTypeStatusCode()
    {
        // Arrange - FileNotFoundException derives from IOException
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = false,
        };

        options.MapException<IOException>(HttpStatusCode.ServiceUnavailable);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new FileNotFoundException("File not found"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert - FileNotFoundException should match IOException mapping via inheritance
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Invoke_CustomMappingTakesPrecedenceOverDefault()
    {
        // Arrange - Override default ArgumentException mapping (400 -> 422)
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = false,
        };

        options.MapException<ArgumentException>(HttpStatusCode.UnprocessableEntity);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new ArgumentException("Invalid argument"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert - Should be 422, not the default 400
        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void MapException_NonGeneric_WithNonExceptionType_ThrowsArgumentException()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions();

        // Act & Assert
        var act = () => options.MapException(typeof(string), HttpStatusCode.BadRequest);
        act
            .Should()
            .Throw<ArgumentException>()
            .WithParameterName("exceptionType");
    }

    [Fact]
    public void MapException_SupportsMethodChaining()
    {
        // Arrange & Act
        var options = new GlobalErrorHandlingOptions()
            .MapException<KeyNotFoundException>(HttpStatusCode.NotFound)
            .MapException<InvalidOperationException>(HttpStatusCode.UnprocessableEntity)
            .MapException<UnauthorizedAccessException>(HttpStatusCode.Forbidden);

        // Assert - Just verify no exception is thrown and chaining works
        options.Should().NotBeNull();
    }

    [Fact]
    public async Task Invoke_WhenDownstreamTimeoutThrowsTaskCanceled_Returns504ProblemDetails()
    {
        // Arrange - RequestAborted is NOT triggered, simulating a downstream HttpClient.Timeout
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = true,
        };

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new TaskCanceledException("Simulated downstream timeout"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status504GatewayTimeout);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        responseBody.Should().NotBeEmpty();

        using var doc = JsonDocument.Parse(responseBody);
        doc.RootElement
            .GetProperty("status")
            .GetInt32()
            .Should()
            .Be(StatusCodes.Status504GatewayTimeout);
    }

    [Fact]
    public async Task Invoke_WhenOperationCanceledThrown_Returns504ProblemDetails()
    {
        // Arrange - base type, verifies the resolver entry rather than just inheritance
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = true,
        };

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new OperationCanceledException("Simulated handler-level deadline"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status504GatewayTimeout);
    }

    [Fact]
    public async Task Invoke_WhenUpstreamClientDisconnectsDuringHandler_DoesNotWriteResponse()
    {
        // Arrange - RequestAborted IS triggered, upstream client genuinely disconnected
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = true,
        };

        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        var context = new DefaultHttpContext
        {
            RequestAborted = aborted.Token,
            Response = { Body = new MemoryStream() },
        };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new OperationCanceledException(aborted.Token),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert - silent: no body written and status code untouched
        context.Response.Body.Length.Should().Be(0);
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Invoke_WithCustomMappingForTaskCanceled_OverridesDefault()
    {
        // Arrange - consumers can opt out of 504 in favour of e.g. 408
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = false,
        };

        options.MapException<TaskCanceledException>(HttpStatusCode.RequestTimeout);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new TaskCanceledException("Simulated downstream timeout"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status408RequestTimeout);
    }

    [Fact]
    public async Task Invoke_WhenTimeoutExceptionThrown_Returns504ProblemDetails()
    {
        // Arrange - regression: ensure TimeoutException still maps to 504 alongside the new
        // OperationCanceledException mapping.
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = true,
        };

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new TimeoutException("Simulated timeout"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status504GatewayTimeout);
    }

    [Fact]
    public async Task Invoke_WithTeapotMapping_Returns418()
    {
        // Arrange - Demonstrate custom exception with 418 I'm a teapot (not in HttpStatusCode enum)
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = true,
        };
        options.MapException<TeapotTestException>((HttpStatusCode)StatusCodes.Status418ImATeapot);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new TeapotTestException("I'm a teapot!"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be(418);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(responseBody);
        doc.RootElement
            .GetProperty("status")
            .GetInt32()
            .Should()
            .Be(StatusCodes.Status418ImATeapot);
    }
}