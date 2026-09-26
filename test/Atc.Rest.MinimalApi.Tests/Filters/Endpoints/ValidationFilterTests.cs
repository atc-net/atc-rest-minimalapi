namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints;

public sealed class ValidationFilterTests
{
    [Fact]
    public async Task InvokeAsync_WithValidModel_CallsNext()
    {
        // Arrange
        var model = new CreateLocationRequestWithJsonPropertyNames(
            new AddressWithJsonPropertyNames("DNK", "Place", "Street", "PostalCode", "City"),
            "12345678");

        var (filter, context, nextWasCalled) = CreateTestContext(
            model,
            new CreateLocationRequestWithJsonPropertyNamesValidator());

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextWasCalled.Value = true;
            return ValueTask.FromResult<object?>("Success");
        });

        // Assert
        Assert.True(nextWasCalled.Value, "Next delegate should have been called for valid model");
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidDataAnnotations_ReturnsValidationProblem()
    {
        // Arrange - Address.CountryCodeA3 has [MinLength(3)] and [MaxLength(3)] attributes
        var model = new CreateLocationRequestWithJsonPropertyNames(
            new AddressWithJsonPropertyNames("DK", "Place", "Street", "PostalCode", "City"), // DK is only 2 chars, violates MinLength(3)
            "12345678");

        var (filter, context, nextWasCalled) = CreateTestContext(model);

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextWasCalled.Value = true;
            return ValueTask.FromResult<object?>("Success");
        });

        // Assert
        Assert.False(nextWasCalled.Value, "Next delegate should NOT have been called when DataAnnotations validation fails");
        var validationProblem = Assert.IsType<ValidationProblem>(result);
        Assert.NotEmpty(validationProblem.ProblemDetails.Errors);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidFluentValidation_ReturnsValidationProblem()
    {
        // Arrange - Telephone must not be empty according to FluentValidation
        var model = new CreateLocationRequestWithJsonPropertyNames(
            new AddressWithJsonPropertyNames("DNK", "Place", "Street", "PostalCode", "City"),
            ""); // Empty telephone violates FluentValidation rule

        var (filter, context, nextWasCalled) = CreateTestContext(
            model,
            new CreateLocationRequestWithJsonPropertyNamesValidator());

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextWasCalled.Value = true;
            return ValueTask.FromResult<object?>("Success");
        });

        // Assert
        Assert.False(nextWasCalled.Value, "Next delegate should NOT have been called when FluentValidation fails");
        var validationProblem = Assert.IsType<ValidationProblem>(result);
        Assert.NotEmpty(validationProblem.ProblemDetails.Errors);
        Assert.Contains(validationProblem.ProblemDetails.Errors, e =>
            e.Value.Any(v => v.Contains("Telephone", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task InvokeAsync_WithBothValidationErrors_MergesErrors()
    {
        // Arrange - Both DataAnnotations (CountryCodeA3 too short) and FluentValidation (Telephone empty) fail
        var model = new CreateLocationRequestWithJsonPropertyNames(
            new AddressWithJsonPropertyNames("DK", "Place", "Street", "PostalCode", "City"), // DK violates MinLength(3)
            ""); // Empty violates FluentValidation

        var (filter, context, nextWasCalled) = CreateTestContext(
            model,
            new CreateLocationRequestWithJsonPropertyNamesValidator());

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextWasCalled.Value = true;
            return ValueTask.FromResult<object?>("Success");
        });

        // Assert
        Assert.False(nextWasCalled.Value);
        var validationProblem = Assert.IsType<ValidationProblem>(result);

        // Should have errors from both validation sources
        Assert.True(
            validationProblem.ProblemDetails.Errors.Count >= 2,
            "Should have errors from both DataAnnotations and FluentValidation");
    }

    [Fact]
    public async Task InvokeAsync_WithNullArgumentForOptionalHandlerParameter_SkipsValidationAndCallsNext()
    {
        // Arrange - the handler declares an optional T parameter (e.g. an optional body) and the client sent nothing
        var services = new ServiceCollection().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };

        var handler = typeof(ValidationFilterTests).GetMethod(
            nameof(HandlerWithOptionalRequest),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        httpContext.SetEndpoint(new Endpoint(requestDelegate: null, new EndpointMetadataCollection(handler), "test"));

        var context = new DefaultEndpointFilterInvocationContext(httpContext, [null]);

        var filter = new ValidationFilter<CreateLocationRequestWithJsonPropertyNames>();
        var nextWasCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextWasCalled = true;
            return ValueTask.FromResult<object?>("Success");
        });

        // Assert - nothing to validate, and the endpoint allows the value to be missing
        Assert.True(nextWasCalled, "Next delegate should have been called for a null optional argument");
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task InvokeAsync_WithArgumentOfDerivedType_ValidatesIt()
    {
        // Arrange - the argument is a subclass of T, with a missing [Required] Name
        var services = new ServiceCollection().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };

        var context = new DefaultEndpointFilterInvocationContext(
            httpContext,
            new EmployeeRequest { Department = "IT" });

        var filter = new ValidationFilter<PersonRequest>();

        // Act
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));

        // Assert - validated like a PersonRequest, not treated as a missing argument
        var validationProblem = Assert.IsType<ValidationProblem>(result);
        Assert.Contains("name", validationProblem.ProblemDetails.Errors.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WithLoggerFactory_WhenMissingArgument_LogsSingleErrorWithTypeName()
    {
        // Arrange
        using var loggerProvider = new FakeLoggerProvider();
        using var loggerFactory = new LoggerFactory([loggerProvider]);

        var services = new ServiceCollection().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };

        var context = new DefaultEndpointFilterInvocationContext(
            httpContext,
            "some string argument");

        var filter = new ValidationFilter<CreateLocationRequestWithJsonPropertyNames>(
            loggerFactory: loggerFactory);

        // Act
        await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));

        // Assert - the only server-side record of this 500, so it is logged at Error
        var errorLogs = GetErrorLogs(loggerProvider);

        Assert.Single(errorLogs);
        Assert.Equal(10_100, errorLogs[0].Id.Id);
        Assert.Contains(nameof(CreateLocationRequestWithJsonPropertyNames), errorLogs[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WithMissingArgument_ReturnsInternalServerErrorProblem()
    {
        // Arrange - Create context without the expected argument type (a server misconfiguration)
        var services = new ServiceCollection().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };

        var context = new DefaultEndpointFilterInvocationContext(
            httpContext,
            "some string argument"); // Wrong type - filter expects CreateLocationRequestWithJsonPropertyNames

        var filter = new ValidationFilter<CreateLocationRequestWithJsonPropertyNames>();

        // Act
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));

        // Assert - ProblemDetails (not a bare string), 500, and no internal type names in the response
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        Assert.Equal("The endpoint is misconfigured.", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_WithNestedFromBodyProperty_ValidatesNestedType()
    {
        // Arrange - CreateLocationRequestWithRequest has [FromBody] on Request property
        var model = new CreateLocationRequestWithRequest(
            "LocationId",
            new CreateLocationRequestWithJsonPropertyNames(
                new AddressWithJsonPropertyNames("DNK", "Place", "Street", "PostalCode", "City"),
                "")); // Empty telephone should trigger nested validation

        // Register validator for the nested type (CreateLocationRequestWithJsonPropertyNames)
        var (filter, context, nextWasCalled) = CreateTestContext(
            model,
            new CreateLocationRequestWithJsonPropertyNamesValidator()); // Validator for the [FromBody] property type

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextWasCalled.Value = true;
            return ValueTask.FromResult<object?>("Success");
        });

        // Assert - Should fail because nested [FromBody] property validation should run
        Assert.False(nextWasCalled.Value, "Next delegate should NOT have been called when nested validation fails");
        var validationProblem = Assert.IsType<ValidationProblem>(result);
        Assert.NotEmpty(validationProblem.ProblemDetails.Errors);
    }

    [Fact]
    public async Task InvokeAsync_WithJsonPropertyNames_ResolvesSerializationNames()
    {
        // Arrange - AddressWithJsonPropertyNames has [JsonPropertyName("country_code_a3")] attribute
        var model = new CreateLocationRequestWithJsonPropertyNames(
            new AddressWithJsonPropertyNames("DK", "Place", "Street", "PostalCode", "City"), // DK is too short
            "12345678");

        var (filter, context, _) = CreateTestContext(
            model,
            new CreateLocationRequestWithJsonPropertyNamesValidator());

        // Act
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));

        // Assert - Error key should use the JSON property name
        var validationProblem = Assert.IsType<ValidationProblem>(result);

        // The key should be resolved to use JSON property names like "address.country_code_a3"
        Assert.Contains(validationProblem.ProblemDetails.Errors, e =>
            e.Key.Contains("country_code_a3", StringComparison.Ordinal) ||
            e.Key.Contains("CountryCodeA3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_WithSkipFirstLevel_StripsPrefix()
    {
        // Arrange - Model with Request wrapper (simulating parameter binding)
        var model = new CreateLocationRequestWithRequest(
            "LocationId",
            new CreateLocationRequestWithJsonPropertyNames(
                new AddressWithJsonPropertyNames("DNK", "Place", "Street", "PostalCode", "C"), // City too short
                "1")); // Telephone too short

        var options = new ValidationFilterOptions { KeyStyle = ValidationKeyStyle.MemberPath, SkipFirstLevelOnValidationKeys = true };
        var (filter, context, _) = CreateTestContext(
            model,
            options,
            new CreateLocationRequestWithRequestValidator());

        // Act
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));

        // Assert - Error keys should have first level stripped (no "Request." prefix)
        var validationProblem = Assert.IsType<ValidationProblem>(result);
        Assert.NotEmpty(validationProblem.ProblemDetails.Errors);

        // Keys should NOT start with "Request."
        foreach (var (key, _) in validationProblem.ProblemDetails.Errors)
        {
            Assert.False(
                key.StartsWith("Request.", StringComparison.Ordinal),
                $"Key '{key}' should not start with 'Request.' when SkipFirstLevelOnValidationKeys is true");
        }
    }

    private static (ValidationFilter<T> Filter, EndpointFilterInvocationContext Context, StrongBox<bool> NextWasCalled)
        CreateTestContext<T>(
            T argument,
            params IValidator[] validators)
        where T : class
        => CreateTestContext(argument, validationFilterOptions: null, validators);

    private static (ValidationFilter<T> Filter, EndpointFilterInvocationContext Context, StrongBox<bool> NextWasCalled)
        CreateTestContext<T>(
            T argument,
            ValidationFilterOptions? validationFilterOptions,
            params IValidator[] validators)
        where T : class
    {
        var services = new ServiceCollection();

        foreach (var validator in validators)
        {
            var validatorType = validator.GetType();
            var interfaces = validatorType
                .GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>));

            foreach (var @interface in interfaces)
            {
                services.AddSingleton(@interface, validator);
            }
        }

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };

        var context = new DefaultEndpointFilterInvocationContext(httpContext, argument);
        var filter = new ValidationFilter<T>(validationFilterOptions);
        var nextWasCalled = new StrongBox<bool>(value: false);

        return (filter, context, nextWasCalled);
    }

    private static List<FakeLogRecord> GetErrorLogs(
        FakeLoggerProvider loggerProvider)
        => loggerProvider.Collector
            .GetSnapshot()
            .Where(x => x.Level == LogLevel.Error)
            .ToList();

    /// <summary>
    /// Stands in for an endpoint handler with an optional parameter of the validated type;
    /// only its signature is used (as endpoint metadata).
    /// </summary>
    private static string HandlerWithOptionalRequest(
        CreateLocationRequestWithJsonPropertyNames? request)
        => request?.Telephone ?? string.Empty;
}