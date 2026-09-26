namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface IUpdateUserByIdHandler
{
    Task<Results<Ok<User>, NotFound, ProblemHttpResult>> ExecuteAsync(
        UpdateUserByIdParameters parameters,
        CancellationToken cancellationToken = default);
}