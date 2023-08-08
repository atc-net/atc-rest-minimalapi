namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface IGetUserByIdHandler
{
    Task<Results<Ok<User>, NotFound>> ExecuteAsync(
        GetUserByIdParameters parameters,
        CancellationToken cancellationToken = default);
}