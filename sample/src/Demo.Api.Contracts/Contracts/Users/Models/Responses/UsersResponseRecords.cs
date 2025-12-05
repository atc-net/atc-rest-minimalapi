namespace Demo.Api.Contracts.Contracts.Users.Models.Responses;

public sealed record User(
    Guid Id,
    GenderType Gender,
    string FirstName,
    string LastName,
    string Email,
    string? Telephone,
    Uri? HomePage,
    Address? HomeAddress,
    Address? WorkAddress);