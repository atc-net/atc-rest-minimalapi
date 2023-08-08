namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface IUpdateUserByIdHandler
{
    Task<Results<Ok<User>, BadRequest<string>, NotFound, Conflict<string>>> ExecuteAsync(
        UpdateUserByIdParameters parameters,
        CancellationToken cancellationToken = default);
}