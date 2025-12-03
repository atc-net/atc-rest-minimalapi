namespace Demo.Web.Services;

public interface IUserService
{
    Task<IEnumerable<User>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<(bool Success, ApiValidationProblemDetails? Errors)> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Success, User? User, ApiValidationProblemDetails? Errors)> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}