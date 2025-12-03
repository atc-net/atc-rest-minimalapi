namespace Demo.Web.Models;

public record User(
    Guid Id,
    GenderType Gender,
    string FirstName,
    string LastName,
    string Email,
    string? Telephone,
    string? HomePage,
    Address? HomeAddress,
    Address? WorkAddress);