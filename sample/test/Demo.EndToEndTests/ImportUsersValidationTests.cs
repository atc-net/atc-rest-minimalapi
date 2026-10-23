namespace Demo.EndToEndTests;

/// <summary>
/// A list body is validated user by user, as a single user is on create: the attributes of CreateUserRequest and its
/// FluentValidation validator, each error keyed by the user's index. Explicit (opt-in), like the other end-to-end tests.
/// </summary>
[Collection(DemoAppCollectionDefinition.Name)]
public sealed class ImportUsersValidationTests(DemoAppFixture fixture)
{
    [Fact(Explicit = true)]
    public async Task ImportUsers_InvalidUsers_Returns400KeyedByTheUsersIndex()
    {
        // Arrange - user 0 is valid; user 1 breaks the validator's uppercase rule; user 2 has no email ([Required])
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await fixture.GetApiClientAsync(cancellationToken);
        using var content = new StringContent(
            """
            [
              {"gender":"Female","firstName":"Ada","lastName":"Lovelace","email":"ada.import@example.com"},
              {"gender":"Female","firstName":"grace","lastName":"Hopper","email":"grace.import@example.com"},
              {"gender":"Male","firstName":"Alan","lastName":"Turing"}
            ]
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        using var response = await client.PostAsync(new Uri("/api/users/import", UriKind.Relative), content, cancellationToken);

        // Assert
        ((int)response.StatusCode).Should().Be(400);
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var errors = body.RootElement.GetProperty("errors");
        errors.EnumerateObject().Select(e => e.Name).Should().BeEquivalentTo("[1].firstName", "[2].email");
        errors.GetProperty("[1].firstName")[0].GetString().Should().Be("FirstName has to start with an uppercase letter.");
    }
}