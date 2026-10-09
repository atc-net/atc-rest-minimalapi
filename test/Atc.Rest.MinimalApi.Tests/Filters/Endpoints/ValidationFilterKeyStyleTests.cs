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

    [Fact]
    public async Task WireNames_FromBodyCollection_KeepsTheRowIndex()
    {
        // Arrange - the generator's shape for an array body: [FromBody] List<T> Request; rows 0 and 2 are invalid
        var parameters = new LinesParameters([new SetRequestLine(Sku: null), new SetRequestLine("ok"), new SetRequestLine(Sku: null)]);

        var validator = new InlineValidator<LinesParameters>();
        validator.RuleForEach(x => x.Request).ChildRules(line => line.RuleFor(l => l.Sku).NotEmpty());

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null, validator);

        // Assert - the index relative to the body, as ASP.NET Core MVC reports it ([0].Sku): rows stay apart
        errors.Keys.Should().BeEquivalentTo("[0].sku", "[2].sku");
    }

    [Fact]
    public async Task WireNames_FromBodyMissing_FluentValidationMessageNamesNoMember()
    {
        // Arrange - a wrapper rule on the body member itself; the body is missing
        var validator = new InlineValidator<LinesParameters>();
        validator.RuleFor(x => x.Request).NotNull();

        // Act
        var errors = await GetValidationErrorsAsync(new LinesParameters(Request: null!), options: null, validator);

        // Assert
        errors.Keys.Should().BeEquivalentTo(string.Empty);
        errors[string.Empty].Should().OnlyContain(m => m == "A non-empty request body is required.");
    }

    [Fact]
    public async Task WireNames_BindAsyncBody_WithoutHandlerMetadata_DropsTheMemberSegment()
    {
        // Arrange - no endpoint metadata: the record is recognised as a parameters wrapper by its BindAsync body alone
        var parameters = new BindAsyncBodyParameters(new BindAsyncPet(Name: "a name that is much too long"));

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null);

        // Assert
        errors.Keys.Should().BeEquivalentTo("name");
    }

    [Fact]
    public async Task WireNames_BindAsyncBodyMissing_IsReportedUnderTheEmptyKey()
    {
        // Act
        var errors = await GetValidationErrorsAsync(new BindAsyncBodyParameters(Request: null!), options: null);

        // Assert - as for a missing [FromBody] member
        errors.Keys.Should().BeEquivalentTo(string.Empty);
        errors[string.Empty].Should().OnlyContain(m => m == "A non-empty request body is required.");
    }

    [Fact]
    public async Task WireNames_FromQueryMemberWhoseTypeHasBindAsync_IsNotTheBody()
    {
        // Arrange - an explicit binding source wins over the BindAsync rule
        var parameters = new BindAsyncFromQueryParameters(Filter: new BindAsyncPet(Name: "a name that is much too long"));

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null);

        // Assert - the query binding name stays in the key
        errors.Keys.Should().BeEquivalentTo("filter.name");
    }

    [Theory]
    [InlineData("application/xml", "pet-id")]
    [InlineData("application/json", "petId")]
    public async Task WireNames_BindAsyncBody_XmlAttributeName_IsUsedForAnXmlRequest(
        string contentType,
        string expectedKey)
    {
        // Arrange - XmlAttributePet.PetId has [XmlAttribute("pet-id")] and Range(1, 10)
        var parameters = new XmlAttributeBodyParameters(new XmlAttributePet(PetId: 0));

        // Act
        var errors = await GetValidationErrorsAsync(parameters, contentType);

        // Assert
        errors.Keys.Should().BeEquivalentTo(expectedKey);
    }

    [Fact]
    public async Task WireNames_SingleArgumentBindAsyncBody_IsTheBody()
    {
        // Arrange - BindAsync(HttpContext), without the ParameterInfo argument
        var parameters = new SingleArgumentBindAsyncParameters(new SingleArgumentBindAsyncPet(Name: "a name that is much too long"));

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null);

        // Assert
        errors.Keys.Should().BeEquivalentTo("name");
    }

    [Fact]
    public async Task WireNames_BindableFromHttpContextBody_IsTheBody()
    {
        // Arrange - IBindableFromHttpContext<T> implemented explicitly: no public BindAsync on the type
        var parameters = new BindableBodyParameters(new BindablePet(Name: "a name that is much too long"));

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null);

        // Assert
        errors.Keys.Should().BeEquivalentTo("name");
    }

    [Fact]
    public async Task WireNames_EndpointWithoutHandlerMetadata_RecognisesTheWrapperByItsMembers()
    {
        // Arrange - an endpoint whose metadata has no handler MethodInfo
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        httpContext.SetEndpoint(new Endpoint(requestDelegate: null, new EndpointMetadataCollection(), "no handler metadata"));
        var context = new DefaultEndpointFilterInvocationContext(httpContext, InvalidGeneratorShapedParameters);

        // Act
        var result = await new ValidationFilter<SetParameters>().InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));

        // Assert
        result.Should().BeOfType<ValidationProblem>().Subject.ProblemDetails.Errors.Keys
            .Should().BeEquivalentTo("page-size", "reason", "lines[2].sku");
    }

    [Fact]
    public async Task WireNames_HandlerWithoutAParameterOfT_RecognisesTheWrapperByItsMembers()
    {
        // Arrange - handler metadata that does not declare the validated type
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        var handler = typeof(ValidationFilterKeyStyleTests).GetMethod(nameof(HandlerWithoutParameters), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        httpContext.SetEndpoint(new Endpoint(requestDelegate: null, new EndpointMetadataCollection(handler), "other handler"));
        var context = new DefaultEndpointFilterInvocationContext(httpContext, InvalidGeneratorShapedParameters);

        // Act
        var result = await new ValidationFilter<SetParameters>().InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));

        // Assert
        result.Should().BeOfType<ValidationProblem>().Subject.ProblemDetails.Errors.Keys
            .Should().BeEquivalentTo("page-size", "reason", "lines[2].sku");
    }

    [Fact]
    public async Task MaxValidationDepth_IsConfigurable()
    {
        // Arrange - SetParameters (depth 0) -> Request (1) -> Lines and their rows (2); a collection's items share its depth

        // Act
        var withinLimit = await GetValidationErrorsAsync(InvalidGeneratorShapedParameters, new ValidationFilterOptions { MaxValidationDepth = 1 });
        var beyondLimit = await GetValidationErrorsAsync(InvalidGeneratorShapedParameters, new ValidationFilterOptions { MaxValidationDepth = 0 });

        // Assert - the rows are not reached with 0: the request is rejected as a whole instead of passing them
        withinLimit.Keys.Should().BeEquivalentTo("page-size", "reason", "lines[2].sku");
        beyondLimit.Keys.Should().BeEquivalentTo("page-size", "reason", string.Empty);
        beyondLimit[string.Empty].Should().Equal("The request body is nested too deeply to validate (more than 0 levels).");
    }

    [Fact]
    public void MaxValidationDepth_DefaultsTo32_AndRejectsANegativeValue()
    {
        // Act
        var act = () => new ValidationFilterOptions { MaxValidationDepth = -1 };

        // Assert
        new ValidationFilterOptions().MaxValidationDepth.Should().Be(32);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task WireNames_DictionaryEntry_KeepsTheValidatorsPathFromTheEntry()
    {
        // Arrange - MiniValidation reports a dictionary entry by position (map[0].Value), not by its key
        var request = new MapRequest(new Dictionary<string, SetRequestLine>(StringComparer.Ordinal) { ["a"] = new(Sku: null) });

        // Act
        var errors = await GetValidationErrorsAsync(request, options: null);

        // Assert - the dictionary member is mapped; from the entry on, the segments are kept as they are
        errors.Keys.Should().BeEquivalentTo("map[0].Value.Sku");
    }

    private static void HandlerWithoutParameters()
    {
        // Only its MethodInfo is used: endpoint metadata of a handler without a SetParameters parameter.
    }

    private static Task<IDictionary<string, string[]>> GetValidationErrorsAsync<T>(
        T argument,
        ValidationFilterOptions? options,
        params IValidator[] validators)
        where T : class
        => GetValidationErrorsAsync(argument, options, configureServices: null, validators);

    private static Task<IDictionary<string, string[]>> GetValidationErrorsAsync<T>(
        T argument,
        string contentType)
        where T : class
        => GetValidationErrorsAsync(argument, options: null, configureServices: null, contentType, validators: []);

    private static Task<IDictionary<string, string[]>> GetValidationErrorsAsync<T>(
        T argument,
        ValidationFilterOptions? options,
        Action<IServiceCollection>? configureServices,
        params IValidator[] validators)
        where T : class
        => GetValidationErrorsAsync(argument, options, configureServices, contentType: null, validators);

    private static async Task<IDictionary<string, string[]>> GetValidationErrorsAsync<T>(
        T argument,
        ValidationFilterOptions? options,
        Action<IServiceCollection>? configureServices,
        string? contentType,
        IValidator[] validators)
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
        httpContext.Request.ContentType = contentType;
        var context = new DefaultEndpointFilterInvocationContext(httpContext, argument);
        var filter = new ValidationFilter<T>(options);

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));

        return result.Should().BeOfType<ValidationProblem>().Subject.ProblemDetails.Errors;
    }
}