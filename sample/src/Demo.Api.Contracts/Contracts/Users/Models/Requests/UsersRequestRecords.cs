namespace Demo.Api.Contracts.Contracts.Users.Models.Requests;

public sealed record CreateUserRequest(
    [property: Required] GenderType? Gender,
    [property: Required, JsonPropertyName("firstName")] string FirstName,
    [property: Required] string LastName,
    [property: Required, EmailAddress] string Email,
    string? Telephone,
    [property: Url] string? HomePage,
    Address? HomeAddress,
    Address? WorkAddress);

public sealed record UpdateUserRequest(
    GenderType? Gender,
    [property: JsonPropertyName("firstName")] string? FirstName,
    string? LastName,
    [property: EmailAddress] string? Email,
    [property: JsonPropertyName("address")] Address? WorkAddress);