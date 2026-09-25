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
            .Be(500); // InvalidOperationException -> 500 (no longer 409 Conflict by default)

        root
            .TryGetProperty("title", out var title)
            .Should()
            .BeTrue();

        title
            .GetString()
            .Should()
            .Be("An error occurred while processing your request.");
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

    [Fact]
    public async Task Invoke_WhenResponseHasStarted_RethrowsException()
    {
        // Arrange - the handler already sent the status line/headers before throwing
        var exception = new InvalidDataException("boom after the response started");
        var context = CreateHttpContextWithStartedResponse();

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw exception);

        // Act
        var act = () => middleware.Invoke(context);

        // Assert - the exception propagates, so the server aborts the connection
        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which
            .Should()
            .BeSameAs(exception);
    }

    [Fact]
    public async Task Invoke_WithLoggerFactory_WhenResponseHasStarted_LogsSingleWarningWithoutException()
    {
        // Arrange
        using var loggerProvider = new FakeLoggerProvider();
        using var loggerFactory = new LoggerFactory([loggerProvider]);

        var exception = new InvalidDataException("boom after the response started");
        var context = CreateHttpContextWithStartedResponse();

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw exception,
            loggerFactory: loggerFactory);

        // Act
        var act = () => middleware.Invoke(context);
        await act.Should().ThrowAsync<InvalidDataException>();

        // Assert
        // Warning without the exception: the server (Kestrel) logs the rethrown exception at Error itself
        GetLogs(loggerProvider, LogLevel.Error).Should().BeEmpty();

        var warningLogs = GetLogs(loggerProvider, LogLevel.Warning);
        warningLogs.Should().ContainSingle();
        warningLogs[0].Exception.Should().BeNull();
        warningLogs[0].Id.Id.Should().Be(10_003);
    }

    [Fact]
    public async Task Invoke_WithLoggerFactory_WhenResponseHasStarted_LogScopeContainsClientIds()
    {
        // Arrange
        using var loggerProvider = new FakeLoggerProvider();
        using var loggerFactory = new LoggerFactory([loggerProvider]);

        var context = CreateHttpContextWithStartedResponse();
        context.Request.Headers[CorrelationIdHeader] = "corr-123";
        context.Request.Headers[RequestIdHeader] = "req-456";

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new InvalidDataException("boom after the response started"),
            loggerFactory: loggerFactory);

        // Act
        var act = () => middleware.Invoke(context);
        await act.Should().ThrowAsync<InvalidDataException>();

        // Assert
        var warningLogs = GetLogs(loggerProvider, LogLevel.Warning);
        warningLogs.Should().ContainSingle();

        var scopeValues = GetScopeValues(warningLogs[0]);
        scopeValues.Should().Contain(new KeyValuePair<string, object?>("CorrelationId", "corr-123"));
        scopeValues.Should().Contain(new KeyValuePair<string, object?>("ClientRequestId", "req-456"));
    }

    [Fact]
    public async Task Invoke_WithLoggerFactory_WhenUpstreamClientDisconnects_LogScopeContainsClientIds()
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
        context.Request.Headers[CorrelationIdHeader] = "corr-123";
        context.Request.Headers[RequestIdHeader] = "req-456";

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new OperationCanceledException(aborted.Token),
            loggerFactory: loggerFactory);

        // Act
        await middleware.Invoke(context);

        // Assert
        var debugLogs = GetLogs(loggerProvider, LogLevel.Debug);
        debugLogs.Should().ContainSingle();

        var scopeValues = GetScopeValues(debugLogs[0]);
        scopeValues.Should().Contain(new KeyValuePair<string, object?>("CorrelationId", "corr-123"));
        scopeValues.Should().Contain(new KeyValuePair<string, object?>("ClientRequestId", "req-456"));
    }

    [Theory]
    [InlineData(true, "application/problem+json")] // RFC 9457 ProblemDetails body
    [InlineData(false, "application/json")] // plain dictionary body, not claiming to be RFC 9457
    public async Task Invoke_WhenExceptionThrown_SetsContentTypeForBodyFormat(
        bool useProblemDetailsAsResponseBody,
        string expectedContentType)
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions
        {
            UseProblemDetailsAsResponseBody = useProblemDetailsAsResponseBody,
        };

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new InvalidDataException("boom"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.ContentType.Should().Be(expectedContentType);
    }

    [Theory]
    [InlineData(typeof(ArgumentException), 400, "https://tools.ietf.org/html/rfc9110#section-15.5.1", "Bad Request")]
    [InlineData(typeof(InvalidOperationException), 500, "https://tools.ietf.org/html/rfc9110#section-15.6.1", "An error occurred while processing your request.")]
    [InlineData(typeof(InvalidDataException), 500, "https://tools.ietf.org/html/rfc9110#section-15.6.1", "An error occurred while processing your request.")]
    [InlineData(typeof(NotImplementedException), 501, "https://tools.ietf.org/html/rfc9110#section-15.6.2", "Not Implemented")]
    [InlineData(typeof(TimeoutException), 504, "https://tools.ietf.org/html/rfc9110#section-15.6.5", "Gateway Timeout")]
    public async Task Invoke_WithProblemDetailsBody_SetsTypeAndTitleLikeAspNetCore(
        Type exceptionType,
        int expectedStatus,
        string expectedType,
        string expectedTitle)
    {
        // Arrange
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw (Exception)Activator.CreateInstance(exceptionType, "boom")!);

        // Act
        await middleware.Invoke(context);

        // Assert - the same type and title as TypedResults.Problem(statusCode) in the same app
        using var body = await ReadJsonBodyAsync(context);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        body.RootElement.GetProperty("type").GetString().Should().Be(expectedType);
        body.RootElement.GetProperty("title").GetString().Should().Be(expectedTitle);
    }

    [Fact]
    public async Task Invoke_WithProblemDetailsBody_StatusWithoutRfcLink_OmitsTypeAndUsesReasonPhrase()
    {
        // Arrange - 418 has no RFC link in ASP.NET Core's defaults
        var options = new GlobalErrorHandlingOptions()
            .MapException<TeapotTestException>((HttpStatusCode)StatusCodes.Status418ImATeapot);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new TeapotTestException("I'm a teapot!"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert - like TypedResults.Problem(418): no type (RFC 9457: absent means "about:blank")
        using var body = await ReadJsonBodyAsync(context);
        body.RootElement.TryGetProperty("type", out _).Should().BeFalse();
        body.RootElement.GetProperty("title").GetString().Should().Be("I'm a teapot");
    }

    [Theory]
    [InlineData(typeof(InvalidDataException), "An error occurred while processing your request.")]
    [InlineData(typeof(TimeoutException), "Gateway Timeout")]
    public async Task Invoke_WithDictionaryBody_UsesSameTitleAsProblemDetailsBody(
        Type exceptionType,
        string expectedTitle)
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions { UseProblemDetailsAsResponseBody = false };
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw (Exception)Activator.CreateInstance(exceptionType, "boom")!,
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        using var body = await ReadJsonBodyAsync(context);
        body.RootElement.GetProperty("title").GetString().Should().Be(expectedTitle);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException), 500)] // ISSUE-04: a server bug, not a 409 Conflict
    [InlineData(typeof(ObjectDisposedException), 500)] // derives from InvalidOperationException
    [InlineData(typeof(UnauthorizedAccessException), 500)] // X1: usually a file-system/OS "access denied", not a 401
    [InlineData(typeof(ArgumentNullException), 500)] // likely a coding error (e.g. ThrowIfNull inside the server)
    [InlineData(typeof(ArgumentOutOfRangeException), 500)] // likely a coding error (e.g. an index out of range)
    [InlineData(typeof(CustomArgumentOutOfRangeTestException), 500)] // the most-derived default mapping wins
    [InlineData(typeof(ArgumentException), 400)] // commonly thrown on purpose for bad input
    [InlineData(typeof(System.Globalization.CultureNotFoundException), 400)] // another ArgumentException subclass
    public async Task Invoke_DefaultMappings_ResolveStatusCode(
        Type exceptionType,
        int expectedStatusCode)
    {
        // Arrange
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw (Exception)Activator.CreateInstance(exceptionType, "boom")!);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be(expectedStatusCode);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException), HttpStatusCode.Conflict)] // restores the 2.x 409
    [InlineData(typeof(UnauthorizedAccessException), HttpStatusCode.Unauthorized)] // restores the 2.x 401
    [InlineData(typeof(ArgumentNullException), HttpStatusCode.BadRequest)] // restores the 2.x 400
    [InlineData(typeof(ArgumentOutOfRangeException), HttpStatusCode.BadRequest)] // restores the 2.x 400
    public async Task Invoke_WithOptInMapping_RestoresPreviousStatusCode(
        Type exceptionType,
        HttpStatusCode mappedStatusCode)
    {
        // Arrange - the one-line migration for consumers who relied on the old defaults
        var options = new GlobalErrorHandlingOptions()
            .MapException(exceptionType, mappedStatusCode);

        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw (Exception)Activator.CreateInstance(exceptionType, "boom")!,
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)mappedStatusCode);
    }

    [Fact]
    public void Options_IncludeException_DefaultsToFalse()
        => new GlobalErrorHandlingOptions().IncludeException.Should().BeFalse();

    [Theory]
    [InlineData(true)] // ProblemDetails body
    [InlineData(false)] // plain dictionary body
    public async Task Invoke_DefaultOptions_ServerError_DoesNotLeakExceptionDetails(
        bool useProblemDetailsAsResponseBody)
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions { UseProblemDetailsAsResponseBody = useProblemDetailsAsResponseBody };
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new InvalidDataException("boom from handler"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert - no detail at all: neither the type name nor the message
        using var body = await ReadJsonBodyAsync(context);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status500InternalServerError);
        body.RootElement.TryGetProperty("detail", out _).Should().BeFalse();
        body.RootElement.GetRawText().Should().NotContain("InvalidData").And.NotContain("boom");
    }

    [Fact]
    public async Task Invoke_DefaultOptions_ClientError_ReturnsMessageWithoutTypeName()
    {
        // Arrange
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new ArgumentException("The value 'x' is not valid."));

        // Act
        await middleware.Invoke(context);

        // Assert - the message is meant for the caller; the type name is internal
        using var body = await ReadJsonBodyAsync(context);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status400BadRequest);
        var detail = body.RootElement.GetProperty("detail").GetString();
        detail.Should().Contain("The value 'x' is not valid.");
        detail.Should().NotContain("Argument");
    }

    [Fact]
    public async Task Invoke_IncludeException_ServerError_ReturnsDetailedText()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions { IncludeException = true };
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw new InvalidDataException("boom from handler"),
            options: options);

        // Act
        await middleware.Invoke(context);

        // Assert - opt-in (e.g. Development): today's detailed text with the type name
        using var body = await ReadJsonBodyAsync(context);
        var detail = body.RootElement.GetProperty("detail").GetString();
        detail.Should().Contain("InvalidData").And.Contain("boom from handler");
    }

    private static Task<JsonDocument> ReadJsonBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static DefaultHttpContext CreateHttpContextWithStartedResponse()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedHttpResponseFeature());
        context.Response.Body = new MemoryStream();
        return context;
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

    /// <summary>
    /// A response feature that reports the response as started, like a real server does
    /// once the status line and headers have been sent.
    /// </summary>
    private sealed class StartedHttpResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}