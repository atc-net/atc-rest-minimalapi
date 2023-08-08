namespace Demo.Api.IntegrationTests.Endpoints;

public class UsersEndpointDefinitionTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly HttpClient httpClient;

    public UsersEndpointDefinitionTests(
        TestWebApplicationFactory<Program> factory)
    {
        httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task GetAllUsers_ReturnsOkResultOfIEnumerableUser()
    {
        // Arrange

        // Act
        var response = await httpClient.GetFromJsonAsync<IEnumerable<User>>(
            UsersEndpointDefinition.ApiRouteBase,
            JsonSerializerOptionsFactory.Create());

        // Assert
        Assert.NotNull(response);
    }
}