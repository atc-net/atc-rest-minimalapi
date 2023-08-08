namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface IDeleteUserByIdHandler
{
    Task<Results<NoContent, NotFound>> ExecuteAsync(
        DeleteUserByIdParameters parameters,
        CancellationToken cancellationToken = default);
}