namespace Atc.Rest.MinimalApi.Tests.Extensions;

public sealed class ValidationProblemExtensionsTests
{
    [Fact]
    public async Task Should_Not_Throw_NullReferenceException_For_Incorrect_Type_Specified()
    {
        // Arrange
        var fluentValidator = new CreateLocationRequestWithoutJsonPropertyNamesValidator();

        var elementToValidate = new CreateLocationRequestWithoutJsonPropertyNames(
            new AddressWithoutJsonPropertyNames("DK", "Place", "Street", "PostalCode", "City"),
            "Telephone");

        var (_, miniValidatorErrors) = await MiniValidator.TryValidateAsync(elementToValidate);
        var fluentValidatorResults = await fluentValidator.ValidateAsync(elementToValidate);

        // Act
        var exception = await Record.ExceptionAsync(() =>
        {
            TypedResults
                .ValidationProblem(
                    miniValidatorErrors
                        .MergeErrors(fluentValidatorResults.ToDictionary()))
                .ResolveSerializationTypeNames<DummyTypeWithJsonPropertyName>();

            return Task.CompletedTask;
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Should_Validate_Correctly_With_Proper_Casing_For_Type_Without_JsonPropertyName_Attribute()
    {
        // Arrange
        var fluentValidator = new CreateLocationRequestWithoutJsonPropertyNamesValidator();

        var elementToValidate = new CreateLocationRequestWithoutJsonPropertyNames(
            new AddressWithoutJsonPropertyNames("DK", "Place", "Street", "PostalCode", "City"),
            "Telephone");

        var (_, miniValidatorErrors) = await MiniValidator.TryValidateAsync(elementToValidate);
        var fluentValidatorResults = await fluentValidator.TestValidateAsync(elementToValidate);

        // Act
        var problems = TypedResults
            .ValidationProblem(
                miniValidatorErrors
                    .MergeErrors(fluentValidatorResults.ToDictionary()))
            .ResolveSerializationTypeNames<CreateLocationRequestWithoutJsonPropertyNames>();

        // Assert
        using var scope = new AssertionScope();
        Assert.Single(problems.ProblemDetails.Errors);

        fluentValidatorResults.ShouldHaveValidationErrorFor("Address.CountryCodeA3");

        var (errorKey, errorValues) = problems.ProblemDetails.Errors.First();
        Assert.True(
            "Address.CountryCodeA3".Equals(errorKey, StringComparison.Ordinal),
            "Invalid validation key received.");

        Assert.True(
            errorValues.Length == 2,
            "Invalid number of errors received for validation key (expected 1 from DataAnnotations and 1 from FluentValidation).");

        Assert.True(
            errorValues.Contains("The field CountryCodeA3 must be a string or array type with a minimum length of '3'.", StringComparer.Ordinal),
            "Missing validation error from DataAnnotations.");

        Assert.True(
            errorValues.Contains("CountryCodeA3 must be 3 characters long.", StringComparer.Ordinal),
            "Missing validation error from FluentValidation.");
    }

    [Fact]
    public async Task Should_Validate_Correctly_With_Proper_Casing_For_Type_With_JsonPropertyName_Attribute()
    {
        // Arrange
        var fluentValidator = new CreateLocationRequestWithJsonPropertyNamesValidator();

        var elementToValidate = new CreateLocationRequestWithJsonPropertyNames(
            new AddressWithJsonPropertyNames("DK", "Place", "Street", "PostalCode", "City"),
            " ");

        var (_, miniValidatorErrors) = await MiniValidator.TryValidateAsync(elementToValidate);
        var fluentValidatorResults = await fluentValidator.TestValidateAsync(elementToValidate);

        // Act
        var problems = TypedResults
            .ValidationProblem(
                miniValidatorErrors
                    .MergeErrors(fluentValidatorResults.ToDictionary()))
            .ResolveSerializationTypeNames<CreateLocationRequestWithJsonPropertyNames>();

        // Assert
        using var scope = new AssertionScope();
        Assert.Equal(2, problems.ProblemDetails.Errors.Count);

        fluentValidatorResults.ShouldHaveValidationErrorFor("Address.CountryCodeA3");
        fluentValidatorResults.ShouldHaveValidationErrorFor("Telephone");

        var (errorKey, errorValues) = problems.ProblemDetails.Errors.First();
        Assert.True(
            "address.country_code_a3".Equals(errorKey, StringComparison.Ordinal),
            "Invalid validation key received.");

        Assert.True(
            errorValues.Length == 2,
            "Invalid number of errors received for validation key (expected 1 from DataAnnotations and 1 from FluentValidation).");

        Assert.True(
            errorValues.Contains("The field country_code_a3 must be a string or array type with a minimum length of '3'.", StringComparer.Ordinal),
            "Missing validation error from DataAnnotations.");

        Assert.True(
            errorValues.Contains("country_code_a3 must be 3 characters long.", StringComparer.Ordinal),
            "Missing validation error from FluentValidation.");

        var (errorKey2, errorValues2) = problems.ProblemDetails.Errors.Last();
        Assert.True(
            "telephone".Equals(errorKey2, StringComparison.Ordinal),
            "Invalid validation key received.");

        Assert.True(
            errorValues2.Contains("telephone is required.", StringComparer.Ordinal),
            "Missing validation error from FluentValidation.");
    }

    [Fact]
    public async Task Should_Validate_Correctly_With_Proper_Casing_For_Request_When_SkippingFirstLevelOnValidationKeys()
    {
        // Arrange
        var fluentValidator = new CreateLocationRequestWithRequestValidator();

        var elementToValidate = new CreateLocationRequestWithRequest(
            "LocationId",
            new CreateLocationRequestWithJsonPropertyNames(
                    new AddressWithJsonPropertyNames("DNK", "Place", "Street", "PostalCode", "C"),
                    "1"));

        var (_, miniValidatorErrors) = await MiniValidator.TryValidateAsync(elementToValidate);
        var fluentValidatorResults = await fluentValidator.TestValidateAsync(elementToValidate);

        // Act
        var problems = TypedResults
            .ValidationProblem(
                miniValidatorErrors
                    .MergeErrors(fluentValidatorResults.ToDictionary()))
            .ResolveSerializationTypeNames<CreateLocationRequestWithRequest>(skipFirstLevelOnValidationKeys: true);

        // Assert
        using var scope = new AssertionScope();
        Assert.Equal(2, problems.ProblemDetails.Errors.Count);

        var (errorKey1, errorValues1) = problems.ProblemDetails.Errors.First();
        Assert.True(
            "telephone".Equals(errorKey1, StringComparison.Ordinal),
            "Invalid validation key received.");

        Assert.True(
            errorValues1.Length == 1,
            "Invalid number of errors received for validation key.");

        Assert.True(
            errorValues1.Contains("'Telephone' must be between 2 and 10 characters. You entered 1 characters.", StringComparer.Ordinal),
            "Missing validation error from FluentValidation.");

        var (errorKey2, errorValues2) = problems.ProblemDetails.Errors.Last();
        Assert.True(
            "address.City".Equals(errorKey2, StringComparison.Ordinal),
            "Invalid validation key received.");

        Assert.True(
            errorValues2.Length == 1,
            "Invalid number of errors received for validation key.");

        Assert.True(
            errorValues2.Contains("CityName is too short.", StringComparer.Ordinal),
            "Missing validation error from FluentValidation.");
    }

    [Fact]
    public async Task Should_Validate_Correctly_With_Proper_Casing_For_Request_When_Not_SkippingFirstLevelOnValidationKeys()
    {
        // Arrange
        var fluentValidator = new CreateLocationRequestWithRequestValidator();

        var elementToValidate = new CreateLocationRequestWithRequest(
            "LocationId",
            new CreateLocationRequestWithJsonPropertyNames(
                    new AddressWithJsonPropertyNames("DNK", "Place", "Street", "PostalCode", "C"),
                    "1"));

        var (_, miniValidatorErrors) = await MiniValidator.TryValidateAsync(elementToValidate);
        var fluentValidatorResults = await fluentValidator.TestValidateAsync(elementToValidate);

        // Act
        var problems = TypedResults
            .ValidationProblem(
                miniValidatorErrors
                    .MergeErrors(fluentValidatorResults.ToDictionary()))
            .ResolveSerializationTypeNames<CreateLocationRequestWithRequest>();

        // Assert
        using var scope = new AssertionScope();
        Assert.Equal(2, problems.ProblemDetails.Errors.Count);

        var (errorKey1, errorValues1) = problems.ProblemDetails.Errors.First();
        Assert.True(
            "Request.telephone".Equals(errorKey1, StringComparison.Ordinal),
            "Invalid validation key received.");

        Assert.True(
            errorValues1.Length == 1,
            "Invalid number of errors received for validation key.");

        Assert.True(
            errorValues1.Contains("'Request telephone' must be between 2 and 10 characters. You entered 1 characters.", StringComparer.Ordinal),
            "Missing validation error from FluentValidation.");

        var (errorKey2, errorValues2) = problems.ProblemDetails.Errors.Last();
        Assert.True(
            "Request.address.City".Equals(errorKey2, StringComparison.Ordinal),
            "Invalid validation key received.");

        Assert.True(
            errorValues2.Length == 1,
            "Invalid number of errors received for validation key.");

        Assert.True(
            errorValues2.Contains("CityName is too short.", StringComparer.Ordinal),
            "Missing validation error from FluentValidation.");
    }
}