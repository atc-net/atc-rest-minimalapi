namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints;

/// <summary>
/// <see cref="RequestValidation"/> validates a value an endpoint builds itself (a form bound field by field) as
/// <see cref="ValidationFilter{T}"/> validates its argument: the parameters record's member without a binding attribute
/// is the body, as for a <c>[FromBody]</c> member.
/// </summary>
public sealed class RequestValidationTests
{
    [Fact]
    public async Task ValidateAsync_ValidValue_ReturnsNull()
    {
        // Arrange
        var parameters = new FormParameters("s-1", new ShipmentForm("parcel", new FormAddress("Main Street", "1000")));

        // Act
        var result = await RequestValidation.ValidateAsync(parameters, CreateHttpContext());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_MissingNestedRequired_ReturnsValidationProblem_KeyedByWireNames()
    {
        // Arrange - the form field "address" is a JSON part without its required "street"
        var parameters = new FormParameters(null, new ShipmentForm("parcel", new FormAddress(null, "1000")));

        // Act
        var result = await RequestValidation.ValidateAsync(parameters, CreateHttpContext());

        // Assert - the body level is dropped; the route member keeps its binding name
        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        problem.ProblemDetails.Errors.Keys.Should().BeEquivalentTo("address.street", "shipmentId");
    }

    [Fact]
    public async Task ValidateAsync_MissingBody_IsTheEmptyKey()
    {
        // Act
        var result = await RequestValidation.ValidateAsync(new FormParameters("s-1", null), CreateHttpContext());

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result);
        var error = Assert.Single(problem.ProblemDetails.Errors);
        Assert.Equal(string.Empty, error.Key);
        Assert.Equal(["A non-empty request body is required."], error.Value);
    }

    [Fact]
    public async Task ValidateAsync_RunsFluentValidation_ForTheWrapperAndTheNestedRequest()
    {
        // Arrange
        var parameters = new FormParameters("s", new ShipmentForm("x", new FormAddress("Main Street", "1000")));
        var httpContext = CreateHttpContext(services => services
            .AddSingleton<IValidator<FormParameters>, FormParametersValidator>()
            .AddSingleton<IValidator<ShipmentForm>, ShipmentFormValidator>());

        // Act
        var result = await RequestValidation.ValidateAsync(parameters, httpContext);

        // Assert - the wrapper's validator and the form model's own validator both run
        var problem = Assert.IsType<ValidationProblem>(result);
        problem.ProblemDetails.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["shipmentId"] = ["The shipment id is too short."],
            ["label"] = ["The label is too short."],
        });
    }

    [Fact]
    public async Task ValidateAsync_NotAParametersWrapper_ValidatesTheValueAsTheBody()
    {
        // Act
        var result = await RequestValidation.ValidateAsync(
            new ShipmentForm(null, new FormAddress(null, "1000")),
            CreateHttpContext(),
            isParametersWrapper: false);

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result);
        problem.ProblemDetails.Errors.Keys.Should().BeEquivalentTo("address.street", "label");
    }

    [Fact]
    public async Task ValidateAsync_MaxDepth_TooDeep_IsTheEmptyKey()
    {
        // Arrange
        var parameters = new FormParameters("s-1", new ShipmentForm("parcel", new FormAddress(null, "1000")));
        var httpContext = CreateHttpContext(services => services
            .AddSingleton(new ValidationFilterOptions { MaxValidationDepth = 0 }));

        // Act
        var result = await RequestValidation.ValidateAsync(parameters, httpContext);

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(string.Empty, Assert.Single(problem.ProblemDetails.Errors).Key);
    }

    [Theory]
    [InlineData(ValidationKeyStyle.WireNames, false)]
    [InlineData(ValidationKeyStyle.MemberPath, false)]
    [InlineData(ValidationKeyStyle.MemberPath, true)]
    public async Task ValidateAsync_GivesTheSameErrorsAsTheFilterForAFromBodyMember(
        ValidationKeyStyle keyStyle,
        bool skipFirstLevel)
    {
        // Arrange - the same invalid values: a form record (no binding attribute) and a JSON-body record ([FromBody])
        var form = new ShipmentForm("x", new FormAddress(null, "1000"));
        var options = new ValidationFilterOptions { KeyStyle = keyStyle, SkipFirstLevelOnValidationKeys = skipFirstLevel };
        var httpContext = CreateHttpContext(services => services
            .AddSingleton(options)
            .AddSingleton<IValidator<ShipmentForm>, ShipmentFormValidator>());

        var filter = new ValidationFilter<BodyParameters>(options);
        var filterResult = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext, new BodyParameters(null, form)),
            _ => ValueTask.FromResult<object?>(null));

        // Act
        var result = await RequestValidation.ValidateAsync(new FormParameters(null, form), httpContext);

        // Assert
        var expected = Assert.IsType<ValidationProblem>(filterResult).ProblemDetails.Errors;
        var problem = Assert.IsType<ValidationProblem>(result);
        problem.ProblemDetails.Errors.Should().BeEquivalentTo(expected);
    }

    private static DefaultHttpContext CreateHttpContext(
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Request = { ContentType = "multipart/form-data; boundary=x" },
        };
    }
}