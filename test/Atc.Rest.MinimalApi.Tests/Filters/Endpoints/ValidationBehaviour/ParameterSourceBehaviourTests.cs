namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

/// <summary>Route, query, header and body members of a generator-shaped parameters record, and the body shapes.</summary>
[Collection(ValidationBehaviourGroup.Name)]
public sealed class ParameterSourceBehaviourTests(ValidationBehaviourFixture fixture)
{
    private const string FirstNameTooLong = "The field FirstName must be a string or array type with a maximum length of '5'.";

    [Fact]
    public async Task EverySource_IsKeyedByItsBindingName()
    {
        // Act
        var response = await PostSourcesAsync(fixture.Default, "/sources/0?page-size=100&limit=20", "a-tenant-too-long", """{"firstName":"too long"}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("id", "The field Id must be between 1 and 100."),
            ("page-size", "The field PageSize must be between 1 and 50."),
            ("x-tenant", "The field Tenant must be a string or array type with a maximum length of '5'."),
            ("limit", "The field Limit must be between 1 and 10."),
            ("firstName", FirstNameTooLong));
    }

    [Fact]
    public async Task RequiredHeader_Missing()
    {
        // Act
        var response = await PostSourcesAsync(fixture.Default, "/sources/1?page-size=1", tenant: null, """{"firstName":"Ann"}""");

        // Assert
        response.ShouldBeValidationProblem(("x-tenant", "The Tenant field is required."));
    }

    [Fact]
    public async Task NullableValueTypeQuery_Invalid_IsValidated()
    {
        // Act - an optional int? query parameter (dotnet/aspnetcore#67033: .NET 10's validation skips these)
        var response = await PostSourcesAsync(fixture.Default, "/sources/1?page-size=1&limit=0", "acme", """{"firstName":"Ann"}""");

        // Assert
        response.ShouldBeValidationProblem(("limit", "The field Limit must be between 1 and 10."));
    }

    [Fact]
    public async Task NullableValueTypeQuery_Absent_IsValid()
    {
        // Act
        var response = await PostSourcesAsync(fixture.Default, "/sources/1?page-size=1", "acme", """{"firstName":"Ann"}""");

        // Assert
        response.ShouldBeOk();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    public async Task FromBody_Missing_IsReportedUnderTheEmptyKey(string? body)
    {
        // Act
        var response = await PostSourcesAsync(fixture.Default, "/sources/1?page-size=1", "acme", body);

        // Assert
        response.ShouldBeValidationProblem(("", "A non-empty request body is required."));
    }

    [Fact]
    public async Task ListBody_KeepsTheRowIndexRelativeToTheBody()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/list", """[{"sku":"a"},{"sku":null}]""");

        // Assert
        response.ShouldBeValidationProblem(("[1].sku", "The Sku field is required."));
    }

    [Theory]
    [InlineData("application/json", """{"name":"a name that is much too long","lastName":"too-long"}""", "lastName", "LastName")]
    [InlineData("application/xml", "<pet><name>a name that is much too long</name><surname>too-long</surname></pet>", "surname", "surname")]
    public async Task BindAsyncBody_IsKeyedRelativeToTheBody_InTheNamesOfTheRequestsMediaType(
        string contentType,
        string body,
        string lastNameKey,
        string lastNameInTheMessage)
    {
        // Act
        var response = await fixture.Default.PostAsync("/bind-async", body, contentType);

        // Assert - an XML request's message names the member by its XML name too, as its key does
        response.ShouldBeValidationProblem(
            ("name", "The field Name must be a string or array type with a maximum length of '10'."),
            (lastNameKey, $"The field {lastNameInTheMessage} must be a string or array type with a maximum length of '5'."));
    }

    [Fact]
    public async Task BindAsyncBody_Missing_IsReportedUnderTheEmptyKey()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/bind-async", json: null);

        // Assert
        response.ShouldBeValidationProblem(("", "A non-empty request body is required."));
    }

    internal static Task<ValidationBehaviourResponse> PostSourcesAsync(
        HttpClient client,
        string pathAndQuery,
        string? tenant,
        string? body)
        => client.PostJsonAsync(
            pathAndQuery,
            body,
            request =>
            {
                if (tenant is not null)
                {
                    request.Headers.Add("x-tenant", tenant);
                }
            });
}