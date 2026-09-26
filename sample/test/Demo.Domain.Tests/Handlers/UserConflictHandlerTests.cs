namespace Demo.Domain.Tests.Handlers;

/// <summary>
/// Handler errors are RFC 9457 ProblemDetails (application/problem+json), not bare JSON strings,
/// so clients can read the message from "detail" like any other error.
/// </summary>
public sealed class UserConflictHandlerTests : HandlerTestBase
{
    private const string ExistingEmail = "james@bond.com";

    [Fact]
    public async Task CreateUser_WithExistingEmail_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddUserAsync(Guid.NewGuid(), ExistingEmail, cancellationToken);

        var sut = new CreateUserHandler(Context);
        var parameters = new CreateUserParameters(
            new CreateUserRequest(GenderType.Male, "Ada", "Tester", ExistingEmail, null, null, null, null));

        // Act
        var result = await sut.ExecuteAsync(parameters, cancellationToken);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal($"A user already exists with the email '{ExistingEmail}'.", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task UpdateUser_WithEmailOfAnotherUser_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddUserAsync(Guid.NewGuid(), ExistingEmail, cancellationToken);
        var userId = Guid.NewGuid();
        await AddUserAsync(userId, "ada@example.com", cancellationToken);

        var sut = new UpdateUserByIdHandler(Context);
        var parameters = new UpdateUserByIdParameters(
            userId,
            new UpdateUserRequest(Gender: null, FirstName: null, LastName: null, Email: ExistingEmail, WorkAddress: null));

        // Act
        var result = await sut.ExecuteAsync(parameters, cancellationToken);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal($"A user already exists with the email '{ExistingEmail}'.", problem.ProblemDetails.Detail);
    }

    private Task<int> AddUserAsync(
        Guid id,
        string email,
        CancellationToken cancellationToken)
    {
        Context.Users.Add(new UserEntity { Id = id, Gender = GenderType.Male, FirstName = "James", LastName = "Bond", Email = email });
        return Context.SaveChangesAsync(cancellationToken);
    }
}