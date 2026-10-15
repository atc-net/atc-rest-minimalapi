namespace Atc.Rest.MinimalApi.Tests.Middleware;

/// <summary>
/// A <c>ValidationException</c> thrown by a handler (FluentValidation or DataAnnotations) is answered like the
/// ValidationFilter answers the same failures: a 400 with the field errors in <c>errors</c>.
/// </summary>
public sealed class GlobalErrorHandlingValidationExceptionTests
{
    private const string ValidationTitle = "One or more validation errors occurred.";

    public enum BodyMode
    {
        /// <summary>ProblemDetails, written by the middleware itself (no IProblemDetailsService registered).</summary>
        ProblemDetailsFallbackWriter,

        /// <summary>ProblemDetails, written through IProblemDetailsService (AddProblemDetails()).</summary>
        ProblemDetailsService,

        /// <summary>The plain dictionary body (UseProblemDetailsAsResponseBody = false).</summary>
        Dictionary,
    }

    [Theory]
    [InlineData(BodyMode.ProblemDetailsFallbackWriter, "application/problem+json")]
    [InlineData(BodyMode.ProblemDetailsService, "application/problem+json")]
    [InlineData(BodyMode.Dictionary, "application/json")]
    public async Task FluentValidationException_WithFailures_ErrorsAreKeyedByTheJsonNames(
        BodyMode bodyMode,
        string expectedContentType)
    {
        // Arrange
        var exception = new FluentValidation.ValidationException(
        [
            new FluentValidation.Results.ValidationFailure("Name", "'Name' must be a known thing."),
            new FluentValidation.Results.ValidationFailure("Lines[2].Sku", "'Sku' must not be empty."),
        ]);

        // Act
        var (context, body) = await InvokeAsync(exception, bodyMode);

        // Assert
        using (body)
        {
            context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            context.Response.ContentType.Should().StartWith(expectedContentType);

            var root = body.RootElement;
            root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status400BadRequest);
            root.GetProperty("title").GetString().Should().Be(ValidationTitle);
            root.TryGetProperty("detail", out _).Should().BeFalse("the messages are in errors");
            root.TryGetProperty("traceId", out _).Should().BeTrue();
            GetErrors(root).Should().BeEquivalentTo(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["name"] = ["'Name' must be a known thing."],
                ["lines[2].sku"] = ["'Sku' must not be empty."],
            });

