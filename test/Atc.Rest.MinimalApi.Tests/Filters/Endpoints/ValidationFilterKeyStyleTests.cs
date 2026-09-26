namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints;

/// <summary>
/// The keys of the ValidationProblem "errors" object: the wire names the client sent (default),
/// or the C# member paths of 2.x (opt-in).
/// </summary>
public sealed class ValidationFilterKeyStyleTests
{
    private static readonly SetParameters InvalidGeneratorShapedParameters = new(
        Mode: "x",
        PageSize: 500, // Range(1, 100)
        Request: new SetRequest(
            Reason: "far-too-long", // MaxLength(5)
            FirstName: string.Empty, // FluentValidation: NotEmpty
            Lines: [new SetRequestLine("ok"), new SetRequestLine("ok"), new SetRequestLine(Sku: null)])); // Required

    [Fact]
    public async Task MemberPath_GeneratorShapedParameters_UsesCSharpMemberPaths()
    {
        // Arrange - the 2.x keys (also what .NET 10's built-in validation produces)
        var options = new ValidationFilterOptions { KeyStyle = ValidationKeyStyle.MemberPath };

        // Act
        var errors = await GetValidationErrorsAsync(InvalidGeneratorShapedParameters, options, validators: new SetRequestValidator());

        // Assert
        errors.Keys.Should().BeEquivalentTo("PageSize", "Request.Reason", "Request.Lines[2].Sku", "Request.FirstName");
    }

    [Fact]
    public async Task WireNames_IsTheDefault_GeneratorShapedParameters_UsesTheNamesTheClientSent()
    {
        // Act - no options: the default key style
        var errors = await GetValidationErrorsAsync(InvalidGeneratorShapedParameters, options: null, validators: new SetRequestValidator());

        // Assert - query/route: the binding name; body: the JSON path relative to the body (camelCase by default)
        errors.Keys.Should().BeEquivalentTo("page-size", "reason", "lines[2].sku", "firstName");
    }

    [Fact]
    public async Task WireNames_MessagesAreNotRewritten()
    {
        // Act
        var errors = await GetValidationErrorsAsync(InvalidGeneratorShapedParameters, options: null, validators: new SetRequestValidator());

        // Assert - only the keys change; messages keep the validators' own text
        errors["reason"].Should().ContainSingle()
            .Which.Should().Be("The field Reason must be a string or array type with a maximum length of '5'.");
        errors["firstName"].Should().ContainSingle()
            .Which.Should().Be("'First Name' must not be empty.");
    }

    [Fact]
    public async Task WireNames_ValidatedTypeIsTheBodyItself_MapsEverySegment()
    {
        // Arrange - ValidationFilter<SetRequest> on a [FromBody] SetRequest parameter (no parameters wrapper)
        var body = InvalidGeneratorShapedParameters.Request;

        // Act
        var errors = await GetValidationErrorsAsync(body, options: null, validators: new SetRequestValidator());

        // Assert
        errors.Keys.Should().BeEquivalentTo("reason", "lines[2].sku", "firstName");
    }

    [Fact]
    public async Task WireNames_RouteAndBodyMapToTheSameKey_ErrorsAreMerged()
    {
        // Arrange - route "id" is out of range and body "id" is missing
        var parameters = new CollisionParameters(Id: 0, Request: new CollisionRequest(Id: null));

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null);

        // Assert
        errors.Keys.Should().BeEquivalentTo("id");
        errors["id"].Should().HaveCount(2);
    }

    [Fact]
    public async Task WireNames_BindingAttributesOnConstructorParametersOnly_AreHonored()
    {
        // Arrange - [FromQuery(Name = ...)] / [FromBody] without the property: target
        var parameters = new ParameterAttributeOnlyParameters(
            PageSize: 500,
            Request: new SetRequest(Reason: "far-too-long", FirstName: "Bob", Lines: []));

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null, validators: new ParameterAttributeOnlyParametersValidator());

        // Assert
        errors.Keys.Should().BeEquivalentTo("page-size", "reason");
    }

    [Fact]
    public async Task WireNames_UsesTheAppNamingPolicy()
    {
        // Arrange - the app serializes JSON with snake_case
        void ConfigureServices(IServiceCollection services)
            => services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
                o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        // Act
        var errors = await GetValidationErrorsAsync(InvalidGeneratorShapedParameters, options: null, ConfigureServices, new SetRequestValidator());

        // Assert - body keys follow the policy; the query key is the binding name as-is
        errors.Keys.Should().BeEquivalentTo("page-size", "reason", "lines[2].sku", "first_name");
    }

    [Fact]
    public async Task WireNames_JsonPropertyNameOverridesTheNamingPolicy()
    {
        // Arrange - AddressWithJsonPropertyNames.CountryCodeA3 has [JsonPropertyName("country_code_a3")] and MinLength(3)
        var body = new CreateLocationRequestWithJsonPropertyNames(
            new AddressWithJsonPropertyNames("DK", "Place", "Street", "PostalCode", "City"),
            "12345678");

        // Act
        var errors = await GetValidationErrorsAsync(body, options: null);

        // Assert
        errors.Keys.Should().Contain("address.country_code_a3");
        errors["address.country_code_a3"].Should().OnlyContain(m => m.Contains("CountryCodeA3", StringComparison.Ordinal));
    }

    private static Task<IDictionary<string, string[]>> GetValidationErrorsAsync<T>(
        T argument,
        ValidationFilterOptions? options,
        params IValidator[] validators)
        where T : class
        => GetValidationErrorsAsync(argument, options, configureServices: null, validators);

    private static async Task<IDictionary<string, string[]>> GetValidationErrorsAsync<T>(
        T argument,
        ValidationFilterOptions? options,
        Action<IServiceCollection>? configureServices,
        params IValidator[] validators)
        where T : class
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);

        foreach (var validator in validators)
        {
            foreach (var validatorInterface in validator.GetType().GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            {
                services.AddSingleton(validatorInterface, validator);
            }
        }

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var context = new DefaultEndpointFilterInvocationContext(httpContext, argument);
        var filter = new ValidationFilter<T>(options);

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));

        return result.Should().BeOfType<ValidationProblem>().Subject.ProblemDetails.Errors;
    }
}