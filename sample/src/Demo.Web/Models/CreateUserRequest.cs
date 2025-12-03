namespace Demo.Web.Models;

public class CreateUserRequest
{
    public GenderType Gender { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Telephone { get; set; }

    public string? HomePage { get; set; }

    public Address? HomeAddress { get; set; }

    public Address? WorkAddress { get; set; }
}