namespace Demo.Domain.Tests.Validators;

public sealed class CreateUserRequestValidatorTests
{
    [Fact]
    public void Validate_MissingRequiredValues_LeavesTheRequiredCheckToDataAnnotations()
    {
        // Arrange - Gender, FirstName and LastName are [Required] on CreateUserRequest (DataAnnotations).
        // A second "required" rule here would give every missing value two messages under the same key.
        var validator = new CreateUserRequestValidator();
        var request = new CreateUserRequest(
            Gender: null,
            FirstName: null!,
            LastName: null!,
            Email: "ada@example.com",
            Telephone: null,
            HomePage: null,
            HomeAddress: null,
            WorkAddress: null);

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.DoesNotContain(
            result.Errors,
            e => e.PropertyName is nameof(CreateUserRequest.Gender) or nameof(CreateUserRequest.FirstName) or nameof(CreateUserRequest.LastName));
    }

    [Fact]
    public void Validate_GenderNone_IsRejected()
    {
        // Arrange - the business rule stays: a gender must be specified
        var validator = new CreateUserRequestValidator();
        var request = new CreateUserRequest(GenderType.None, "Ada", "Tester", "ada@example.com", null, null, null, null);

        // Act
        var result = validator.Validate(request);

        // Assert
        var error = Assert.Single(result.Errors);
        Assert.Equal("Gender must be specified (not None).", error.ErrorMessage);
    }
}