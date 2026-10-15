namespace Demo.Web.Services;

public class UserService : IUserService
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonSerializerOptions;
    private readonly ILogger<UserService> logger;

    public UserService(
        HttpClient httpClient,
        JsonSerializerOptions jsonSerializerOptions,
        ILogger<UserService> logger)
    {
        this.httpClient = httpClient;
        this.jsonSerializerOptions = jsonSerializerOptions;
        this.logger = logger;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var users = await httpClient.GetFromJsonAsync<IEnumerable<User>>(
                "/api/users",
                jsonSerializerOptions,
                cancellationToken);

            return users ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all users");
            return [];
        }
    }

    public async Task<User?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<User>(
                $"/api/users/{userId}",
                jsonSerializerOptions,
                cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get user {UserId}", userId);
            return null;
        }
    }

    public async Task<(bool Success, ApiValidationProblemDetails? Errors)> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "/api/users",
                request.ToRequestBody(),
                jsonSerializerOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            return (false, await ReadProblemAsync(response, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create user");
            return (false, new ApiValidationProblemDetails
            {
                Title = "Error",
                Status = 500,
                Detail = ex.Message,
            });
        }
    }

    public async Task<(bool Success, User? User, ApiValidationProblemDetails? Errors)> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var response = await httpClient.PutAsJsonAsync(
                $"/api/users/{userId}",
                request.ToRequestBody(),
                jsonSerializerOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<User>(jsonSerializerOptions, cancellationToken);
                return (true, user, null);
            }

            return (false, null, await ReadProblemAsync(response, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update user {UserId}", userId);
            return (false, null, new ApiValidationProblemDetails
            {
                Title = "Error",
                Status = 500,
                Detail = ex.Message,
            });
        }
    }

    public async Task<bool> DeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.DeleteAsync(
                $"/api/users/{userId}",
                cancellationToken);

            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Reads the error body of a failed response. The API answers errors with RFC 9457 ProblemDetails
    /// (application/problem+json): a 400 ValidationProblem with field errors, or e.g. a 409 with a "detail" message.
    /// </summary>
    private async Task<ApiValidationProblemDetails?> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
        {
            return await response.Content.ReadFromJsonAsync<ApiValidationProblemDetails>(
                jsonSerializerOptions,
                cancellationToken);
        }

        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError("Request failed with status {StatusCode}: {Content}", response.StatusCode, errorContent);

        return new ApiValidationProblemDetails
        {
            Title = "Error",
            Status = (int)response.StatusCode,
            Detail = errorContent,
        };
    }
}