            if (bodyMode != BodyMode.Dictionary)
            {
                root.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
            }
        }
    }

    [Fact]
    public async Task FluentValidationException_TwoFailuresOnOneProperty_OneKeyWithBothMessages()
    {
        // Arrange
        var exception = new FluentValidation.ValidationException(
        [
            new FluentValidation.Results.ValidationFailure("Name", "first"),
            new FluentValidation.Results.ValidationFailure("Name", "second"),
        ]);

        // Act
        var (_, body) = await InvokeAsync(exception, BodyMode.ProblemDetailsFallbackWriter);

        // Assert
        using (body)
        {
            GetErrors(body.RootElement).Should().BeEquivalentTo(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["name"] = ["first", "second"],
            });
        }
    }

    [Theory]
    [InlineData(BodyMode.ProblemDetailsFallbackWriter)]
    [InlineData(BodyMode.ProblemDetailsService)]
    [InlineData(BodyMode.Dictionary)]
    public async Task FluentValidationException_MessageOnly_MessageIsTheDetail(
        BodyMode bodyMode)
    {
        // Act
        var (context, body) = await InvokeAsync(new FluentValidation.ValidationException("The order is closed."), bodyMode);

        // Assert - no failures: nothing to key, so the message stays in detail
        using (body)
        {
            context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            body.RootElement.GetProperty("detail").GetString().Should().Be("The order is closed.");
            body.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(BodyMode.ProblemDetailsFallbackWriter)]
    [InlineData(BodyMode.ProblemDetailsService)]
    [InlineData(BodyMode.Dictionary)]
    public async Task DataAnnotationsValidationException_WithMemberName_ErrorIsKeyedByTheJsonName(
        BodyMode bodyMode)
    {
        // Arrange
        var exception = new System.ComponentModel.DataAnnotations.ValidationException(
            new ValidationResult("The FirstName field is required.", ["FirstName"]),
            validatingAttribute: null,
            value: null);

        // Act
        var (_, body) = await InvokeAsync(exception, bodyMode);

        // Assert
        using (body)
        {
            body.RootElement.GetProperty("title").GetString().Should().Be(ValidationTitle);
            body.RootElement.TryGetProperty("detail", out _).Should().BeFalse("the message belongs to its field");
            GetErrors(body.RootElement).Should().BeEquivalentTo(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["firstName"] = ["The FirstName field is required."],
            });
        }
    }

    [Theory]
    [InlineData(BodyMode.ProblemDetailsFallbackWriter)]
    [InlineData(BodyMode.ProblemDetailsService)]
    [InlineData(BodyMode.Dictionary)]
    public async Task DataAnnotationsValidationException_WithoutMemberName_IsReportedUnderTheEmptyKeyAndAsTheDetail(
        BodyMode bodyMode)
    {
        // Arrange - a DataAnnotations ValidationException always carries one ValidationResult
        var exception = new System.ComponentModel.DataAnnotations.ValidationException("The order is closed.");

        // Act
        var (_, body) = await InvokeAsync(exception, bodyMode);

        // Assert - under "" as the ValidationFilter reports an error about the body as a whole, and as the detail,
        // as a message-only FluentValidation exception gives it
        using (body)
        {
            body.RootElement.GetProperty("title").GetString().Should().Be(ValidationTitle);
            body.RootElement.GetProperty("detail").GetString().Should().Be("The order is closed.");
            GetErrors(body.RootElement).Should().BeEquivalentTo(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [string.Empty] = ["The order is closed."],
            });
        }
    }

    [Theory]
    [InlineData(BodyMode.ProblemDetailsFallbackWriter)]
    [InlineData(BodyMode.ProblemDetailsService)]
    [InlineData(BodyMode.Dictionary)]
    public async Task FluentValidationException_OnlyObjectLevelFailures_MessagesAreTheDetail(
        BodyMode bodyMode)
    {
        // Arrange - root-level rules (no property name)
        var exception = new FluentValidation.ValidationException(
        [
            new FluentValidation.Results.ValidationFailure(string.Empty, "The order is closed."),
            new FluentValidation.Results.ValidationFailure(string.Empty, "The order is paid."),
        ]);

        // Act
        var (_, body) = await InvokeAsync(exception, bodyMode);

        // Assert
        using (body)
        {
            body.RootElement.GetProperty("detail").GetString().Should().Be("The order is closed. The order is paid.");
            GetErrors(body.RootElement).Should().BeEquivalentTo(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [string.Empty] = ["The order is closed.", "The order is paid."],
            });
        }
    }

    [Fact]
    public async Task FluentValidationException_ObjectLevelAndFieldFailures_HasNoDetail()
    {
        // Arrange
        var exception = new FluentValidation.ValidationException(
        [
            new FluentValidation.Results.ValidationFailure(string.Empty, "The order is closed."),
            new FluentValidation.Results.ValidationFailure("Name", "'Name' must not be empty."),
        ]);

        // Act
        var (_, body) = await InvokeAsync(exception, BodyMode.ProblemDetailsFallbackWriter);

        // Assert - field messages belong to their fields, so no single message describes the problem
        using (body)
        {
            body.RootElement.TryGetProperty("detail", out _).Should().BeFalse();
            GetErrors(body.RootElement).Keys.Should().BeEquivalentTo(string.Empty, "name");
        }
    }

    [Fact]
    public async Task FluentValidationException_KeysFollowTheAppsJsonNamingPolicy()
    {
        // Arrange
        var exception = new FluentValidation.ValidationException(
        [
            new FluentValidation.Results.ValidationFailure("Address.PostalCode", "bad"),
        ]);

        // Act
        var (_, body) = await InvokeAsync(
            exception,
            BodyMode.ProblemDetailsFallbackWriter,
            configureServices: services => services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
                o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower));

        // Assert
        using (body)
        {
            GetErrors(body.RootElement).Keys.Should().BeEquivalentTo("address.postal_code");
        }
    }

    [Theory]
    [InlineData(BodyMode.ProblemDetailsFallbackWriter)]
    [InlineData(BodyMode.Dictionary)]
    public async Task IncludeException_DoesNotAddTheExceptionTypeToAValidationBody(
        BodyMode bodyMode)
    {
        // Arrange
        var exception = new FluentValidation.ValidationException(
        [
            new FluentValidation.Results.ValidationFailure("Name", "'Name' must be a known thing."),
        ]);

        // Act
        var (_, body) = await InvokeAsync(exception, bodyMode, includeException: true);

        // Assert
        using (body)
        {
            body.RootElement.TryGetProperty("detail", out _).Should().BeFalse();
            body.RootElement.GetRawText().Should().NotContain(nameof(FluentValidation.ValidationException));
            GetErrors(body.RootElement).Keys.Should().BeEquivalentTo("name");
        }
    }

    [Fact]
    public async Task IncludeException_MessageOnly_DetailIsThePlainMessage()
    {
        // Act - a validation message is meant for the client, as BadHttpRequestException's is
        var (_, body) = await InvokeAsync(
            new FluentValidation.ValidationException("The order is closed."),
            BodyMode.ProblemDetailsFallbackWriter,
            includeException: true);

        // Assert
        using (body)
        {
            body.RootElement.GetProperty("detail").GetString().Should().Be("The order is closed.");
        }
    }

    [Fact]
    public async Task CustomMappingToAnotherStatus_KeepsThePlainProblemDetails()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions()
            .MapException<FluentValidation.ValidationException>(HttpStatusCode.UnprocessableEntity);
        var exception = new FluentValidation.ValidationException(
        [
            new FluentValidation.Results.ValidationFailure("Name", "'Name' must be a known thing."),
        ]);

        // Act
        var (context, body) = await InvokeAsync(exception, BodyMode.ProblemDetailsFallbackWriter, options: options);

        // Assert
        using (body)
        {
            context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
            body.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
            body.RootElement.GetProperty("detail").GetString().Should().Contain("'Name' must be a known thing.");
        }
    }

    private static async Task<(HttpContext Context, JsonDocument Body)> InvokeAsync(
        Exception exception,
        BodyMode bodyMode,
        bool includeException = false,
        GlobalErrorHandlingOptions? options = null,
        Action<IServiceCollection>? configureServices = null)
    {
        options ??= new GlobalErrorHandlingOptions();
        options.IncludeException = includeException;
        options.UseProblemDetailsAsResponseBody = bodyMode != BodyMode.Dictionary;

        var services = new ServiceCollection();
        services.AddLogging();
        if (bodyMode == BodyMode.ProblemDetailsService)
        {
            services.AddProblemDetails();
        }

        configureServices?.Invoke(services);

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };

        var middleware = new GlobalErrorHandlingMiddleware(
            next: _ => throw exception,
            options: options);

        await middleware.Invoke(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return (context, body);
    }

    private static Dictionary<string, string[]> GetErrors(JsonElement root)
        => root
            .GetProperty("errors")
            .EnumerateObject()
            .ToDictionary(
                p => p.Name,
                p => p.Value.EnumerateArray().Select(m => m.GetString()!).ToArray(),
                StringComparer.Ordinal);
}