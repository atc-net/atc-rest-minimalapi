extern alias MinimalApi;

namespace Atc.Rest.MinimalApi.Tests.Middleware;

public sealed class GlobalErrorHandlingMiddlewareTests
{
    // WellKnownHttpHeaders exists in both Atc.Rest.MinimalApi and Atc.Rest (via Atc.Rest.AwesomeAssertions)
    // with the same full name (CS0433), so it is referenced through the MinimalApi extern alias.
    private const string CorrelationIdHeader = MinimalApi::Microsoft.AspNetCore.Http.WellKnownHttpHeaders.CorrelationId;
    private const string RequestIdHeader = MinimalApi::Microsoft.AspNetCore.Http.WellKnownHttpHeaders.RequestId;

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

    [Fact]
    public async Task Invoke_WithLoggerFactory_WhenUnhandledException_LogsSingleErrorWithException()
    {
        // Arrange
        using var loggerProvider = new FakeLoggerProvider();
        using var loggerFactory = new LoggerFactory([loggerProvider]);

        var exception = new InvalidDataException("boom");
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw exception,
            loggerFactory: loggerFactory);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var errorLogs = GetLogs(loggerProvider, LogLevel.Error);

        errorLogs.Should().ContainSingle();
        errorLogs[0].Exception.Should().BeSameAs(exception);
        errorLogs[0].Category.Should().Be(typeof(GlobalErrorHandlingMiddleware).FullName);
    }

    [Fact]
    public async Task Invoke_WithLoggerFactory_WhenClientErrorException_LogsDebugWithoutException()
    {
        // Arrange
        using var loggerProvider = new FakeLoggerProvider();
        using var loggerFactory = new LoggerFactory([loggerProvider]);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new ArgumentException("Invalid argument"),
            loggerFactory: loggerFactory);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        GetLogs(loggerProvider, LogLevel.Error).Should().BeEmpty();
        GetLogs(loggerProvider, LogLevel.Information).Should().BeEmpty();

        var debugLogs = GetLogs(loggerProvider, LogLevel.Debug);
        debugLogs.Should().ContainSingle();
        debugLogs[0].Exception.Should().BeNull();
        debugLogs[0].Message.Should().Contain("Invalid argument");
    }

    [Fact]
    public async Task Invoke_WithLoggerFactory_WhenUpstreamClientDisconnects_LogsDebugOnly()
    {
        // Arrange
        using var loggerProvider = new FakeLoggerProvider();
        using var loggerFactory = new LoggerFactory([loggerProvider]);

        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        var context = new DefaultHttpContext
        {
            RequestAborted = aborted.Token,
            Response = { Body = new MemoryStream() },
        };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new OperationCanceledException(aborted.Token),
            loggerFactory: loggerFactory);

        // Act
        await middleware.Invoke(context);

        // Assert
        GetLogs(loggerProvider, LogLevel.Error).Should().BeEmpty();
        GetLogs(loggerProvider, LogLevel.Information).Should().BeEmpty();

        var debugLogs = GetLogs(loggerProvider, LogLevel.Debug);
        debugLogs.Should().ContainSingle();
        debugLogs[0].Exception.Should().BeNull();
    }

    [Fact]
    public async Task Invoke_WithLoggerFactory_WhenCorrelationIdHeaderPresent_LogScopeContainsCorrelationId()
    {
        // Arrange
        using var loggerProvider = new FakeLoggerProvider();
        using var loggerFactory = new LoggerFactory([loggerProvider]);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        context.Request.Headers[CorrelationIdHeader] = "corr-123";

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new InvalidDataException("boom"),
            loggerFactory: loggerFactory);

        // Act
        await middleware.Invoke(context);

        // Assert
        var errorLogs = GetLogs(loggerProvider, LogLevel.Error);
        errorLogs.Should().ContainSingle();

        GetScopeValues(errorLogs[0])
            .Should()
            .Contain(new KeyValuePair<string, object?>("CorrelationId", "corr-123"));
    }

    [Fact]
    public async Task Invoke_WithLoggerFactory_WhenRequestIdHeaderPresent_LogScopeContainsClientRequestId()
    {
        // Arrange
        using var loggerProvider = new FakeLoggerProvider();
        using var loggerFactory = new LoggerFactory([loggerProvider]);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        context.Request.Headers[RequestIdHeader] = "req-456";

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new InvalidDataException("boom"),
            loggerFactory: loggerFactory);

        // Act
        await middleware.Invoke(context);

        // Assert
        var errorLogs = GetLogs(loggerProvider, LogLevel.Error);
        errorLogs.Should().ContainSingle();

        GetScopeValues(errorLogs[0])
            .Should()
            .Contain(new KeyValuePair<string, object?>("ClientRequestId", "req-456"));
    }

    [Theory]
    [InlineData(typeof(InvalidDataException))] // 5xx - Error
    [InlineData(typeof(ArgumentException))] // 4xx - Debug
    public async Task Invoke_WithLoggerFactory_WhenLogLevelDisabled_DoesNotCreateLogScope(
        Type exceptionType)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(false);

        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);

        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        context.Request.Headers[CorrelationIdHeader] = "corr-123";

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw exception,
            loggerFactory: loggerFactory);

        // Act
        await middleware.Invoke(context);

        // Assert
        logger
            .DidNotReceiveWithAnyArgs()
            .BeginScope(Arg.Any<Dictionary<string, object?>>());
    }

    private static List<FakeLogRecord> GetLogs(
        FakeLoggerProvider loggerProvider,
        LogLevel level)
        => loggerProvider.Collector
            .GetSnapshot()
            .Where(x => x.Level == level)
            .ToList();

    private static List<KeyValuePair<string, object?>> GetScopeValues(
        FakeLogRecord logRecord)
        => logRecord.Scopes
            .OfType<IEnumerable<KeyValuePair<string, object?>>>()
            .SelectMany(x => x)
            .ToList();
}