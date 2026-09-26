namespace Demo.EndToEndTests;

/// <summary>
/// Every error the sample API returns is RFC 9457 ProblemDetails (application/problem+json):
/// empty framework errors via AddProblemDetails() + UseStatusCodePages(), and handler errors via TypedResults.Problem.
/// Explicit (opt-in), like the other end-to-end tests.
/// </summary>
[Collection(DemoAppCollectionDefinition.Name)]
public sealed class ApiErrorResponseTests(DemoAppFixture fixture)
{
    [Fact(Explicit = true)]
    public async Task GetUnknownUser_Returns404ProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await fixture.GetApiClientAsync(cancellationToken);

        // Act
        using var response = await client.GetAsync(new Uri($"/api/users/{Guid.NewGuid()}", UriKind.Relative), cancellationToken);

        // Assert - not "Status Code: 404; Not Found" as text/plain
        ((int)response.StatusCode).Should().Be(404);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        body.RootElement.GetProperty("status").GetInt32().Should().Be(404);
    }

    [Fact(Explicit = true)]
    public async Task CreateUserWithExistingEmail_Returns409ProblemDetailsWithDetail()
    {
        // Arrange - take the email of an existing (seeded) user
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await fixture.GetApiClientAsync(cancellationToken);
        using var users = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(new Uri("/api/users", UriKind.Relative), cancellationToken));
        var existingEmail = users.RootElement[0].GetProperty("email").GetString();

        using var content = new StringContent(
            $$"""{"gender":"Male","firstName":"Ada","lastName":"Tester","email":"{{existingEmail}}"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        using var response = await client.PostAsync(new Uri("/api/users", UriKind.Relative), content, cancellationToken);

        // Assert - a ProblemDetails body instead of a bare JSON string
        ((int)response.StatusCode).Should().Be(409);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        body.RootElement.GetProperty("detail").GetString().Should().Be($"A user already exists with the email '{existingEmail}'.");
    }
}