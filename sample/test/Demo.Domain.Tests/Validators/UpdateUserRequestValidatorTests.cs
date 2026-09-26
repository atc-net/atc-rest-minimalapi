namespace Demo.Domain.Tests.Validators;

public sealed class UpdateUserRequestValidatorTests
{
    [Fact]
    public void Validate_FirstNameTooShort_MessageUsesTheFieldDisplayName()
    {
        // Arrange - the validator targets the request model (the [FromBody] type), not the parameters record,
        // so FluentValidation's display name is "First Name" instead of "Request First Name".
        var validator = new UpdateUserRequestValidator();
        var request = new UpdateUserRequest(Gender: null, FirstName: "A", LastName: null, Email: null, WorkAddress: null);

        // Act
        var result = validator.Validate(request);

        // Assert
        var error = Assert.Single(result.Errors, e => e.PropertyName == nameof(UpdateUserRequest.FirstName) && e.ErrorCode == "LengthValidator");
        Assert.Equal("'First Name' must be between 2 and 10 characters. You entered 1 characters.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_PartialUpdateWithoutValues_IsValid()
    {
        // Arrange - partial update: fields are only validated when provided
        var validator = new UpdateUserRequestValidator();
        var request = new UpdateUserRequest(Gender: null, FirstName: null, LastName: null, Email: null, WorkAddress: null);

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }
}