namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints;

/// <summary>
/// For an XML request, a body member's messages name the member as its key does - by the XML name the client sent -
/// not by the JSON name the message was built with. A JSON request keeps the messages as they are.
/// </summary>
public sealed class ValidationFilterXmlMessageTests
{
    [Fact]
    public async Task XmlRequest_MemberWithAnXmlName_MessageNamesTheXmlName()
    {
        // Act
        var errors = await GetValidationErrorsAsync(new XmlNamedPetParameters(new XmlNamedPet("Rexy", Code: null, Address: null)), "application/xml");

        // Assert
        errors.Keys.Should().Equal("alias");
        errors["alias"].Should().ContainSingle().Which.Should().Be("The field alias must be a string with a maximum length of 3.");
    }

    [Fact]
    public async Task JsonRequest_SameModel_MessageNamesTheJsonName()
    {
        // Act
        var errors = await GetValidationErrorsAsync(new XmlNamedPetParameters(new XmlNamedPet("Rexy", Code: null, Address: null)), "application/json");

        // Assert
        errors.Keys.Should().Equal("nickName");
        errors["nickName"].Should().ContainSingle().Which.Should().Be("The field nickName must be a string with a maximum length of 3.");
    }

    [Fact]
    public async Task XmlRequest_NestedMemberWithoutDisplay_MessageNamesTheXmlName()
    {
        // Act - the message is built with the C# member name (City)
        var errors = await GetValidationErrorsAsync(new XmlNamedPetParameters(new XmlNamedPet(NickName: null, Code: null, new XmlNamedAddress("Odense"))), "application/xml");

        // Assert
        errors.Keys.Should().Equal("address.town");
        errors["address.town"].Should().ContainSingle().Which.Should().Be("The field town must be a string with a maximum length of 3.");
    }

    [Fact]
    public async Task XmlRequest_XmlNameEqualsTheDisplayName_MessageIsKept()
    {
        // Act
        var errors = await GetValidationErrorsAsync(new XmlNamedPetParameters(new XmlNamedPet(NickName: null, Code: "ABC", Address: null)), "application/xml");

        // Assert
        errors["code"].Should().ContainSingle().Which.Should().Contain("The field code ");
    }

    [Fact]
    public async Task XmlRequest_FluentValidationMessageWithTheSplitName_NamesTheXmlName()
    {
        // Arrange - FluentValidation's default display name splits the member name into words ('Nick Name')
        var validator = new InlineValidator<XmlNamedPet>();
        validator.RuleFor(x => x.NickName).NotEqual("Rex");
        var parameters = new XmlNamedPetParameters(new XmlNamedPet("Rex", Code: null, Address: null));

        // Act
        var errors = await GetValidationErrorsAsync(parameters, "application/xml", validator);

        // Assert
        errors["alias"].Should().ContainSingle().Which.Should().NotContain("Nick Name").And.Contain("alias");
    }

    [Fact]
    public async Task XmlRequest_TheNameAsPartOfAWord_IsLeftAlone()
    {
        // Arrange - "code" in "barcode" is no mention of the member
        var validator = new InlineValidator<XmlNamedPet>();
        validator.RuleFor(x => x.NickName).NotEqual("Rex").WithMessage("Use the nickName from the barcode.");
        var parameters = new XmlNamedPetParameters(new XmlNamedPet("Rex", Code: null, Address: null));

        // Act
        var errors = await GetValidationErrorsAsync(parameters, "application/xml", validator);

        // Assert
        errors["alias"].Should().Equal("Use the alias from the barcode.");
    }

    private static async Task<IDictionary<string, string[]>> GetValidationErrorsAsync<T>(
        T argument,
        string contentType,
        params IValidator[] validators)
        where T : class
    {
        var services = new ServiceCollection();
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
        var filter = new ValidationFilter<T>();

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));
        return result.Should().BeOfType<ValidationProblem>().Subject.ProblemDetails.Errors;
    }
}