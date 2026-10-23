namespace Demo.Api.Contracts.Contracts.Users.Interfaces;

public interface IImportUsersHandler
{
    Task<Results<Ok<int>, ProblemHttpResult>> ExecuteAsync(
        ImportUsersParameters parameters,
        CancellationToken cancellationToken = default);
}