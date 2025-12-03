namespace Demo.Api.IntegrationTests.Endpoints;

public class UsersEndpointDefinitionTests(TestWebApplicationFactory<Program> factory)
    : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly HttpClient httpClient = factory.CreateClient();

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