namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface IDeleteUserByIdHandler
{
    Task<Results<NoContent, NotFound, ProblemHttpResult>> ExecuteAsync(
        DeleteUserByIdParameters parameters,
        CancellationToken cancellationToken = default);
}