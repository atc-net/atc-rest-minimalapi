namespace Demo.Domain.Tests.Handlers;

public sealed class ImportUsersHandlerTests : HandlerTestBase
{
    [Fact]
    public async Task ImportUsers_NewUsers_AddsEachAndReturnsTheCount()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var sut = new ImportUsersHandler(Context);
        var parameters = new ImportUsersParameters([NewUser("ada@example.com"), NewUser("grace@example.com")]);

        // Act
        var result = await sut.ExecuteAsync(parameters, cancellationToken);

        // Assert
        Assert.Equal(2, Assert.IsType<Ok<int>>(result.Result).Value);
        Assert.Equal(2, await Context.Users.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task ImportUsers_AnEmailTakenByAStoredUser_ReturnsConflict_AddsNone()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        Context.Users.Add(new UserEntity { Id = Guid.NewGuid(), Gender = GenderType.Male, FirstName = "James", LastName = "Bond", Email = "james@bond.com" });
        await Context.SaveChangesAsync(cancellationToken);
        var sut = new ImportUsersHandler(Context);

        // Act
        var result = await sut.ExecuteAsync(new ImportUsersParameters([NewUser("ada@example.com"), NewUser("JAMES@bond.com")]), cancellationToken);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal(1, await Context.Users.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task ImportUsers_AnEmailTwiceInTheList_ReturnsConflict()
    {
        // Arrange
        var sut = new ImportUsersHandler(Context);

        // Act
        var result = await sut.ExecuteAsync(new ImportUsersParameters([NewUser("ada@example.com"), NewUser("Ada@Example.com")]), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("The email 'ada@example.com' is in the list more than once.", Assert.IsType<ProblemHttpResult>(result.Result).ProblemDetails.Detail);
    }

    private static CreateUserRequest NewUser(string email)
        => new(GenderType.Female, "Ada", "Lovelace", email, null, null, null, null);
}