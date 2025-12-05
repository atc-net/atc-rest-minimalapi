namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface IDeleteUserByIdHandler
{
    Task<Results<NoContent, BadRequest<string>, NotFound>> ExecuteAsync(
        DeleteUserByIdParameters parameters,
        CancellationToken cancellationToken = default);
}