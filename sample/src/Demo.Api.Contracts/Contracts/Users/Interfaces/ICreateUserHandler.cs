namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface ICreateUserHandler
{
    Task<Results<CreatedAtRoute, BadRequest<string>, Conflict<string>>> ExecuteAsync(
        CreateUserParameters parameters,
        CancellationToken cancellationToken = default);
}