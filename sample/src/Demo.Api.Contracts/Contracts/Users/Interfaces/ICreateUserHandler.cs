namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface ICreateUserHandler
{
    Task<Results<CreatedAtRoute, ProblemHttpResult>> ExecuteAsync(
        CreateUserParameters parameters,
        CancellationToken cancellationToken = default);
}