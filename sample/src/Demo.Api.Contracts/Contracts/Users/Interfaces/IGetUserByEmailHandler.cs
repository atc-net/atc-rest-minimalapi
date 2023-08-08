namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface IGetUserByEmailHandler
{
    Task<Results<Ok<User>, NotFound>> ExecuteAsync(
        GetUserByEmailParameters parameters,
        CancellationToken cancellationToken = default);
}