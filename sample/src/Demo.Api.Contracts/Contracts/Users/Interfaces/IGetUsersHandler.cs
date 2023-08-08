namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface IGetUsersHandler
{
    Task<Ok<IEnumerable<User>>> ExecuteAsync(
        CancellationToken cancellationToken = default);
}