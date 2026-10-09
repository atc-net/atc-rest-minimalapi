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
    public async Task Root_PropertyError_ValidateIsSkipped()
    {
        // Act - Name fails MaxLength(3), and Min > Max would fail Validate
        var response = await fixture.Default.PostJsonAsync("/validatable", """{"name":"abcd","min":5,"max":1}""");

        // Assert - as .NET's Validator and Microsoft.Extensions.Validation: Validate runs only when the members passed
        response.ShouldBeValidationProblem(
            ("name", "The field Name must be a string or array type with a maximum length of '3'."));
    }

    [Fact]
    public async Task NestedPropertyError_TheParentsValidateIsSkipped()
    {
        // Act - the child's Code fails MaxLength(3); the parent's own members pass, but Min > Max would fail its Validate
        var response = await fixture.Default.PostJsonAsync("/validatable", """{"min":5,"max":1,"child":{"code":"abcd"}}""");

        // Assert - as Microsoft.Extensions.Validation (.NET 10): an error anywhere below an object skips its Validate
        response.ShouldBeValidationProblem(
            ("child.code", "The field Code must be a string or array type with a maximum length of '3'."));
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
    public async Task Nested_PropertyError_ItsValidateIsSkipped()
    {
        // Act - Code fails MaxLength(3), and starting with "bad" would fail the child's Validate
        var response = await fixture.Default.PostJsonAsync("/validatable", """{"child":{"code":"badxx"}}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("child.code", "The field Code must be a string or array type with a maximum length of '3'."));
    }

    [Fact]
    public async Task TypeLevelAttribute_PropertyError_IsSkipped()
    {
        // Act - Name fails MaxLength(3), and Min > Max would fail the type-level attribute
        var response = await fixture.Default.PostJsonAsync("/type-level", """{"name":"abcd","min":5,"max":1}""");

        // Assert - as Microsoft.Extensions.Validation: type-level attributes run only when the members passed
        response.ShouldBeValidationProblem(
            ("name", "The field Name must be a string or array type with a maximum length of '3'."));
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