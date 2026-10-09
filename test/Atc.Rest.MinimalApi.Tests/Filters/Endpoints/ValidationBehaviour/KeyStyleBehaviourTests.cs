namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

/// <summary>The same requests under each key configuration: WireNames (default), MemberPath, MemberPath with
/// SkipFirstLevelOnValidationKeys, and a snake_case naming policy.</summary>
[Collection(ValidationBehaviourGroup.Name)]
public sealed class KeyStyleBehaviourTests(ValidationBehaviourFixture fixture)
{
    private const string IdRange = "The field Id must be between 1 and 100.";
    private const string PageSizeRange = "The field PageSize must be between 1 and 50.";
    private const string TenantTooLong = "The field Tenant must be a string or array type with a maximum length of '5'.";
    private const string LimitRange = "The field Limit must be between 1 and 10.";
    private const string FirstNameTooLong = "The field FirstName must be a string or array type with a maximum length of '5'.";
    private const string CountryCodeTooShort = "The field CountryCode must be a string or array type with a minimum length of '3'.";
    private const string CityNameTooLong = "The field CityName must be a string or array type with a maximum length of '3'.";

    [Fact]
    public async Task MemberPath_EverySource()
    {
        // Act
        var response = await PostInvalidSourcesAsync(fixture.MemberPath);

        // Assert
        response.ShouldBeValidationProblem(
            ("Id", IdRange),
            ("PageSize", PageSizeRange),
            ("Tenant", TenantTooLong),
            ("Limit", LimitRange),
            ("Request.FirstName", FirstNameTooLong));
    }

    [Fact]
    public async Task MemberPathSkipFirstLevel_EverySource()
    {
        // Act
        var response = await PostInvalidSourcesAsync(fixture.MemberPathSkipFirstLevel);

        // Assert
        response.ShouldBeValidationProblem(
            ("Id", IdRange),
            ("PageSize", PageSizeRange),
            ("Tenant", TenantTooLong),
            ("Limit", LimitRange),
            ("FirstName", FirstNameTooLong));
    }

    [Fact]
    public async Task SnakeCase_EverySource()
    {
        // Act
        var response = await PostInvalidSourcesAsync(fixture.SnakeCase);

        // Assert - binding names as declared; body members through the policy
        response.ShouldBeValidationProblem(
            ("id", IdRange),
            ("page-size", PageSizeRange),
            ("x-tenant", TenantTooLong),
            ("limit", LimitRange),
            ("first_name", FirstNameTooLong));
    }

    [Fact]
    public async Task WireNames_JsonPropertyNameWins_OthersFollowThePolicy()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/named", """{"country_code":"dk","cityName":"Aarhus"}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("country_code", CountryCodeTooShort),
            ("cityName", CityNameTooLong));
    }

    [Fact]
    public async Task SnakeCase_JsonPropertyNameWins_OthersFollowThePolicy()
    {
        // Act
        var response = await fixture.SnakeCase.PostJsonAsync("/named", """{"country_code":"dk","city_name":"Aarhus"}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("country_code", CountryCodeTooShort),
            ("city_name", CityNameTooLong));
    }

    [Fact]
    public async Task MemberPath_JsonPropertyName()
    {
        // Act
        var response = await fixture.MemberPath.PostJsonAsync("/named", """{"country_code":"dk","cityName":"Aarhus"}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("Request.country_code", "The field country_code must be a string or array type with a minimum length of '3'."),
            ("Request.CityName", CityNameTooLong));
    }

    [Fact]
    public async Task MemberPath_MissingBody_KeepsTheMemberAndItsMessage()
    {
        // Act
        var response = await ParameterSourceBehaviourTests.PostSourcesAsync(fixture.MemberPath, "/sources/1?page-size=1", "acme", body: null);

        // Assert
        response.ShouldBeValidationProblem(("Request", "The Request field is required."));
    }

    [Fact]
    public async Task MemberPath_ListBody()
    {
        // Act
        var response = await fixture.MemberPath.PostJsonAsync("/list", """[{"sku":"a"},{"sku":null}]""");

        // Assert
        response.ShouldBeValidationProblem(("Request[1].Sku", "The Sku field is required."));
    }

    private static Task<ValidationBehaviourResponse> PostInvalidSourcesAsync(
        HttpClient client)
        => ParameterSourceBehaviourTests.PostSourcesAsync(
            client,
            "/sources/0?page-size=100&limit=20",
            "a-tenant-too-long",
            """{"firstName":"too long","first_name":"too long"}""");
}