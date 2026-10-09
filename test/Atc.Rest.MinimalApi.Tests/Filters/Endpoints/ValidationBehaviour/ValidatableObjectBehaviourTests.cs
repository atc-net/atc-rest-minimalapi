namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

/// <summary><see cref="IValidatableObject"/> and type-level attributes.</summary>
[Collection(ValidationBehaviourGroup.Name)]
public sealed class ValidatableObjectBehaviourTests(ValidationBehaviourFixture fixture)
{
    [Fact]
    public async Task Root_ValidateNamesTwoMembers_BothKeysGetTheMessage()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/validatable", """{"min":5,"max":1}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("min", "Min must not be greater than Max."),
            ("max", "Min must not be greater than Max."));
    }

    [Fact]
    public async Task Root_ValidateWithoutMemberNames_IsReportedUnderTheEmptyKey()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/validatable", """{"name":"all"}""");

        // Assert
        response.ShouldBeValidationProblem(("", "The request as a whole is rejected."));
    }

    [Fact]
    public async Task Root_PropertyErrorAndValidateError_BothAreReported()
    {
        // Act - Name fails MaxLength(3), and Min > Max fails Validate
        var response = await fixture.Default.PostJsonAsync("/validatable", """{"name":"abcd","min":5,"max":1}""");

        // Assert - today Validate runs although a property failed
        response.ShouldBeValidationProblem(
            ("name", "The field Name must be a string or array type with a maximum length of '3'."),
            ("min", "Min must not be greater than Max."),
            ("max", "Min must not be greater than Max."));
    }

    [Fact]
    public async Task Nested_ValidateError_IsKeyedByThePath()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/validatable", """{"child":{"code":"bad"}}""");

        // Assert
        response.ShouldBeValidationProblem(("child.code", "Code is bad."));
    }

    [Fact]
    public async Task Nested_PropertyErrorAndValidateError_BothAreReported()
    {
        // Act - Code fails MaxLength(3), and starts with "bad"
        var response = await fixture.Default.PostJsonAsync("/validatable", """{"child":{"code":"badxx"}}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("child.code", "The field Code must be a string or array type with a maximum length of '3'."),
            ("child.code", "Code is bad."));
    }

    [Fact]
    public async Task TypeLevelAttribute_IsReportedUnderTheEmptyKey()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/type-level", """{"min":5,"max":1}""");

        // Assert
        response.ShouldBeValidationProblem(("", "Min must not be greater than Max."));
    }
}