namespace Demo.Domain.Tests.Handlers;

public class GetUsersHandlerTests : HandlerTestBase
{
    [Theory, AutoNSubstituteData]
    public async Task GetAllUsers_ReturnsOkResultOfIEnumerableUsers(
        ICollection<UserEntity> userEntities,
        CancellationToken cancellationToken)
    {
        // Arrange
        Context.Users.AddRange(userEntities);
        await Context.SaveChangesAsync(cancellationToken);

        var sut = new GetUsersHandler(Context);

        // Act
        var result = await sut.ExecuteAsync(cancellationToken);

        // Assert
        result.Should().NotBeNull();

        if (result.Value!.TryGetNonEnumeratedCount(out var numberOfUsers))
        {
            Assert.Equal(userEntities.Count, numberOfUsers);
        }

        Assert.IsType<Ok<IEnumerable<Api.Contracts.Contracts.Users.Models.Responses.User>>>(result);
    }
}