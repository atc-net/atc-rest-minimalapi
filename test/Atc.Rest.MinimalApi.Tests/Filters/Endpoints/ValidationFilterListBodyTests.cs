namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints;

/// <summary>
/// A body that is a list of a model: each item is validated by the model's FluentValidation validator - what a domain
/// registers - as it is by its DataAnnotations, and the errors of both are keyed alike (<c>[0].name</c>). A validator
/// for the list itself (its own type or a base type such as <see cref="List{T}"/>) runs instead, keyed the same way.
/// </summary>
public sealed class ValidationFilterListBodyTests
{
    private const string TakenMessage = "That name is taken.";

    [Fact]
    public async Task ItemValidator_FromBodyList_RunsForEachItem()
    {
        // Arrange - [FromBody] IList<SetRequestLine>, and a validator for the item only
        var parameters = new LinesParameters([new SetRequestLine("bad"), new SetRequestLine("ok"), new SetRequestLine("bad")]);

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null, LineValidator());

        // Assert
        errors.Keys.Should().BeEquivalentTo("[0].sku", "[2].sku");
        errors["[0].sku"].Should().Equal("That SKU is retired.");
    }

    [Fact]
    public async Task ItemValidator_BindAsyncListBody_RunsForEachItem_MergedWithTheDataAnnotations()
    {
        // Arrange - the generator's list body: a List<T> subclass that binds itself; item 0 breaks the validator's rule,
        // item 1 the model's [Required]
        var parameters = new BindAsyncListBodyParameters([new BindAsyncPet("Rex"), new BindAsyncPet(Name: null)]);

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null, PetValidator());

        // Assert
        errors.Keys.Should().BeEquivalentTo("[0].name", "[1].name");
        errors["[0].name"].Should().Equal(TakenMessage);
        errors["[1].name"].Should().ContainSingle().Which.Should().Contain("required");
    }

    [Fact]
    public async Task ItemValidator_SameItemFieldFromBoth_KeepsBothMessages_TheDataAnnotationsFirst()
    {
        // Arrange - MaxLength(10) and the validator's rule on the same name
        var parameters = new BindAsyncListBodyParameters([new BindAsyncPet("Rex-the-eleventh")]);
        var validator = new InlineValidator<BindAsyncPet>();
        validator.RuleFor(x => x.Name).Must(n => n is null || !n.Contains('-', StringComparison.Ordinal)).WithMessage("No dashes.");

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null, validator);

        // Assert
        errors.Keys.Should().BeEquivalentTo("[0].name");
        errors["[0].name"].Should().HaveCount(2);
        errors["[0].name"][0].Should().Contain("maximum length");
        errors["[0].name"][1].Should().Be("No dashes.");
    }

    [Fact]
    public async Task ListValidator_RegisteredForABaseTypeOfTheBody_RunsAndIsKeyedByTheWireNames()
    {
        // Arrange - the body is BindAsyncList<BindAsyncPet>; the validator is written for List<BindAsyncPet>
        var parameters = new BindAsyncListBodyParameters([new BindAsyncPet("Ada"), new BindAsyncPet("Rex")]);
        var validator = new InlineValidator<List<BindAsyncPet>>();
        validator.RuleForEach(x => x).SetValidator(PetValidator());

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null, validator);

        // Assert - not "x[1].Name": the lambda's name is no part of the key
        errors.Keys.Should().BeEquivalentTo("[1].name");
        errors["[1].name"].Should().Equal(TakenMessage);
    }

    [Fact]
    public async Task ListValidator_RegisteredForTheBodysOwnType_IsKeyedByTheWireNames()
    {
        // Arrange
        var parameters = new BindAsyncListBodyParameters([new BindAsyncPet("Rex")]);
        var validator = new InlineValidator<BindAsyncList<BindAsyncPet>>();
        validator.RuleForEach(x => x).SetValidator(PetValidator());

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null, validator);

        // Assert
        errors.Keys.Should().BeEquivalentTo("[0].name");
    }

    [Fact]
    public async Task ListValidatorAndItemValidator_BothRegistered_TheListValidatorDecides_NoDuplicateMessages()
    {
        // Arrange - a list validator that applies the item's validator, and the item's validator registered too
        var parameters = new BindAsyncListBodyParameters([new BindAsyncPet("Rex")]);
        var listValidator = new InlineValidator<List<BindAsyncPet>>();
        listValidator.RuleForEach(x => x).SetValidator(PetValidator());

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options: null, listValidator, PetValidator());

        // Assert
        errors.Keys.Should().BeEquivalentTo("[0].name");
        errors["[0].name"].Should().Equal(TakenMessage);
    }

    [Fact]
    public async Task ItemValidator_ValidItems_CallsNext()
    {
        // Arrange
        var parameters = new BindAsyncListBodyParameters([new BindAsyncPet("Ada"), new BindAsyncPet("Grace")]);

        // Act
        var result = await InvokeAsync(parameters, options: null, PetValidator());

        // Assert
        result.Should().Be("Success");
    }

    [Fact]
    public async Task ItemValidator_MemberPathKeyStyle_UsesTheCSharpPath()
    {
        // Arrange - the 2.x keys: the body member's name, the index, the member's (JSON) name
        var parameters = new BindAsyncListBodyParameters([new BindAsyncPet("Rex")]);
        var options = new ValidationFilterOptions { KeyStyle = ValidationKeyStyle.MemberPath };

        // Act
        var errors = await GetValidationErrorsAsync(parameters, options, PetValidator());

        // Assert
        errors.Keys.Should().BeEquivalentTo("Request[0].Name");
    }

    private static InlineValidator<SetRequestLine> LineValidator()
    {
        var validator = new InlineValidator<SetRequestLine>();
        validator.RuleFor(x => x.Sku).NotEqual("bad").WithMessage("That SKU is retired.");
        return validator;
    }

    private static InlineValidator<BindAsyncPet> PetValidator()
    {
        var validator = new InlineValidator<BindAsyncPet>();
        validator.RuleFor(x => x.Name)
            .MustAsync(async (name, cancellationToken) =>
            {
                await Task.Yield();
                return name != "Rex";
            })
            .WithMessage(TakenMessage);
        return validator;
    }

    private static async Task<IDictionary<string, string[]>> GetValidationErrorsAsync<T>(
        T argument,
        ValidationFilterOptions? options,
        params IValidator[] validators)
        where T : class
    {
        var result = await InvokeAsync(argument, options, validators);
        return result.Should().BeOfType<ValidationProblem>().Subject.ProblemDetails.Errors;
    }

    private static async Task<object?> InvokeAsync<T>(
        T argument,
        ValidationFilterOptions? options,
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
        var context = new DefaultEndpointFilterInvocationContext(httpContext, argument);
        var filter = new ValidationFilter<T>(options);

        return await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("Success"));
    }
}