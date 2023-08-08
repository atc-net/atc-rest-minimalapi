namespace Demo.Api.Contracts.Tests.Endpoints;

public class UsersEndpointDefinitionTests
{
    [Theory, AutoNSubstituteData]
    public async Task GetAllUsers_ReturnsOkResultOfIEnumerableUser(
        [Frozen] IGetUsersHandler handler,
        UsersEndpointDefinition sut,
        CancellationToken cancellationToken,
        List<Contracts.Users.Models.Responses.User> users)
    {
        // Arrange
        handler
            .ExecuteAsync(cancellationToken)
            .Returns(TypedResults.Ok(users.AsEnumerable()));

        // Act
        var result = await sut.GetAllUsers(handler, cancellationToken);

        // Assert
        var typedResult = Assert.IsType<Ok<IEnumerable<Contracts.Users.Models.Responses.User>>>(result);
        Assert.Equal(StatusCodes.Status200OK, typedResult.StatusCode);
        Assert.Equal(users, typedResult.Value);
    }
}