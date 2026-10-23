namespace Demo.Api.Contracts.Contracts.Users.Parameters;

public record CreateUserParameters(
    [property: FromBody] CreateUserRequest Request);

public record GetUserByIdParameters(
    [property: FromRoute] Guid UserId);

public record GetUserByEmailParameters(
    [property: FromQuery] string Email);

/// <summary>
/// A list body: each user is validated as on create - by the [Required] and other attributes of CreateUserRequest and by
/// its FluentValidation validator - and an error is keyed by the user's index ([1].firstName).
/// </summary>
public record ImportUsersParameters(
    [property: FromBody] IList<CreateUserRequest> Request);

public record UpdateUserByIdParameters(
    [property: FromRoute] Guid UserId,
    [property: FromBody] UpdateUserRequest Request);

public record DeleteUserByIdParameters(
    [property: FromRoute] Guid UserId);