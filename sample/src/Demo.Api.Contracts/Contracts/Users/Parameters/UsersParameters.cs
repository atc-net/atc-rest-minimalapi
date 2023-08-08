namespace Demo.Api.Contracts.Contracts.Users.Parameters;

public record CreateUserParameters(
    [property: FromBody] CreateUserRequest Request);

public record GetUserByIdParameters(
    [property: FromRoute] Guid UserId);

public record GetUserByEmailParameters(
    [property: FromQuery] string Email);

public record UpdateUserByIdParameters(
    [property: FromRoute] Guid UserId,
    [property: FromBody] UpdateUserRequest Request);

public record DeleteUserByIdParameters(
    [property: FromRoute] Guid UserId);