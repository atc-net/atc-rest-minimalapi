namespace Demo.Api.Contracts.Contracts.Users.Models.Requests;

public sealed record CreateUserRequest(
    GenderType Gender,
    string FirstName,
    string LastName,
    [property: EmailAddress] string Email,
    string Telephone,
    [property: Uri] string HomePage,
    Address? HomeAddress,
    Address WorkAddress);

public sealed record UpdateUserRequest(
    [property: Required] GenderType Gender,
    [property: Required, JsonPropertyName("firstName")] string FirstName,
    [property: Required] string LastName,
    [property: Required, EmailAddress] string Email,
    [property: Required, JsonPropertyName("address")] Address WorkAddress);