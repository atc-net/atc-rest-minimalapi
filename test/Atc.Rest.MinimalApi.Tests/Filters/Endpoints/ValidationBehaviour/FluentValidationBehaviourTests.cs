namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

/// <summary>FluentValidation next to DataAnnotations: validators for the body, for a nested type and for the parameters record.</summary>
[Collection(ValidationBehaviourGroup.Name)]
public sealed class FluentValidationBehaviourTests(ValidationBehaviourFixture fixture)
{
    private const string NameTooLong = "The field Name must be a string or array type with a maximum length of '5'.";

    [Fact]
    public async Task FluentValidationOnly_TwoRulesOnOneProperty()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/fluent", """{"name":"taken"}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("name", "Name is taken."),
            ("name", "Name starts with taken."));
    }

    [Fact]
    public async Task BothEngines_SameProperty_MessagesAreMergedUnderOneKey()
    {
        // Act - MaxLength(5) and the FluentValidation rule both fail
        var response = await fixture.Default.PostJsonAsync("/fluent", """{"name":"taken-too-long"}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("name", NameTooLong),
            ("name", "Name starts with taken."));
    }

    [Fact]
    public async Task CollectionOfPrimitives_ItemRule_KeepsTheIndex()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/fluent", """{"tags":["ok","too-long"]}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("tags[1]", "The length of 'Tags' must be 3 characters or fewer. You entered 8 characters."));
    }

    [Fact]
    public async Task ValidatorForANestedType_IsNotDiscovered()
    {
        // Act - FluentChildValidator (Code NotEmpty) is registered, but nothing calls SetValidator for Child
        var response = await fixture.Default.PostJsonAsync("/fluent", """{"child":{"child_code":""}}""");

        // Assert
        response.ShouldBeOk();
    }

    [Fact]
    public async Task ValidatorForTheParametersRecord_RunsNextToTheBodysValidator()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/fluent", """{"name":"wrapper"}""");

        // Assert - the wrapper's Request.Name maps to the body-relative key
        response.ShouldBeValidationProblem(
            ("name", NameTooLong),
            ("name", "Rejected by the parameters validator."));
    }

    [Theory]
    [InlineData(false, """{"child":{"child_code":"nested"}}""", "child.child_code", "The nested code is rejected.")]
    [InlineData(true, """{"child":{"child_code":"nested"}}""", "Request.Child.child_code", "The nested code is rejected.")]
    [InlineData(false, """{"name":"alias"}""", "Alias", "Rejected under an alias.")]
    [InlineData(true, """{"name":"alias"}""", "Request.Alias", "Rejected under an alias.")]
    [InlineData(false, """{"name":"dots"}""", "Extra.Info", "Rejected under a dotted alias.")]
    [InlineData(true, """{"name":"dots"}""", "Request.Extra.Info", "Rejected under a dotted alias.")]
    public async Task BodyValidator_NestedRuleAndOverriddenNames(
        bool memberPath,
        string json,
        string expectedKey,
        string expectedMessage)
    {
        // Act - a nested member with [JsonPropertyName], and OverridePropertyName names that are not members (kept as is)
        var response = await (memberPath ? fixture.MemberPath : fixture.Default).PostJsonAsync("/fluent", json);

        // Assert
        response.ShouldBeValidationProblem((expectedKey, expectedMessage));
    }

    [Theory]
    [InlineData(false, "country_code")]
    [InlineData(true, "Request.country_code")]
    public async Task BodyValidator_JsonPropertyName(
        bool memberPath,
        string expectedKey)
    {
        // Act
        var response = await (memberPath ? fixture.MemberPath : fixture.Default).PostJsonAsync("/named", """{"country_code":"xxx"}""");

        // Assert
        response.ShouldBeValidationProblem((expectedKey, "The country code xxx is reserved."));
    }

    [Fact]
    public async Task MemberPath_BothEngines()
    {
        // Act
        var response = await fixture.MemberPath.PostJsonAsync("/fluent", """{"name":"taken-too-long"}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("Request.Name", NameTooLong),
            ("Request.Name", "Name starts with taken."));
    }
}